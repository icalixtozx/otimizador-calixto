using System.Security;
using System.Text;

namespace Optimizer.Core.Inventory;

public enum DiagnosticReportSaveState
{
    Cancelled,
    OverwriteConfirmationRequired,
    SavedAndVerified,
    Failed
}

public sealed record DiagnosticReportSaveResult(
    DiagnosticReportSaveState State,
    string Message,
    string? FailureType = null,
    bool TemporaryFileMayRemain = false);

public interface IDiagnosticReportFileOperations
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    void WriteNew(string path, ReadOnlyMemory<byte> contents);
    byte[] ReadAllBytes(string path);
    void MoveNew(string sourcePath, string destinationPath);
    void ReplaceExisting(string sourcePath, string destinationPath);
    void Delete(string path);
}

/// <summary>Saves the exact reviewed UTF-8 bytes and verifies the final file before reporting success.</summary>
public sealed class DiagnosticReportFileSaver(IDiagnosticReportFileOperations? fileOperations = null)
{
    private readonly IDiagnosticReportFileOperations _files = fileOperations ?? new PhysicalDiagnosticReportFileOperations();

    public DiagnosticReportSaveResult Save(string? destinationPath, string reviewedContent, bool overwriteConfirmed)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            return new(DiagnosticReportSaveState.Cancelled, "Salvamento cancelado; nenhum arquivo foi gravado.");

        string fullPath;
        string directory;
        string temporaryPath;
        byte[] contentBytes;
        try
        {
            fullPath = Path.GetFullPath(destinationPath);
            directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory) || !_files.DirectoryExists(directory))
                return new(DiagnosticReportSaveState.Failed,
                    "Não foi possível salvar: a pasta escolhida não está disponível.", "DirectoryNotFoundException");

            if (_files.FileExists(fullPath) && !overwriteConfirmed)
                return new(DiagnosticReportSaveState.OverwriteConfirmationRequired,
                    "O arquivo de destino já existe. Nada foi sobrescrito; confirme a substituição ou escolha outro nome.");

            contentBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(reviewedContent);
            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            return Failure(exception);
        }

        var temporaryMayRemain = false;
        DiagnosticReportSaveResult result;
        try
        {
            _files.WriteNew(temporaryPath, contentBytes);
            var stagedBytes = _files.ReadAllBytes(temporaryPath);
            if (!stagedBytes.AsSpan().SequenceEqual(contentBytes))
            {
                result = new(DiagnosticReportSaveState.Failed,
                    "A verificação do arquivo temporário divergiu do conteúdo revisado; o relatório não foi confirmado.",
                    "ContentMismatch");
                return CompleteWithCleanup(result, temporaryPath, ref temporaryMayRemain);
            }

            if (_files.FileExists(fullPath))
            {
                if (!overwriteConfirmed)
                {
                    result = new(DiagnosticReportSaveState.OverwriteConfirmationRequired,
                        "O arquivo de destino apareceu durante o salvamento. Nada foi sobrescrito; confirme novamente.");
                    return CompleteWithCleanup(result, temporaryPath, ref temporaryMayRemain);
                }

                _files.ReplaceExisting(temporaryPath, fullPath);
            }
            else
            {
                try
                {
                    _files.MoveNew(temporaryPath, fullPath);
                }
                catch (IOException) when (_files.FileExists(fullPath))
                {
                    result = new(DiagnosticReportSaveState.OverwriteConfirmationRequired,
                        "O arquivo de destino apareceu durante o salvamento. Nada foi sobrescrito; confirme novamente.");
                    return CompleteWithCleanup(result, temporaryPath, ref temporaryMayRemain);
                }
            }

            var finalBytes = _files.ReadAllBytes(fullPath);
            result = finalBytes.AsSpan().SequenceEqual(contentBytes)
                ? new(DiagnosticReportSaveState.SavedAndVerified,
                    "Arquivo salvo e verificado: o conteúdo corresponde exatamente à prévia revisada.")
                : new(DiagnosticReportSaveState.Failed,
                    "A verificação final divergiu do conteúdo revisado; o salvamento não foi confirmado.", "ContentMismatch");
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            result = Failure(exception);
        }

        return CompleteWithCleanup(result, temporaryPath, ref temporaryMayRemain);
    }

    private DiagnosticReportSaveResult CompleteWithCleanup(
        DiagnosticReportSaveResult result,
        string temporaryPath,
        ref bool temporaryMayRemain)
    {
        try
        {
            if (_files.FileExists(temporaryPath)) _files.Delete(temporaryPath);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            temporaryMayRemain = true;
        }

        if (!temporaryMayRemain) return result;
        var cleanupMessage = " O arquivo temporário de trabalho pode permanecer na pasta escolhida; remova-o após conferir o destino.";
        return result with
        {
            Message = result.Message + cleanupMessage,
            TemporaryFileMayRemain = true,
            State = result.State == DiagnosticReportSaveState.SavedAndVerified
                ? DiagnosticReportSaveState.Failed
                : result.State
        };
    }

    private static DiagnosticReportSaveResult Failure(Exception exception) => new(
        DiagnosticReportSaveState.Failed,
        $"Não foi possível salvar e verificar o relatório ({exception.GetType().Name}). Nenhum sucesso foi confirmado.",
        exception.GetType().Name);

    private static bool IsFileFailure(Exception exception) => exception is
        IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;
}

public sealed class PhysicalDiagnosticReportFileOperations : IDiagnosticReportFileOperations
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    public void WriteNew(string path, ReadOnlyMemory<byte> contents)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(contents.Span);
        stream.Flush(flushToDisk: true);
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public void MoveNew(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);
    public void ReplaceExisting(string sourcePath, string destinationPath) => File.Replace(sourcePath, destinationPath, null, ignoreMetadataErrors: true);
    public void Delete(string path) => File.Delete(path);
}
