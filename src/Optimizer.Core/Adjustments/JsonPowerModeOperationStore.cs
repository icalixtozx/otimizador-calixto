using System.Text.Json;
using Optimizer.Core.Recovery;

namespace Optimizer.Core.Adjustments;

/// <summary>Stores power-mode operation captures separately from synthetic demonstrations and VIDEOIDLE.</summary>
public sealed class JsonPowerModeOperationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directoryPath;
    private readonly IOperationJournalStorage _storage;

    public JsonPowerModeOperationStore(string directoryPath, IOperationJournalStorage? storage = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Power-mode operation directory is required.", nameof(directoryPath));
        _directoryPath = Path.GetFullPath(directoryPath);
        _storage = storage ?? new PhysicalOperationJournalStorage();
    }

    public async Task SaveAsync(PowerModeOperationRecord record, CancellationToken cancellationToken = default)
    {
        Validate(record);
        _storage.CreateDirectory(_directoryPath);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        await _storage.WriteAtomicAsync(GetPath(record.OperationId), bytes, cancellationToken);
    }

    public async Task<PowerModeOperationRecord?> LoadAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        var path = GetPath(operationId);
        if (!_storage.FileExists(path)) return null;
        var bytes = await _storage.ReadAllBytesAsync(path, cancellationToken);
        var record = JsonSerializer.Deserialize<PowerModeOperationRecord>(bytes, JsonOptions);
        if (record is null || record.OperationId != operationId)
            throw new InvalidDataException("Power-mode operation identity is invalid.");
        Validate(record);
        return record;
    }

    public async Task<IReadOnlyList<PowerModeOperationRecord>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_storage.DirectoryExists(_directoryPath)) return Array.Empty<PowerModeOperationRecord>();
        var records = new List<PowerModeOperationRecord>();
        foreach (var path in _storage.EnumerateFiles(_directoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var operationId)) continue;
            var record = await LoadAsync(operationId, cancellationToken);
            if (record is not null) records.Add(record);
        }
        return Array.AsReadOnly(records.OrderByDescending(record => record.CapturedAtUtc).ToArray());
    }

    private string GetPath(Guid operationId) => Path.Combine(_directoryPath, operationId.ToString("D") + ".json");

    private static void Validate(PowerModeOperationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var hasAc = record.TargetAcMode is not null;
        var hasDc = record.TargetDcMode is not null;
        if (record.SchemaVersion is not (1 or 2) || record.OperationId == Guid.Empty ||
            record.CapturedAtUtc == default || record.UpdatedAtUtc < record.CapturedAtUtc ||
            !Enum.IsDefined(record.State) || string.IsNullOrWhiteSpace(record.Detail) || (!hasAc && !hasDc) ||
            (record.SchemaVersion == 1 && (hasAc != (record.PreviousAcMode is not null) || hasDc != (record.PreviousDcMode is not null))) ||
            (record.SchemaVersion == 2 && (record.PreviousAcMode is null || record.PreviousDcMode is null || record.CapturedActivePlanId is null || record.CapturedActivePlanId == Guid.Empty)) ||
            (record.PreviousAcMode is { } ac && !Enum.IsDefined(ac)) ||
            (record.PreviousDcMode is { } dc && !Enum.IsDefined(dc)) ||
            (record.TargetAcMode is { } targetAc && !Enum.IsDefined(targetAc)) ||
            (record.TargetDcMode is { } targetDc && !Enum.IsDefined(targetDc)))
        {
            throw new InvalidDataException("Power-mode operation record is incomplete or has an unsupported schema.");
        }

        var expected = PowerModeCaptureFingerprint.Compute(record);
        if (!string.Equals(record.CaptureFingerprint, expected, StringComparison.Ordinal))
            throw new InvalidDataException("Power-mode capture fingerprint does not match its persisted values.");
    }
}
