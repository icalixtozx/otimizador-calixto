using System.Net;

namespace Optimizer.Core.Network;

public sealed record NetworkAdapterInfo(
    string Name,
    string Description,
    string InterfaceType,
    string LinkState,
    bool IsOperational,
    long? LinkSpeedBitsPerSecond,
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers);

public sealed record NetworkInventoryResult(
    DateTimeOffset CollectedAtUtc,
    IReadOnlyList<NetworkAdapterInfo> Adapters,
    IReadOnlyList<string> Warnings);

public enum NetworkProbeStatus
{
    Success,
    TimedOut,
    DestinationUnreachable,
    Failed
}

public sealed record NetworkProbeReply(
    NetworkProbeStatus Status,
    long? RoundtripMilliseconds = null,
    string? StatusDetail = null);

public interface INetworkDiagnosticsAdapter
{
    NetworkInventoryResult ReadAdapters();
    Task<IReadOnlyList<IPAddress>> ResolveHostAsync(string hostName, CancellationToken cancellationToken);
    Task<NetworkProbeReply> ProbeAsync(IPAddress destination, TimeSpan timeout, CancellationToken cancellationToken);
}

public enum NetworkConnectivityOutcome
{
    Success,
    PartialResponses,
    NoIcmpResponses,
    DnsResolutionFailed,
    NoActiveConnection,
    AdapterReadFailed,
    ProbeFailed,
    Cancelled
}

public sealed record NetworkProbeProgress(int CompletedSamples, int RequestedSamples, int RepliesReceived);

public sealed record NetworkConnectivityResult(
    string Target,
    string Purpose,
    IPAddress? ResolvedAddress,
    NetworkConnectivityOutcome Outcome,
    int RequestedSamples,
    int CompletedSamples,
    int RepliesReceived,
    int TimedOutSamples,
    int FailedSamples,
    int TimeoutMilliseconds,
    long DurationMilliseconds,
    IReadOnlyList<long> RoundtripTimesMilliseconds,
    IReadOnlyList<string> ProbeDetails,
    string Summary)
{
    public int LostResponses => Math.Max(0, CompletedSamples - RepliesReceived);
    public double? LossPercentage => CompletedSamples == 0 ? null : LostResponses * 100d / CompletedSamples;
    public long? MinimumRoundtripMilliseconds => RoundtripTimesMilliseconds.Count == 0 ? null : RoundtripTimesMilliseconds.Min();
    public double? AverageRoundtripMilliseconds => RoundtripTimesMilliseconds.Count == 0
        ? null
        : RoundtripTimesMilliseconds.Average();
    public long? MaximumRoundtripMilliseconds => RoundtripTimesMilliseconds.Count == 0 ? null : RoundtripTimesMilliseconds.Max();
}
