using System.Diagnostics;
using System.IO;
using Optimizer.Core.Programs;

namespace Optimizer.Windows;

/// <summary>
/// Reads only the current user's AppX registration for the WinGet pre-indexed source.
/// This does not invoke winget.exe, update the source, or install/remove packages.
/// </summary>
public sealed class WindowsWinGetSourcePackageStatusProvider : IWinGetSourcePackageStatusProvider
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private const string ProbeScript = @"
$ErrorActionPreference = 'Stop'
try {
  $package = Get-AppxPackage -Name 'Microsoft.Winget.Source' | Select-Object -First 1
  if ($null -eq $package) {
    [Console]::Out.WriteLine('SOURCE_NOT_REGISTERED')
  } else {
    [Console]::Out.WriteLine('SOURCE_REGISTERED|' + [string]$package.Status)
  }
} catch {
  [Console]::Out.WriteLine('SOURCE_QUERY_FAILED')
  exit 1
}";

    public async Task<WinGetSourcePackageStatus> ReadCurrentProfileStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: "consulta não suportada nesta plataforma");
        }

        var powershellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(powershellPath))
        {
            return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: "Windows PowerShell indisponível");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = powershellPath,
            WorkingDirectory = Environment.SystemDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(ProbeScript);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: "consulta não iniciou");
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryStop(process);
                await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
                return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: "consulta excedeu 3 segundos");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryStop(process);
                throw;
            }

            var output = (await standardOutput.ConfigureAwait(false)).Trim();
            _ = await standardError.ConfigureAwait(false);
            if (process.ExitCode != 0 || output == "SOURCE_QUERY_FAILED")
            {
                return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: "consulta AppX falhou");
            }

            if (output == "SOURCE_NOT_REGISTERED")
            {
                return new(WinGetSourcePackageRegistration.NotRegistered);
            }

            const string registeredPrefix = "SOURCE_REGISTERED|";
            if (output.StartsWith(registeredPrefix, StringComparison.Ordinal))
            {
                var status = output[registeredPrefix.Length..].Trim();
                return string.Equals(status, "Ok", StringComparison.OrdinalIgnoreCase)
                    ? new(WinGetSourcePackageRegistration.RegisteredHealthy, status)
                    : new(WinGetSourcePackageRegistration.RegisteredUnhealthy, status);
            }

            return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: "resposta AppX não reconhecida");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            return new(WinGetSourcePackageRegistration.Unknown, Diagnostic: exception.GetType().Name);
        }
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process exited between the check and the kill.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The process may already have exited; the caller reports the timeout as inconclusive.
        }
    }
}
