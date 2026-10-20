using System.Net;
using System.Net.NetworkInformation;
using Optimizer.Core.Network;

namespace Optimizer.Windows;

/// <summary>Windows/.NET network queries; the inventory path is local and does not send test traffic.</summary>
public sealed class WindowsNetworkDiagnosticsAdapter : INetworkDiagnosticsAdapter
{
    private static readonly byte[] ProbePayload = new byte[32];

    public NetworkInventoryResult ReadAdapters()
    {
        var collectedAt = DateTimeOffset.UtcNow;
        var adapters = new List<NetworkAdapterInfo>();
        var warnings = new List<string>();

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    var properties = networkInterface.GetIPProperties();
                    adapters.Add(new NetworkAdapterInfo(
                        networkInterface.Name,
                        networkInterface.Description,
                        networkInterface.NetworkInterfaceType.ToString(),
                        networkInterface.OperationalStatus.ToString(),
                        networkInterface.OperationalStatus == OperationalStatus.Up,
                        networkInterface.Speed >= 0 ? networkInterface.Speed : null,
                        properties.UnicastAddresses.Select(item => item.Address.ToString()).ToArray(),
                        properties.GatewayAddresses.Select(item => item.Address.ToString())
                            .Where(address => !string.IsNullOrWhiteSpace(address)).ToArray(),
                        properties.DnsAddresses.Select(address => address.ToString()).ToArray()));
                }
                catch (Exception exception) when (exception is NetworkInformationException or InvalidOperationException or NotSupportedException)
                {
                    warnings.Add($"Um adaptador não pôde ser lido ({exception.GetType().Name}).");
                }
            }
        }
        catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException or NotSupportedException)
        {
            warnings.Add($"A consulta de adaptadores falhou ({exception.GetType().Name}).");
        }

        return new NetworkInventoryResult(collectedAt, adapters, warnings);
    }

    public async Task<IReadOnlyList<IPAddress>> ResolveHostAsync(string hostName, CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(hostName, cancellationToken).ConfigureAwait(false);

    public async Task<NetworkProbeReply> ProbeAsync(IPAddress destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(destination, timeout, ProbePayload, cancellationToken: cancellationToken).ConfigureAwait(false);
        return reply.Status switch
        {
            IPStatus.Success => new NetworkProbeReply(NetworkProbeStatus.Success, checked((long)reply.RoundtripTime), reply.Status.ToString()),
            IPStatus.TimedOut => new NetworkProbeReply(NetworkProbeStatus.TimedOut, StatusDetail: reply.Status.ToString()),
            IPStatus.DestinationHostUnreachable or IPStatus.DestinationNetworkUnreachable or IPStatus.DestinationPortUnreachable =>
                new NetworkProbeReply(NetworkProbeStatus.DestinationUnreachable, StatusDetail: reply.Status.ToString()),
            _ => new NetworkProbeReply(NetworkProbeStatus.Failed, StatusDetail: reply.Status.ToString())
        };
    }
}
