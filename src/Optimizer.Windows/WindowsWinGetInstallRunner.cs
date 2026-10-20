using System.ComponentModel;
using System.Diagnostics;
using Optimizer.Core.Programs;

namespace Optimizer.Windows;

/// <summary>
/// Invokes only an exact package selected from the compiled-in catalog. Production composition
/// supplies a blocked execution policy; this runner is not exposed to arbitrary command input.
/// </summary>
public sealed class WindowsWinGetInstallRunner : IProgramInstallRunner
{
    public async Task<ProgramRunnerResult> InstallAsync(
        ProgramPackageDefinition package,
        ProgramInstallConsent consent,
        CancellationToken cancellationToken)
    {
        var catalog = ProgramCatalog.CreateDefault();
        if (!catalog.TryGet(package.PackageId, out _)
            || !string.Equals(package.PackageId, consent.PackageId, StringComparison.OrdinalIgnoreCase))
        {
            return new(ProgramRunnerOutcome.Failed);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "winget.exe",
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Never accept arbitrary commands, package IDs, sources or switches from the UI.
        // Scope is left to the selected package's WinGet manifest/installer.
        try
        {
            foreach (var argument in ProgramInstallCommandFactory.BuildArguments(catalog, consent))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }
        catch (InvalidOperationException)
        {
            return new(ProgramRunnerOutcome.Failed);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start()) return new(ProgramRunnerOutcome.ManagerUnavailable);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return new(ProgramRunnerOutcome.ManagerUnavailable);
        }
        catch (Win32Exception)
        {
            return new(ProgramRunnerOutcome.Failed);
        }
        catch (InvalidOperationException)
        {
            return new(ProgramRunnerOutcome.Failed);
        }

        using var cancellationRegistration = cancellationToken.Register(() => TryTerminate(process));
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (InvalidOperationException) { }
            return new(ProgramRunnerOutcome.Cancelled, TryReadExitCode(process));
        }

        return new(ProgramRunnerOutcome.Completed, TryReadExitCode(process));
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static int? TryReadExitCode(Process process)
    {
        try { return process.ExitCode; }
        catch (InvalidOperationException) { return null; }
    }
}
