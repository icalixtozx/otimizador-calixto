using System.Text.Json;

namespace Optimizer.Core.Recovery;

public sealed class JsonOperationJournalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directoryPath;
    private readonly IOperationJournalStorage _storage;

    public JsonOperationJournalStore(string directoryPath, IOperationJournalStorage? storage = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Journal directory is required.", nameof(directoryPath));

        _directoryPath = Path.GetFullPath(directoryPath);
        _storage = storage ?? new PhysicalOperationJournalStorage();
    }

    public async Task SaveAsync(OperationJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.OperationId == Guid.Empty || entry.PlanId == Guid.Empty ||
            string.IsNullOrWhiteSpace(entry.PlanFingerprint))
        {
            throw new ArgumentException("Operation and plan identity are required.", nameof(entry));
        }

        _storage.CreateDirectory(_directoryPath);
        var path = GetPath(entry.OperationId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        await _storage.WriteAtomicAsync(path, bytes, cancellationToken);
    }

    public async Task<OperationJournalEntry?> LoadAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Operation ID is required.", nameof(operationId));

        var path = GetPath(operationId);
        if (!_storage.FileExists(path))
            return null;

        var bytes = await _storage.ReadAllBytesAsync(path, cancellationToken);
        var entry = JsonSerializer.Deserialize<OperationJournalEntry>(bytes, JsonOptions);
        if (entry is null || entry.OperationId != operationId)
            throw new InvalidDataException("Journal file identity is invalid.");

        return entry;
    }

    public async Task<IReadOnlyList<OperationJournalEntry>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_storage.DirectoryExists(_directoryPath))
            return Array.Empty<OperationJournalEntry>();

        var entries = new List<OperationJournalEntry>();
        foreach (var path in _storage.EnumerateFiles(_directoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!Guid.TryParseExact(fileName, "D", out var operationId))
                continue;

            var entry = await LoadAsync(operationId, cancellationToken);
            if (entry is not null)
                entries.Add(entry);
        }

        return Array.AsReadOnly(entries.OrderByDescending(entry => entry.CreatedAtUtc).ToArray());
    }

    private string GetPath(Guid operationId) =>
        Path.Combine(_directoryPath, operationId.ToString("D") + ".json");
}
