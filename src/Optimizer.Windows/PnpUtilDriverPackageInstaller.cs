using System.Diagnostics;
using System.Security.Principal;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

/// <summary>
/// Documented PnPUtil backend. It never invokes an OEM setup program, deletes packages,
/// requests forced ranking, or reboots. Production composition keeps the caller policy blocked.
/// </summary>
public sealed class PnpUtilDriverPackageInstaller : IDriverPackageInstaller
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    public Task<DriverPackageCommandResult> StageToDriverStoreAsync(
        string infPath,
        CancellationToken cancellationToken = default) => RunAsync(infPath, installOnMatches: false, cancellationToken);

    public Task<DriverPackageCommandResult> InstallOnMatchingDeviceAsync(
        string infPath,
        CancellationToken cancellationToken = default) => RunAsync(infPath, installOnMatches: true, cancellationToken);

    private static async Task<DriverPackageCommandResult> RunAsync(
        string infPath,
        bool installOnMatches,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return new(false, null, null, "PnPUtil está disponível somente no Windows.");
        if (string.IsNullOrWhiteSpace(infPath) || !File.Exists(infPath) ||
            !string.Equals(Path.GetExtension(infPath), ".inf", StringComparison.OrdinalIgnoreCase))
            return new(false, null, null, "Caminho INF ausente ou inválido.");
        if (!IsCurrentProcessElevated())
            return new(false, 740, null,
                "A etapa exige processo já elevado. Esta versão não abre UAC nem cria worker privilegiado; não foi iniciada chamada ao sistema.");

        var pnputil = Path.Combine(Environment.SystemDirectory, "pnputil.exe");
        if (!File.Exists(pnputil))
            return new(false, null, null, "pnputil.exe não foi encontrado em System32.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pnputil,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(infPath))!
            }
        };
        process.StartInfo.ArgumentList.Add("/add-driver");
        process.StartInfo.ArgumentList.Add(Path.GetFullPath(infPath));
        if (installOnMatches) process.StartInfo.ArgumentList.Add("/install");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!process.Start()) return new(false, null, null, "PnPUtil não iniciou.");
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr);

            var code = process.ExitCode;
            if (code != 0)
                return new(false, code, null,
                    $"PnPUtil terminou com código {code}. A saída do comando não foi interpretada por texto localizado; consulte SetupAPI.dev.log para diagnóstico manual.");

            return new(true, code, null, installOnMatches
                ? "PnPUtil /add-driver /install retornou código 0. O comando não força downgrade; releia o dispositivo para confirmar INF ativo. Não foi solicitado reinício automático."
                : "PnPUtil /add-driver retornou código 0. O pacote foi reportado como adicionado ao Driver Store; isso não comprova associação ao dispositivo.");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new(false, null, null,
                $"A chamada PnPUtil não pôde ser concluída ({exception.GetType().Name}); confira o histórico e SetupAPI.dev.log.");
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Security.SecurityException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
