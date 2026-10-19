namespace Optimizer.Core.Recovery;

/// <summary>Storage boundary for fault-injected journal persistence tests.</summary>
public interface IOperationJournalStorage
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    Task WriteAtomicAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);
}

public sealed class PhysicalOperationJournalStorage : IOperationJournalStorage
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    public async Task WriteAtomicAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(contents, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Preserve the original write failure; history reads ignore orphaned temp files.
                }
            }
        }
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
        await File.ReadAllBytesAsync(path, cancellationToken);
}
