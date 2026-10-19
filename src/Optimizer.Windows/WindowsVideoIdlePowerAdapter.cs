using System.ComponentModel;
using System.Runtime.InteropServices;
using Optimizer.Core.Adjustments;

namespace Optimizer.Windows;

/// <summary>
/// Thin PowrProf adapter. ReadActiveAcTimeout is read-only; write methods are never called by application startup.
/// </summary>
public sealed class WindowsVideoIdlePowerAdapter : IVideoIdlePowerAdapter
{
    public VideoIdlePowerState ReadActiveAcTimeout()
    {
        EnsureWindows();
        var result = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var schemePointer);
        ThrowOnPowerError(result, nameof(NativeMethods.PowerGetActiveScheme));
        if (schemePointer == IntPtr.Zero)
            throw new InvalidDataException("PowerGetActiveScheme returned a null scheme pointer.");

        Guid schemeId;
        try
        {
            schemeId = Marshal.PtrToStructure<Guid>(schemePointer);
        }
        finally
        {
            _ = NativeMethods.LocalFree(schemePointer);
        }

        var acSeconds = ReadAcTimeout(schemeId);
        return new VideoIdlePowerState(schemeId, acSeconds);
    }

    public void WriteAcTimeout(Guid schemeId, uint seconds)
    {
        EnsureWindows();
        if (schemeId == Guid.Empty)
            throw new ArgumentException("Power scheme ID is required.", nameof(schemeId));

        var setting = VideoIdleSetting.VideoIdleSettingId;
        var subgroup = VideoIdleSetting.DisplaySubgroupId;
        var result = NativeMethods.PowerWriteACValueIndex(
            IntPtr.Zero,
            ref schemeId,
            ref subgroup,
            ref setting,
            seconds);
        ThrowOnPowerError(result, nameof(NativeMethods.PowerWriteACValueIndex));
    }

    public void ActivateScheme(Guid schemeId)
    {
        EnsureWindows();
        if (schemeId == Guid.Empty)
            throw new ArgumentException("Power scheme ID is required.", nameof(schemeId));

        var result = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref schemeId);
        ThrowOnPowerError(result, nameof(NativeMethods.PowerSetActiveScheme));
    }

    private static uint ReadAcTimeout(Guid schemeId)
    {
        if (schemeId == Guid.Empty)
            throw new InvalidDataException("Active power scheme ID is empty.");

        var setting = VideoIdleSetting.VideoIdleSettingId;
        var subgroup = VideoIdleSetting.DisplaySubgroupId;
        var result = NativeMethods.PowerReadACValueIndex(
            IntPtr.Zero,
            ref schemeId,
            ref subgroup,
            ref setting,
            out var value);
        ThrowOnPowerError(result, nameof(NativeMethods.PowerReadACValueIndex));
        return value;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("VIDEOIDLE power APIs require Windows desktop.");
    }

    private static void ThrowOnPowerError(uint errorCode, string api)
    {
        if (errorCode != 0)
            throw new Win32Exception(unchecked((int)errorCode), $"{api} failed with Windows error {errorCode}.");
    }

    private static class NativeMethods
    {
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerReadACValueIndex(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            ref Guid subgroupGuid,
            ref Guid powerSettingGuid,
            out uint acValueIndex);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerWriteACValueIndex(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            ref Guid subgroupGuid,
            ref Guid powerSettingGuid,
            uint acValueIndex);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

        [DllImport("Kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr LocalFree(IntPtr memory);
    }
}
