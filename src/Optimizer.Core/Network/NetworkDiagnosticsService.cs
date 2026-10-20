using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Optimizer.Core.Network;

/// <summary>Read-only adapter inventory plus explicit, bounded DNS/ICMP diagnostics.</summary>
public sealed class NetworkDiagnosticsService(INetworkDiagnosticsAdapter adapter)
{
    public const int DefaultSampleCount = 4;
    public const int DefaultTimeoutMilliseconds = 1000;
    public const int MaximumSampleCount = 10;
    public const int MaximumTimeoutMilliseconds = 5000;

    private const string TestPurpose = "Resolve the named destination if needed, then send a small ICMP echo request to the displayed IP address.";

    public NetworkInventoryResult ReadAdapters() => adapter.ReadAdapters();

    public async Task<NetworkConnectivityResult> TestConnectivityAsync(
        string target,
        int sampleCount = DefaultSampleCount,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds,
        IProgress<NetworkProbeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedTarget = target?.Trim() ?? string.Empty;
        if (!IsValidTarget(normalizedTarget))
            throw new ArgumentException("Informe um nome de host ou endereço IP, sem URL, caminho ou porta.", nameof(target));
        if (sampleCount is < 1 or > MaximumSampleCount)
            throw new ArgumentOutOfRangeException(nameof(sampleCount), $"A quantidade deve estar entre 1 e {MaximumSampleCount}.");
        if (timeoutMilliseconds is < 100 or > MaximumTimeoutMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), $"O timeout deve estar entre 100 e {MaximumTimeoutMilliseconds} ms.");

        var stopwatch = Stopwatch.StartNew();
        if (cancellationToken.IsCancellationRequested)
            return Result(NetworkConnectivityOutcome.Cancelled, null, [], [], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);

        NetworkInventoryResult inventory;
        try
        {
            inventory = await Task.Run(adapter.ReadAdapters, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(NetworkConnectivityOutcome.Cancelled, null, [], [], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
        }
        catch (Exception exception)
        {
            return Result(NetworkConnectivityOutcome.AdapterReadFailed, null, [], [$"Leitura local indisponível ({exception.GetType().Name})."], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
        }

        if (!HasActiveAddress(inventory.Adapters))
        {
            var preflightOutcome = inventory.Warnings.Count > 0
                ? NetworkConnectivityOutcome.AdapterReadFailed
                : NetworkConnectivityOutcome.NoActiveConnection;
            return Result(preflightOutcome, null, [], inventory.Warnings, 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
        }

        IPAddress destination;
        if (IPAddress.TryParse(normalizedTarget, out var literalAddress))
        {
            destination = literalAddress;
        }
        else
        {
            try
            {
                var addresses = await adapter.ResolveHostAsync(normalizedTarget, cancellationToken).ConfigureAwait(false);
                if (addresses.Count == 0)
                    return Result(NetworkConnectivityOutcome.DnsResolutionFailed, null, [], [], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);

                // Test one address so the result has a single unambiguous destination. Prefer IPv4,
                // otherwise use the first address returned by the resolver; the UI displays it.
                destination = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result(NetworkConnectivityOutcome.Cancelled, null, [], [], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
            }
            catch (SocketException exception)
            {
                return Result(NetworkConnectivityOutcome.DnsResolutionFailed, null, [], [$"Resolução DNS falhou ({exception.SocketErrorCode})."], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
            }
            catch (OperationCanceledException)
            {
                return Result(NetworkConnectivityOutcome.DnsResolutionFailed, null, [], ["A resolução DNS excedeu o limite do resolvedor."], 0, 0, 0, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
            }
        }

        var roundtrips = new List<long>(sampleCount);
        var details = new List<string>();
        var completed = 0;
        var timedOut = 0;
        var failed = 0;
        var wasCancelled = false;

        for (var index = 0; index < sampleCount; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
                break;
            }

            NetworkProbeReply reply;
            try
            {
                reply = await adapter.ProbeAsync(destination, TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
                break;
            }
            catch (Exception exception)
            {
                reply = new NetworkProbeReply(NetworkProbeStatus.Failed, StatusDetail: exception.GetType().Name);
            }

            completed++;
            if (reply.Status == NetworkProbeStatus.Success && reply.RoundtripMilliseconds is long rtt)
                roundtrips.Add(Math.Max(0, rtt));
            else if (reply.Status == NetworkProbeStatus.TimedOut)
                timedOut++;
            else
            {
                failed++;
                if (!string.IsNullOrWhiteSpace(reply.StatusDetail)) details.Add(reply.StatusDetail);
            }

            progress?.Report(new NetworkProbeProgress(completed, sampleCount, roundtrips.Count));
        }

        var outcome = wasCancelled
            ? NetworkConnectivityOutcome.Cancelled
            : roundtrips.Count == sampleCount
                ? NetworkConnectivityOutcome.Success
                : roundtrips.Count > 0
                    ? NetworkConnectivityOutcome.PartialResponses
                    : timedOut == completed && completed > 0
                        ? NetworkConnectivityOutcome.NoIcmpResponses
                        : NetworkConnectivityOutcome.ProbeFailed;

        return Result(outcome, destination, roundtrips, details, completed, timedOut, failed, normalizedTarget, sampleCount, timeoutMilliseconds, stopwatch);
    }

    private static bool HasActiveAddress(IEnumerable<NetworkAdapterInfo> adapters) => adapters.Any(adapterInfo =>
        adapterInfo.IsOperational && adapterInfo.Addresses.Any(address =>
            IPAddress.TryParse(address.Split('%')[0], out var ip) && !IPAddress.IsLoopback(ip)));

    private static bool IsValidTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > 253 || target.Any(char.IsWhiteSpace)) return false;
        if (IPAddress.TryParse(target, out _)) return true;
        return Uri.CheckHostName(target) == UriHostNameType.Dns &&
               !target.Contains('/') && !target.Contains('\\') && !target.Contains(':');
    }

    private static NetworkConnectivityResult Result(
        NetworkConnectivityOutcome outcome,
        IPAddress? address,
        IReadOnlyList<long> roundtrips,
        IReadOnlyList<string> details,
        int completed,
        int timedOut,
        int failed,
        string target,
        int requested,
        int timeout,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        var received = roundtrips.Count;
        var summary = outcome switch
        {
            NetworkConnectivityOutcome.Success => "Todas as sondagens ICMP receberam resposta deste destino; isso não mede velocidade da internet.",
            NetworkConnectivityOutcome.PartialResponses => "Respostas ICMP parciais. A perda se refere somente às sondagens feitas e não mede disponibilidade geral da internet.",
            NetworkConnectivityOutcome.NoIcmpResponses => "Nenhuma resposta ICMP. O destino, a rota ou uma regra pode bloquear ICMP; isso não comprova falta de internet.",
            NetworkConnectivityOutcome.DnsResolutionFailed => "Falha ao resolver o nome informado. Nenhuma sondagem ICMP foi enviada.",
            NetworkConnectivityOutcome.NoActiveConnection => "Nenhum adaptador ativo com endereço não local foi detectado; a leitura é apenas local e não testa a internet.",
            NetworkConnectivityOutcome.AdapterReadFailed => "A leitura local dos adaptadores falhou ou ficou incompleta; não é possível concluir se há conexão.",
            NetworkConnectivityOutcome.ProbeFailed => "A sondagem falhou sem resposta classificável. Isso não comprova falta de internet.",
            NetworkConnectivityOutcome.Cancelled => "Teste cancelado; as medições abaixo incluem somente sondagens concluídas.",
            _ => "Resultado indisponível."
        };

        return new NetworkConnectivityResult(target, TestPurpose, address, outcome, requested, completed, received,
            timedOut, failed, timeout, (long)stopwatch.Elapsed.TotalMilliseconds, roundtrips.ToArray(), details.ToArray(), summary);
    }
}
