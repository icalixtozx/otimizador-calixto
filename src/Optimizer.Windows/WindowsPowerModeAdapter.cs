using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Optimizer.Core.Adjustments;

namespace Optimizer.Windows;

/// <summary>Adapter for the documented Windows 11 user-configured power-mode API (PowrProf.dll).</summary>
public sealed class WindowsPowerModeAdapter : IPowerModeAdapter
{
    private const int Windows11MinimumBuild = 22000;
    private const uint ErrorSuccess = 0;

    public PowerModeReadResult ReadConfiguredModes()
    {
        if (!OperatingSystem.IsWindows())
            return Unavailable("O modo de energia configurado exige um aplicativo desktop no Windows 11.");

        var version = Environment.OSVersion.Version;
        if (version.Build < Windows11MinimumBuild)
            return Unavailable($"Requer Windows 11 (build {Windows11MinimumBuild} ou posterior); build detectada: {version.Build}.");

        if (!HasRequiredApiExports())
            return Unavailable("As funções documentadas de leitura/escrita AC e DC não estão disponíveis em PowrProf.dll.");

        Guid? acMode = null;
        Guid? dcMode = null;
        Guid? activePlan = null;
        uint? acError = null;
        uint? dcError = null;
        uint? activePlanError = null;

        try
        {
            var result = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var planPointer);
            if (result == ErrorSuccess && planPointer != IntPtr.Zero)
            {
                try { activePlan = Marshal.PtrToStructure<Guid>(planPointer); }
                finally { _ = NativeMethods.LocalFree(planPointer); }
            }
            else activePlanError = result == ErrorSuccess ? unchecked((uint)0x80004005) : result;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            activePlanError = unchecked((uint)Marshal.GetHRForException(exception));
        }

        try
        {
            var result = NativeMethods.PowerGetUserConfiguredACPowerMode(out var mode);
            if (result == ErrorSuccess) acMode = mode;
            else acError = result;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            acError = unchecked((uint)Marshal.GetHRForException(exception));
        }

        try
        {
            var result = NativeMethods.PowerGetUserConfiguredDCPowerMode(out var mode);
            if (result == ErrorSuccess) dcMode = mode;
            else dcError = result;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            dcError = unchecked((uint)Marshal.GetHRForException(exception));
        }

        return new(
            DateTimeOffset.UtcNow,
            ApiAvailable: true,
            acMode,
            dcMode,
            acError,
            dcError,
            activePlan is null
                ? "A API PowrProf de AC/DC respondeu, mas o plano ativo não pôde ser lido; aplicação fica indisponível. Preferências configuradas não comprovam o modo efetivo."
                : "A API PowrProf de modo de energia configurado está disponível. O plano ativo foi lido separadamente; preferências configuradas não comprovam o modo efetivo.",
            activePlan,
            activePlanError);
    }

    public void SetConfiguredAcMode(UserConfiguredPowerMode mode)
    {
        EnsureApiAvailable();
        var modeId = UserConfiguredPowerModes.ToId(mode);
        ThrowOnPowerError(NativeMethods.PowerSetUserConfiguredACPowerMode(ref modeId), nameof(NativeMethods.PowerSetUserConfiguredACPowerMode));
    }

    public void SetConfiguredDcMode(UserConfiguredPowerMode mode)
    {
        EnsureApiAvailable();
        var modeId = UserConfiguredPowerModes.ToId(mode);
        ThrowOnPowerError(NativeMethods.PowerSetUserConfiguredDCPowerMode(ref modeId), nameof(NativeMethods.PowerSetUserConfiguredDCPowerMode));
    }

    private static PowerModeReadResult Unavailable(string reason) => new(
        DateTimeOffset.UtcNow,
        ApiAvailable: false,
        AcModeId: null,
        DcModeId: null,
        AcErrorCode: null,
        DcErrorCode: null,
        CompatibilityReason: reason);

    private static bool HasRequiredApiExports()
    {
        var powerProfilePath = Path.Combine(Environment.SystemDirectory, "PowrProf.dll");
        if (!NativeLibrary.TryLoad(powerProfilePath, out var library)) return false;
        try
        {
            return NativeLibrary.TryGetExport(library, "PowerGetUserConfiguredACPowerMode", out _) &&
                NativeLibrary.TryGetExport(library, "PowerGetUserConfiguredDCPowerMode", out _) &&
                NativeLibrary.TryGetExport(library, "PowerGetActiveScheme", out _) &&
                NativeLibrary.TryGetExport(library, "PowerSetUserConfiguredACPowerMode", out _) &&
                NativeLibrary.TryGetExport(library, "PowerSetUserConfiguredDCPowerMode", out _);
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    private static void EnsureApiAvailable()
    {
        var state = new WindowsPowerModeAdapter().ReadConfiguredModes();
        if (!state.ApiAvailable)
            throw new PlatformNotSupportedException(state.CompatibilityReason);
    }

    private static void ThrowOnPowerError(uint errorCode, string api)
    {
        if (errorCode != ErrorSuccess)
            throw new Win32Exception(unchecked((int)errorCode), $"{api} failed with Windows error {errorCode}.");
    }

    private static class NativeMethods
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerGetUserConfiguredACPowerMode(out Guid powerModeGuid);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerGetUserConfiguredDCPowerMode(out Guid powerModeGuid);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("Kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr LocalFree(IntPtr memory);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerSetUserConfiguredACPowerMode(ref Guid powerModeGuid);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerSetUserConfiguredDCPowerMode(ref Guid powerModeGuid);
    }
}
