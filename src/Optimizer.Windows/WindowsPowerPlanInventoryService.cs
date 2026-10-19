using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

/// <summary>Read-only power scheme inventory based on PowrProf APIs.</summary>
public sealed class WindowsPowerPlanInventoryService : IPowerPlanInventoryService
{
    private const uint AccessScheme = 16;
    private const uint ErrorSuccess = 0;
    private const uint ErrorMoreData = 234;
    private const uint ErrorNoMoreItems = 259;
    private static readonly Guid VideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoIdleSetting = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

    public PowerPlanInventoryResult Collect(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return new PowerPlanInventoryResult(DateTimeOffset.UtcNow, InventoryAvailability.Failed,
                Array.Empty<PowerPlanInfo>(), Array.Empty<string>(), "A consulta de planos exige Windows.");

        var warnings = new List<string>();
        try
        {
            var activeScheme = ReadActiveScheme();
            var plans = new List<PowerPlanInfo>();
            var valueWarnings = new HashSet<uint>();

            for (uint index = 0; index < 1024; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var requiredSize = 0u;
                var result = NativeMethods.PowerEnumerate(
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, AccessScheme, index, null, ref requiredSize);

                if (result == ErrorNoMoreItems) break;
                if (result != ErrorMoreData && result != ErrorSuccess)
                    throw new Win32Exception(unchecked((int)result), $"PowerEnumerate falhou ({result}).");
                if (requiredSize < 16)
                    throw new InvalidDataException("PowerEnumerate retornou um identificador de esquema incompleto.");

                var schemeBuffer = new byte[requiredSize];
                result = NativeMethods.PowerEnumerate(
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, AccessScheme, index, schemeBuffer, ref requiredSize);
                if (result == ErrorNoMoreItems) break;
                if (result != ErrorSuccess)
                    throw new Win32Exception(unchecked((int)result), $"PowerEnumerate falhou ({result}).");

                var schemeId = new Guid(schemeBuffer.AsSpan(0, 16));
                string name;
                try
                {
                    name = ReadFriendlyName(schemeId);
                }
                catch (Win32Exception exception)
                {
                    name = "Nome não informado";
                    warnings.Add($"O nome de um plano não pôde ser lido (erro Win32 {exception.NativeErrorCode}).");
                }

                var ac = ReadValue(schemeId, isAc: true, valueWarnings);
                var dc = ReadValue(schemeId, isAc: false, valueWarnings);
                plans.Add(new PowerPlanInfo(schemeId, name, schemeId == activeScheme, ac, dc));
            }

            if (plans.Count == 1024)
                warnings.Add("A enumeração foi limitada a 1024 planos para evitar leitura sem fim.");
            foreach (var code in valueWarnings)
                warnings.Add($"O Windows não retornou um ou mais valores VIDEOIDLE (erro Win32 {code}).");

            if (plans.Count == 0)
                return new PowerPlanInventoryResult(DateTimeOffset.UtcNow, InventoryAvailability.Failed,
                    plans, warnings, "O Windows não retornou planos de energia.");

            if (!plans.Any(plan => plan.IsActive))
                warnings.Add("O esquema ativo informado pelo Windows não apareceu na lista enumerada.");

            return new PowerPlanInventoryResult(DateTimeOffset.UtcNow,
                warnings.Count == 0 ? InventoryAvailability.Available : InventoryAvailability.Partial,
                plans, warnings, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidDataException or DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return new PowerPlanInventoryResult(DateTimeOffset.UtcNow, InventoryAvailability.Failed,
                Array.Empty<PowerPlanInfo>(), warnings,
                $"Não foi possível consultar os planos de energia ({exception.GetType().Name}).");
        }
    }

    private static Guid ReadActiveScheme()
    {
        var result = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var schemePointer);
        ThrowOnPowerError(result, nameof(NativeMethods.PowerGetActiveScheme));
        if (schemePointer == IntPtr.Zero)
            throw new InvalidDataException("PowerGetActiveScheme retornou um ponteiro nulo.");

        try
        {
            return Marshal.PtrToStructure<Guid>(schemePointer);
        }
        finally
        {
            _ = NativeMethods.LocalFree(schemePointer);
        }
    }

    private static string ReadFriendlyName(Guid schemeId)
    {
        var requiredSize = 0u;
        var result = NativeMethods.PowerReadFriendlyName(
            IntPtr.Zero, ref schemeId, IntPtr.Zero, IntPtr.Zero, null, ref requiredSize);
        if (result != ErrorMoreData && result != ErrorSuccess)
            throw new Win32Exception(unchecked((int)result), $"PowerReadFriendlyName falhou ({result}).");
        if (requiredSize < 2 || requiredSize > 65536)
            throw new InvalidDataException("PowerReadFriendlyName retornou tamanho inválido.");

        var buffer = new byte[requiredSize];
        result = NativeMethods.PowerReadFriendlyName(
            IntPtr.Zero, ref schemeId, IntPtr.Zero, IntPtr.Zero, buffer, ref requiredSize);
        ThrowOnPowerError(result, nameof(NativeMethods.PowerReadFriendlyName));

        var length = buffer.Length;
        for (var index = 0; index + 1 < buffer.Length; index += 2)
        {
            if (buffer[index] == 0 && buffer[index + 1] == 0)
            {
                length = index;
                break;
            }
        }
        var value = Encoding.Unicode.GetString(buffer, 0, length).TrimEnd('\0').Trim();
        return string.IsNullOrWhiteSpace(value) ? "Nome indisponível" : value;
    }

    private static uint? ReadValue(Guid schemeId, bool isAc, ISet<uint> warnings)
    {
        var subgroup = VideoSubgroup;
        var setting = VideoIdleSetting;
        var result = isAc
            ? NativeMethods.PowerReadACValueIndex(IntPtr.Zero, ref schemeId, ref subgroup, ref setting, out var value)
            : NativeMethods.PowerReadDCValueIndex(IntPtr.Zero, ref schemeId, ref subgroup, ref setting, out value);
        if (result == ErrorSuccess) return value;
        warnings.Add(result);
        return null;
    }

    private static void ThrowOnPowerError(uint errorCode, string api)
    {
        if (errorCode != ErrorSuccess)
            throw new Win32Exception(unchecked((int)errorCode), $"{api} falhou (Win32 {errorCode}).");
    }

    private static class NativeMethods
    {
        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerEnumerate(
            IntPtr rootPowerKey, IntPtr schemeGuid, IntPtr subgroupGuid,
            uint accessFlags, uint index, [Out] byte[]? buffer, ref uint bufferSize);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerReadFriendlyName(
            IntPtr rootPowerKey, ref Guid schemeGuid, IntPtr subgroupGuid, IntPtr powerSettingGuid,
            [Out] byte[]? buffer, ref uint bufferSize);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerReadACValueIndex(
            IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid powerSettingGuid,
            out uint acValueIndex);

        [DllImport("PowrProf.dll", ExactSpelling = true)]
        internal static extern uint PowerReadDCValueIndex(
            IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid, ref Guid powerSettingGuid,
            out uint dcValueIndex);

        [DllImport("Kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr LocalFree(IntPtr memory);
    }
}
