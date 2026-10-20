using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

/// <summary>
/// Documented Windows driver staging plus one-devnode association. Production composition
/// keeps both mutation paths behind the blocked policy. This type is never invoked by tests.
/// </summary>
public sealed class WindowsDirectedDriverPackageInstaller : IDriverPackageInstaller
{
    private const uint ErrorFileExists = 80;
    private const uint ErrorInsufficientBuffer = 122;
    private const uint ErrorNoMoreItems = 259;
    private const uint ErrorAccessDenied = 5;
    private const uint CrSuccess = 0;
    private const uint CmLocateDevNodeNormal = 0;
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpditCompatDriver = 0x00000002;
    private const uint DiFlagsexAllowExcludedDrivers = 0x00000800;
    private const uint SpCopyNoOverwrite = 0x00000008;
    private const uint SpostPath = 1;
    private const uint DevpropTypeString = 0x00000012;
    private const uint SpdrpHardwareId = 0x00000001;
    private const int MaxPath = 260;
    private const int MaxInfDetailBuffer = 1024 * 1024;
    private static readonly IntPtr InvalidHandle = new(-1);
    private static readonly DEVPROPKEY DriverInfPathKey = new(
        new Guid("A8B865DD-2E3D-4094-AD97-E593A70C75D6"), 5);

    public Task<DriverPackageCommandResult> StageToDriverStoreAsync(
        string infPath,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Stage(infPath, cancellationToken));

    public Task<DriverPackageCommandResult> AssociateWithDeviceAsync(
        DriverPackageAssociationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Associate(request, cancellationToken));

    private static DriverPackageCommandResult Stage(string infPath, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return new(false, null, null, "SetupAPI de drivers está disponível somente no Windows.");
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(infPath) || !File.Exists(infPath) ||
            !string.Equals(Path.GetExtension(infPath), ".inf", StringComparison.OrdinalIgnoreCase))
            return new(false, null, null, "Caminho INF ausente ou inválido.");
        if (Path.GetFullPath(infPath).Length >= MaxPath)
            return new(false, null, null, "O caminho INF excede o limite MAX_PATH dessa chamada documentada; staging bloqueado.");
        if (!IsCurrentProcessElevated())
            return new(false, 740, null, "Staging exige processo já elevado. Esta versão não abre UAC nem cria worker privilegiado.");

        var destination = new StringBuilder(MaxPath);
        var succeeded = SetupCopyOEMInfW(Path.GetFullPath(infPath), null, SpostPath, SpCopyNoOverwrite,
            destination, (uint)destination.Capacity, out _, IntPtr.Zero);
        var error = Marshal.GetLastWin32Error();
        var alreadyStaged = !succeeded && error == ErrorFileExists && destination.Length > 0;
        if (!succeeded && !alreadyStaged)
            return new(false, (int)error, error == ErrorAccessDenied ? false : null,
                $"SetupCopyOEMInf recusou staging (erro Win32 {error}). Nenhuma saída de texto localizada foi interpretada.");

        var stagedPath = destination.ToString();
        if (!IsPublishedInfPath(stagedPath) || !File.Exists(stagedPath))
            return new(false, (int)error, null, "SetupCopyOEMInf não retornou um INF publicado verificável em %WINDIR%\\INF.");
        if (!SameFileHash(infPath, stagedPath))
            return new(false, (int)error, null, "O INF publicado não corresponde byte a byte ao INF analisado; a associação fica bloqueada.");

        return new(true, succeeded ? 0 : (int)ErrorFileExists, false,
            alreadyStaged
                ? $"O pacote já estava staged sem sobrescrita; SetupCopyOEMInf identificou {Path.GetFileName(stagedPath)}."
                : $"SetupCopyOEMInf informou staging no Driver Store como {Path.GetFileName(stagedPath)}. Isso ainda não associa o dispositivo.",
            stagedPath);
    }

    private static DriverPackageCommandResult Associate(
        DriverPackageAssociationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!OperatingSystem.IsWindows()) return new(false, null, null, "DiInstallDevice está disponível somente no Windows.");
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.DeviceInstanceId) || string.IsNullOrWhiteSpace(request.ExpectedHardwareId) ||
            string.IsNullOrWhiteSpace(request.StagedInfPath) || !IsPublishedInfPath(request.StagedInfPath))
            return new(false, null, false, "Alvo ou INF publicado inválido; associação recusada.", Refusal: DriverPackageCommandRefusal.CandidateNotFoundInDriverStore);
        if (!IsCurrentProcessElevated())
            return new(false, 740, false, "Associação exige processo já elevado. Esta versão não abre UAC nem cria worker privilegiado.");

        var deviceSet = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (deviceSet == InvalidHandle)
            return Win32Failure("SetupDiGetClassDevs falhou ao enumerar dispositivos presentes.");

        var driverListBuilt = false;
        SP_DEVINFO_DATA? cleanupDevice = null;
        try
        {
            var located = FindPresentDevice(deviceSet, request.DeviceInstanceId);
            if (located.Error is not null)
                return new(false, unchecked((int)located.Error.Value), false, "O dispositivo-alvo não está presente ou não pôde ser identificado; associação recusada.", Refusal: DriverPackageCommandRefusal.DeviceNotPresent);
            var device = located.Device!.Value;
            cleanupDevice = device;
            if (!IdentityMatches(deviceSet, ref device, request, out var identityError))
                return new(false, identityError, false, "A identidade atual divergiu da análise; associação recusada.", Refusal: DriverPackageCommandRefusal.DeviceIdentityChanged);

            var installParams = new SP_DEVINSTALL_PARAMS_W { cbSize = (uint)Marshal.SizeOf<SP_DEVINSTALL_PARAMS_W>() };
            if (!SetupDiGetDeviceInstallParamsW(deviceSet, ref device, ref installParams))
                return Win32Failure("SetupDiGetDeviceInstallParams falhou.");
            // Microsoft documents this flag for including ExcludeFromSelect PnP entries
            // in the compatible-driver list. It does not force installation; the selected
            // staged INF must still be the unique best-ranked compatible candidate below.
            installParams.FlagsEx |= DiFlagsexAllowExcludedDrivers;
            if (!SetupDiSetDeviceInstallParamsW(deviceSet, ref device, ref installParams))
                return Win32Failure("SetupDiSetDeviceInstallParams falhou ao permitir enumeração de drivers PnP excluídos da seleção manual.");
            if (!SetupDiBuildDriverInfoList(deviceSet, ref device, SpditCompatDriver))
                return Win32Failure("SetupDiBuildDriverInfoList não conseguiu criar a lista de drivers compatíveis do dispositivo.");
            driverListBuilt = true;

            var enumerated = EnumerateCompatibleDrivers(deviceSet, ref device);
            if (enumerated.Error is not null)
                return new(false, unchecked((int)enumerated.Error.Value), null, "A lista compatível não pôde ser enumerada por completo; associação bloqueada.");
            var selection = DriverStoreCandidateSelector.SelectUniqueBest(
                enumerated.Drivers.Select(item => item.Candidate).ToArray(), request.StagedInfPath);
            if (selection.Status != DriverStoreCandidateSelectionStatus.Selected)
            {
                var refusal = selection.Status switch
                {
                    DriverStoreCandidateSelectionStatus.CandidateNotBestRank => DriverPackageCommandRefusal.CandidateNotBestRank,
                    DriverStoreCandidateSelectionStatus.BestRankAmbiguous => DriverPackageCommandRefusal.CandidateRankAmbiguous,
                    _ => DriverPackageCommandRefusal.CandidateNotFoundInDriverStore
                };
                return new(false, null, false, selection.Detail + " Nenhum flag de força/downgrade será usado.", Refusal: refusal);
            }

            var selectedDriver = enumerated.Drivers.Single(item =>
                string.Equals(item.Candidate.InfPath, selection.Candidate!.InfPath, StringComparison.OrdinalIgnoreCase) &&
                item.Candidate.Rank == selection.Candidate.Rank).DriverInfo;

            // Re-read exact present instance and its Hardware ID after candidate enumeration, immediately before the native write.
            if (!IdentityMatches(deviceSet, ref device, request, out identityError))
                return new(false, identityError, false, "A identidade do dispositivo mudou imediatamente antes de DiInstallDevice; associação recusada.", Refusal: DriverPackageCommandRefusal.DeviceIdentityChanged);
            if (!string.Equals(Path.GetFileName(request.StagedInfPath), Path.GetFileName(selection.Candidate!.InfPath), StringComparison.OrdinalIgnoreCase))
                return new(false, null, false, "O INF selecionado deixou de corresponder ao staging registrado.", Refusal: DriverPackageCommandRefusal.CandidateNotFoundInDriverStore);

            // Confirm that the instance is still configured in the live device tree, rather than
            // relying only on the SetupAPI snapshot taken before compatible-driver enumeration.
            var locateStatus = CM_Locate_DevNodeW(out var currentDevInst, request.DeviceInstanceId, CmLocateDevNodeNormal);
            if (locateStatus != CrSuccess)
                return new(false, unchecked((int)locateStatus), false,
                    "O dispositivo deixou de estar presente/configurado antes de DiInstallDevice; associação recusada.",
                    Refusal: DriverPackageCommandRefusal.DeviceNotPresent);
            if (currentDevInst != device.DevInst)
                return new(false, null, false,
                    "O identificador de devnode mudou desde a enumeração; associação recusada.",
                    Refusal: DriverPackageCommandRefusal.DeviceIdentityChanged);

            var installed = DiInstallDevice(IntPtr.Zero, deviceSet, ref device, ref selectedDriver, 0, out var needReboot);
            if (!installed)
            {
                var error = Marshal.GetLastWin32Error();
                return new(false, (int)error, error == ErrorAccessDenied ? false : null,
                    $"DiInstallDevice recusou a associação direcionada (erro Win32 {error}). Nenhum restart foi solicitado pelo aplicativo.");
            }
            return new(true, 0, needReboot,
                $"DiInstallDevice retornou êxito para o devnode {HashInstanceId(request.DeviceInstanceId)} e INF {Path.GetFileName(request.StagedInfPath)}. " +
                (needReboot ? "A API indicou que reinício é necessário; não foi executado." : "A API não indicou reinício."));
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            var error = exception is Win32Exception win32 ? win32.NativeErrorCode : (int?)null;
            return new(false, error, error == ErrorAccessDenied ? false : null,
                $"A associação direcionada não pôde ser preparada ({exception.GetType().Name}); nenhuma chamada adicional foi tentada.");
        }
        finally
        {
            if (driverListBuilt && cleanupDevice is { } cleanupTarget)
                SetupDiDestroyDriverInfoList(deviceSet, ref cleanupTarget, SpditCompatDriver);
            SetupDiDestroyDeviceInfoList(deviceSet);
        }
    }

    private static (SP_DEVINFO_DATA? Device, uint? Error) FindPresentDevice(IntPtr deviceSet, string expectedInstanceId)
    {
        for (uint index = 0; ; index++)
        {
            var device = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            if (!SetupDiEnumDeviceInfo(deviceSet, index, ref device))
            {
                var error = (uint)Marshal.GetLastWin32Error();
                return (null, error == ErrorNoMoreItems ? ErrorNoMoreItems : error);
            }
            var instanceId = GetDeviceInstanceId(deviceSet, ref device);
            if (string.Equals(instanceId, expectedInstanceId, StringComparison.OrdinalIgnoreCase))
                return (device, null);
        }
    }

    private static bool IdentityMatches(IntPtr deviceSet, ref SP_DEVINFO_DATA device,
        DriverPackageAssociationRequest request, out int? error)
    {
        error = null;
        var actualInstanceId = GetDeviceInstanceId(deviceSet, ref device);
        if (!string.Equals(actualInstanceId, request.DeviceInstanceId, StringComparison.OrdinalIgnoreCase)) return false;
        var hardwareIds = GetMultiStringProperty(deviceSet, ref device, SpdrpHardwareId, out var propertyError);
        if (hardwareIds is null)
        {
            error = propertyError;
            return false;
        }
        if (!hardwareIds.Contains(request.ExpectedHardwareId, StringComparer.OrdinalIgnoreCase)) return false;
        if (!TryGetDriverInfPath(deviceSet, ref device, out var currentInf, out propertyError))
        {
            error = propertyError;
            return false;
        }
        if (!string.Equals(currentInf, request.ExpectedCurrentInfName, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string GetDeviceInstanceId(IntPtr deviceSet, ref SP_DEVINFO_DATA device)
    {
        var buffer = new StringBuilder(4096);
        if (!SetupDiGetDeviceInstanceIdW(deviceSet, ref device, buffer, (uint)buffer.Capacity, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return buffer.ToString();
    }

    private static IReadOnlyList<string>? GetMultiStringProperty(
        IntPtr deviceSet, ref SP_DEVINFO_DATA device, uint property, out int? error)
    {
        error = null;
        var buffer = new byte[64 * 1024];
        if (!SetupDiGetDeviceRegistryPropertyW(deviceSet, ref device, property, out var type, buffer,
                (uint)buffer.Length, out var required))
        {
            var code = Marshal.GetLastWin32Error();
            if (code != ErrorInsufficientBuffer || required == 0 || required > 1024 * 1024)
            {
                error = (int)code;
                return null;
            }
            buffer = new byte[checked((int)required)];
            if (!SetupDiGetDeviceRegistryPropertyW(deviceSet, ref device, property, out type, buffer,
                    (uint)buffer.Length, out required))
            {
                error = Marshal.GetLastWin32Error();
                return null;
            }
        }
        if (type != 7 || required > buffer.Length)
        {
            error = 13;
            return null;
        }
        var text = Encoding.Unicode.GetString(buffer, 0, (int)required);
        return text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool TryGetDriverInfPath(
        IntPtr deviceSet, ref SP_DEVINFO_DATA device, out string infName, out int? error)
    {
        infName = string.Empty;
        error = null;
        var buffer = new byte[4096];
        var propertyKey = DriverInfPathKey;
        if (!SetupDiGetDevicePropertyW(deviceSet, ref device, ref propertyKey,
                out var type, buffer, (uint)buffer.Length, out var required, 0))
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }
        if (type != DevpropTypeString || required < 2 || required > buffer.Length)
        {
            error = 13;
            return false;
        }
        infName = Encoding.Unicode.GetString(buffer, 0, (int)required).TrimEnd('\0');
        return infName.Length > 0;
    }

    private static (IReadOnlyList<EnumeratedDriver> Drivers, uint? Error) EnumerateCompatibleDrivers(
        IntPtr deviceSet, ref SP_DEVINFO_DATA device)
    {
        var candidates = new List<EnumeratedDriver>();
        for (uint index = 0; ; index++)
        {
            var driver = new SP_DRVINFO_DATA_W { cbSize = (uint)Marshal.SizeOf<SP_DRVINFO_DATA_W>() };
            if (!SetupDiEnumDriverInfoW(deviceSet, ref device, SpditCompatDriver, index, ref driver))
            {
                var error = (uint)Marshal.GetLastWin32Error();
                return error == ErrorNoMoreItems ? (candidates, null) : (Array.Empty<EnumeratedDriver>(), error);
            }
            if (!SetupDiGetDriverInstallParamsW(deviceSet, ref device, ref driver, out var installParams))
                return (Array.Empty<EnumeratedDriver>(), (uint)Marshal.GetLastWin32Error());
            if (!TryGetDriverInfFileName(deviceSet, ref device, ref driver, out var infFileName, out var detailError))
                return (Array.Empty<EnumeratedDriver>(), detailError);
            candidates.Add(new(new(infFileName, installParams.Rank), driver));
        }
    }

    private static bool TryGetDriverInfFileName(
        IntPtr deviceSet, ref SP_DEVINFO_DATA device, ref SP_DRVINFO_DATA_W driver,
        out string infFileName, out uint? error)
    {
        infFileName = string.Empty;
        error = null;
        var size = MaxInfDetailBuffer;
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            for (var offset = 0; offset < size; offset += sizeof(long)) Marshal.WriteInt64(memory, offset, 0);
            var headerSize = (uint)Marshal.SizeOf<SP_DRVINFO_DETAIL_DATA_W>();
            Marshal.WriteInt32(memory, (int)headerSize);
            var success = SetupDiGetDriverInfoDetailW(deviceSet, ref device, ref driver, memory,
                (uint)size, out var required);
            var nativeError = Marshal.GetLastWin32Error();
            if (!success && nativeError != ErrorInsufficientBuffer)
            {
                error = (uint)nativeError;
                return false;
            }
            if (required > size)
            {
                error = ErrorInsufficientBuffer;
                return false;
            }
            var detail = Marshal.PtrToStructure<SP_DRVINFO_DETAIL_DATA_W>(memory);
            infFileName = detail.InfFileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(infFileName))
            {
                error = 13;
                return false;
            }
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static bool IsPublishedInfPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var infRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF"));
            var parent = Path.GetDirectoryName(fullPath);
            var name = Path.GetFileName(fullPath);
            return string.Equals(parent, infRoot, StringComparison.OrdinalIgnoreCase) &&
                   name.StartsWith("oem", StringComparison.OrdinalIgnoreCase) &&
                   name.EndsWith(".inf", StringComparison.OrdinalIgnoreCase) &&
                   name.AsSpan(3, name.Length - 7).Length > 0 &&
                   name.AsSpan(3, name.Length - 7).ToString().All(char.IsAsciiDigit);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SameFileHash(string sourcePath, string destinationPath)
    {
        try
        {
            using var source = File.OpenRead(sourcePath);
            using var destination = File.OpenRead(destinationPath);
            return CryptographicOperations.FixedTimeEquals(SHA256.HashData(source), SHA256.HashData(destination));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string HashInstanceId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];

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

    private static DriverPackageCommandResult Win32Failure(string action)
    {
        var error = Marshal.GetLastWin32Error();
        return new(false, error, error == ErrorAccessDenied ? false : null, $"{action} Erro Win32 {error}.");
    }

    private sealed record EnumeratedDriver(DriverStoreDriverCandidate Candidate, SP_DRVINFO_DATA_W DriverInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY(Guid fmtid, uint pid)
    {
        public Guid Fmtid = fmtid;
        public uint Pid = pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SP_DRVINFO_DATA_W
    {
        public uint cbSize;
        public uint DriverType;
        public IntPtr Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? ManufacturerName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? ProviderName;
        public System.Runtime.InteropServices.ComTypes.FILETIME DriverDate;
        public ulong DriverVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DRVINSTALL_PARAMS
    {
        public uint cbSize;
        public uint Rank;
        public uint Flags;
        public UIntPtr PrivateData;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SP_DEVINSTALL_PARAMS_W
    {
        public uint cbSize;
        public uint Flags;
        public uint FlagsEx;
        public IntPtr hwndParent;
        public IntPtr InstallMsgHandler;
        public IntPtr InstallMsgHandlerContext;
        public IntPtr FileQueue;
        public UIntPtr ClassInstallReserved;
        public uint Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string? DriverPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SP_DRVINFO_DETAIL_DATA_W
    {
        public uint cbSize;
        public System.Runtime.InteropServices.ComTypes.FILETIME InfDate;
        public uint CompatIDsOffset;
        public uint CompatIDsLength;
        public UIntPtr Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? SectionName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)] public string? InfFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string? DriverDescription;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1)] public string? HardwareId;
    }

    [DllImport("setupapi.dll", EntryPoint = "SetupCopyOEMInfW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupCopyOEMInfW(
        string sourceInfFileName,
        string? oemSourceMediaLocation,
        uint oemSourceMediaType,
        uint copyStyle,
        StringBuilder destinationInfFileName,
        uint destinationInfFileNameSize,
        out uint requiredSize,
        IntPtr destinationInfFileNameComponent);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiDestroyDeviceInfoList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiEnumDeviceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstanceIdW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        StringBuilder deviceInstanceId, uint deviceInstanceIdSize, out uint requiredSize);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryPropertyW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        uint property, out uint propertyRegDataType, [Out] byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDevicePropertyW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        ref DEVPROPKEY propertyKey, out uint propertyType, [Out] byte[] propertyBuffer, uint propertyBufferSize,
        out uint requiredSize, uint flags);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstallParamsW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstallParamsW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_DEVINSTALL_PARAMS_W deviceInstallParams);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiSetDeviceInstallParamsW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiSetDeviceInstallParamsW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_DEVINSTALL_PARAMS_W deviceInstallParams);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiBuildDriverInfoList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiBuildDriverInfoList(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint driverType);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiEnumDriverInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDriverInfoW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        uint driverType, uint memberIndex, ref SP_DRVINFO_DATA_W driverInfoData);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDriverInstallParamsW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDriverInstallParamsW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_DRVINFO_DATA_W driverInfoData, out SP_DRVINSTALL_PARAMS driverInstallParams);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDriverInfoDetailW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDriverInfoDetailW(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_DRVINFO_DATA_W driverInfoData, IntPtr driverInfoDetailData, uint driverInfoDetailDataSize, out uint requiredSize);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiDestroyDriverInfoList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDriverInfoList(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint driverType);

    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Locate_DevNodeW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint deviceInstance, string deviceInstanceId, uint flags);

    [DllImport("newdev.dll", EntryPoint = "DiInstallDevice", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DiInstallDevice(IntPtr hwndParent, IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData, ref SP_DRVINFO_DATA_W driverInfoData, uint flags,
        [MarshalAs(UnmanagedType.Bool)] out bool needReboot);
}
