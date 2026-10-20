using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

/// <summary>
/// Uses Windows SetupAPI and WinTrust to check an INF against its catalog and to check
/// package payload files as catalog members. It performs no catalog registration or writes.
/// </summary>
public sealed class WindowsDriverPackageSignatureVerifier : IDriverPackageSignatureVerifier
{
    private static readonly Guid DriverActionVerify = new("F750E6C3-38EE-11D1-85E5-00C04FC295EE");

    public DriverPackageSignatureResult VerifyInf(string infPath, string catalogPath)
    {
        if (!OperatingSystem.IsWindows())
            return new(DriverPackageVerificationStatus.Unavailable, null, "SetupAPI só está disponível no Windows.");

        try
        {
            var signer = new SpInfSignerInfoV2W { CbSize = (uint)Marshal.SizeOf<SpInfSignerInfoV2W>() };
            if (!SetupVerifyInfFileW(Path.GetFullPath(infPath), IntPtr.Zero, ref signer))
            {
                var error = Marshal.GetLastWin32Error();
                return new(DriverPackageVerificationStatus.Invalid, null,
                    $"SetupAPI recusou a assinatura ou associação INF/catálogo (erro Win32 {error}).");
            }

            var expectedCatalog = Path.GetFullPath(catalogPath);
            var resolvedCatalog = string.IsNullOrWhiteSpace(signer.CatalogFile) ? null : Path.GetFullPath(signer.CatalogFile);
            if (resolvedCatalog is null || !string.Equals(resolvedCatalog, expectedCatalog, StringComparison.OrdinalIgnoreCase))
                return new(DriverPackageVerificationStatus.Invalid, signer.DigitalSigner,
                    "SetupAPI encontrou um catálogo diferente do declarado no INF ou não retornou catálogo.");

            return new(DriverPackageVerificationStatus.Valid,
                string.IsNullOrWhiteSpace(signer.DigitalSigner) ? null : signer.DigitalSigner,
                "SetupAPI validou o INF por meio do catálogo declarado e retornou o signatário.");
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            return new(DriverPackageVerificationStatus.Unavailable, null,
                $"A verificação SetupAPI não pôde ser concluída ({exception.GetType().Name}).");
        }
    }

    public DriverPackageSignatureResult VerifyCatalogMember(string filePath, string catalogPath)
    {
        if (!OperatingSystem.IsWindows())
            return new(DriverPackageVerificationStatus.Unavailable, null, "WinTrust só está disponível no Windows.");
        if (!File.Exists(filePath) || !File.Exists(catalogPath))
            return new(DriverPackageVerificationStatus.Invalid, null, "O arquivo ou catálogo não existe.");

        var diagnostics = new List<string>();
        var completedTrustCheck = false;
        foreach (var algorithm in new[] { "SHA256", "SHA1" })
        {
            IntPtr catalogAdmin = IntPtr.Zero;
            try
            {
                var subsystem = DriverActionVerify;
                if (!CryptCATAdminAcquireContext2(out catalogAdmin, ref subsystem, algorithm, IntPtr.Zero, 0) || catalogAdmin == IntPtr.Zero)
                {
                    diagnostics.Add($"{algorithm}: contexto de catálogo indisponível ({Marshal.GetLastWin32Error()}).");
                    continue;
                }

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var handle = stream.SafeFileHandle;
                var hashLength = 0u;
                if (!CryptCATAdminCalcHashFromFileHandle2(catalogAdmin, handle, ref hashLength, IntPtr.Zero, 0) || hashLength is 0 or > 128)
                {
                    diagnostics.Add($"{algorithm}: o hash do arquivo não pôde ser calculado ({Marshal.GetLastWin32Error()}).");
                    continue;
                }

                var hashBuffer = Marshal.AllocHGlobal((int)hashLength);
                try
                {
                    var actualLength = hashLength;
                    if (!CryptCATAdminCalcHashFromFileHandle2(catalogAdmin, handle, ref actualLength, hashBuffer, 0) || actualLength != hashLength)
                    {
                        diagnostics.Add($"{algorithm}: falha ao calcular o hash do arquivo ({Marshal.GetLastWin32Error()}).");
                        continue;
                    }

                    var hash = new byte[actualLength];
                    Marshal.Copy(hashBuffer, hash, 0, hash.Length);
                    var result = VerifyCatalogMemberWithWinTrust(filePath, catalogPath, Convert.ToHexString(hash), hash, catalogAdmin, handle);
                    completedTrustCheck = true;
                    if (result == 0)
                        return new(DriverPackageVerificationStatus.Valid, null,
                            $"WinTrust validou a integridade de {Path.GetFileName(filePath)} como membro do catálogo ({algorithm}).");
                    diagnostics.Add($"{algorithm}: WinVerifyTrust retornou 0x{unchecked((uint)result):X8}.");
                }
                finally
                {
                    Marshal.FreeHGlobal(hashBuffer);
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or IOException or UnauthorizedAccessException or Win32Exception)
            {
                diagnostics.Add($"{algorithm}: verificação indisponível ({exception.GetType().Name}).");
            }
            finally
            {
                if (catalogAdmin != IntPtr.Zero) CryptCATAdminReleaseContext(catalogAdmin, 0);
            }
        }

        return new(completedTrustCheck ? DriverPackageVerificationStatus.Invalid : DriverPackageVerificationStatus.Unavailable, null,
            diagnostics.Count == 0 ? "O arquivo não foi validado como membro do catálogo." : string.Join(" ", diagnostics));
    }

    private static int VerifyCatalogMemberWithWinTrust(
        string memberPath,
        string catalogPath,
        string memberTag,
        byte[] calculatedHash,
        IntPtr catalogAdmin,
        SafeFileHandle memberHandle)
    {
        var catalogPathPointer = Marshal.StringToCoTaskMemUni(Path.GetFullPath(catalogPath));
        var memberTagPointer = Marshal.StringToCoTaskMemUni(memberTag);
        var memberPathPointer = Marshal.StringToCoTaskMemUni(Path.GetFullPath(memberPath));
        var hashPointer = Marshal.AllocHGlobal(calculatedHash.Length);
        var catalogInfoPointer = IntPtr.Zero;
        var trustDataPointer = IntPtr.Zero;
        try
        {
            Marshal.Copy(calculatedHash, 0, hashPointer, calculatedHash.Length);
            var catalogInfo = new WinTrustCatalogInfo
            {
                CbStruct = (uint)Marshal.SizeOf<WinTrustCatalogInfo>(),
                CatalogVersion = 0,
                CatalogFilePath = catalogPathPointer,
                MemberTag = memberTagPointer,
                MemberFilePath = memberPathPointer,
                MemberFileHandle = memberHandle.DangerousGetHandle(),
                CalculatedFileHash = hashPointer,
                CalculatedFileHashSize = (uint)calculatedHash.Length,
                CatalogContext = IntPtr.Zero,
                CatalogAdmin = catalogAdmin
            };
            catalogInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustCatalogInfo>());
            Marshal.StructureToPtr(catalogInfo, catalogInfoPointer, false);

            var trustData = new WinTrustData
            {
                CbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
                PolicyCallbackData = IntPtr.Zero,
                SipClientData = IntPtr.Zero,
                UiChoice = 2,
                RevocationChecks = 0,
                UnionChoice = 2,
                Info = catalogInfoPointer,
                StateAction = 1,
                StateData = IntPtr.Zero,
                UrlReference = IntPtr.Zero,
                ProviderFlags = 0x1000,
                UiContext = 0,
                SignatureSettings = IntPtr.Zero
            };
            trustDataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());
            Marshal.StructureToPtr(trustData, trustDataPointer, false);
            var action = DriverActionVerify;
            var result = WinVerifyTrust(new IntPtr(-1), ref action, trustDataPointer);

            var updated = Marshal.PtrToStructure<WinTrustData>(trustDataPointer);
            if (updated.StateData != IntPtr.Zero)
            {
                updated.StateAction = 2;
                Marshal.StructureToPtr(updated, trustDataPointer, false);
                _ = WinVerifyTrust(new IntPtr(-1), ref action, trustDataPointer);
            }
            return result;
        }
        finally
        {
            if (trustDataPointer != IntPtr.Zero) Marshal.FreeHGlobal(trustDataPointer);
            if (catalogInfoPointer != IntPtr.Zero) Marshal.FreeHGlobal(catalogInfoPointer);
            Marshal.FreeHGlobal(hashPointer);
            Marshal.FreeCoTaskMem(memberPathPointer);
            Marshal.FreeCoTaskMem(memberTagPointer);
            Marshal.FreeCoTaskMem(catalogPathPointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SpInfSignerInfoV2W
    {
        public uint CbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string CatalogFile;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DigitalSigner;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DigitalSignerVersion;
        public uint SignerScore;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustCatalogInfo
    {
        public uint CbStruct;
        public uint CatalogVersion;
        public IntPtr CatalogFilePath;
        public IntPtr MemberTag;
        public IntPtr MemberFilePath;
        public IntPtr MemberFileHandle;
        public IntPtr CalculatedFileHash;
        public uint CalculatedFileHashSize;
        public IntPtr CatalogContext;
        public IntPtr CatalogAdmin;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint CbStruct;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr Info;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("setupapi.dll", EntryPoint = "SetupVerifyInfFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupVerifyInfFileW(string infName, IntPtr alternatePlatform, ref SpInfSignerInfoV2W signerInfo);

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(out IntPtr catalogAdmin, ref Guid subsystem, string hashAlgorithm, IntPtr strongHashPolicy, uint flags);

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr catalogAdmin, SafeFileHandle fileHandle, ref uint hashSize, IntPtr hashBuffer, uint flags);

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr catalogAdmin, uint flags);

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid actionId, IntPtr trustData);
}
