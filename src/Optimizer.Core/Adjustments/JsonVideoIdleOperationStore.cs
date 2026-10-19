using System.Text.Json;

namespace Optimizer.Core.Adjustments;

/// <summary>Persists a real, read-only VIDEOIDLE capture separately from synthetic demo history.</summary>
public sealed class JsonVideoIdleOperationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directoryPath;
    private readonly Optimizer.Core.Recovery.IOperationJournalStorage _storage;

    public JsonVideoIdleOperationStore(
        string directoryPath,
        Optimizer.Core.Recovery.IOperationJournalStorage? storage = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("VIDEOIDLE operation directory is required.", nameof(directoryPath));

        _directoryPath = Path.GetFullPath(directoryPath);
        _storage = storage ?? new Optimizer.Core.Recovery.PhysicalOperationJournalStorage();
    }

    public async Task SaveAsync(VideoIdleOperationRecord record, CancellationToken cancellationToken = default)
    {
        Validate(record);
        _storage.CreateDirectory(_directoryPath);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        await _storage.WriteAtomicAsync(GetPath(record.OperationId), bytes, cancellationToken);
    }

    public async Task<VideoIdleOperationRecord?> LoadAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Operation ID is required.", nameof(operationId));

        var path = GetPath(operationId);
        if (!_storage.FileExists(path))
            return null;

        var bytes = await _storage.ReadAllBytesAsync(path, cancellationToken);
        var record = JsonSerializer.Deserialize<VideoIdleOperationRecord>(bytes, JsonOptions);
        if (record is null || record.OperationId != operationId)
            throw new InvalidDataException("VIDEOIDLE operation identity is invalid.");
        Validate(record);
        return record;
    }

    public async Task<IReadOnlyList<VideoIdleOperationRecord>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_storage.DirectoryExists(_directoryPath))
            return Array.Empty<VideoIdleOperationRecord>();

        var records = new List<VideoIdleOperationRecord>();
        foreach (var path in _storage.EnumerateFiles(_directoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var operationId))
                continue;
            var record = await LoadAsync(operationId, cancellationToken);
            if (record is not null)
                records.Add(record);
        }

        return Array.AsReadOnly(records.OrderByDescending(record => record.CapturedAtUtc).ToArray());
    }

    private string GetPath(Guid operationId) =>
        Path.Combine(_directoryPath, operationId.ToString("D") + ".json");

    private static void Validate(VideoIdleOperationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.SchemaVersion != 1 || record.OperationId == Guid.Empty || record.ActiveSchemeId == Guid.Empty ||
            record.CapturedAtUtc == default || record.UpdatedAtUtc < record.CapturedAtUtc ||
            string.IsNullOrWhiteSpace(record.Detail))
        {
            throw new InvalidDataException("VIDEOIDLE operation record is incomplete or has an unsupported schema.");
        }

        var expected = VideoIdleCaptureFingerprint.Compute(
            record.OperationId,
            record.ActiveSchemeId,
            record.PreviousAcTimeoutSeconds,
            record.ProposedAcTimeoutSeconds);
        if (!string.Equals(record.CaptureFingerprint, expected, StringComparison.Ordinal))
            throw new InvalidDataException("VIDEOIDLE capture fingerprint does not match its persisted values.");
    }
}
