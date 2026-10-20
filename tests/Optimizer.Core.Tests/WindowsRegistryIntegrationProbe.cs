using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Optimizer.Core.Registry;

/// <summary>
/// Opt-in, test-project-only integration probe. It cannot target the product preference or an
/// arbitrary Registry path: the adapter is constructed with a fresh GUID and fixed ProbeValue.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsRegistryIntegrationProbe
{
    private const string ProductTestRoot = @"Software\CalixtoOptimizer\IntegrationTests";
    private const string SharedProductParent = @"Software\CalixtoOptimizer";
    private const string ValueName = "ProbeValue";

    public static int Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("SKIP: este teste explícito de integração exige Windows.");
            return 2;
        }

        var id = Guid.NewGuid();
        var childName = id.ToString("D");
        var keyPath = $@"{ProductTestRoot}\{childName}";
        var adapter = new IsolatedRegistryTestAdapter(id);
        var createdParents = new List<string>();
        var targetCreated = false;
        RegistryTypedValue? lastValueWrittenByTest = null;
        var passed = false;
        var failure = (string?)null;
        var residues = new List<string>();

        Console.WriteLine($"Teste de integração do Registro · HKCU\\{keyPath} · Registry64");
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var software = root.OpenSubKey("Software", writable: true) ??
                throw new IOException("HKCU\\Software não pôde ser aberto; nenhuma chave de teste foi criada.");

            if (CreateChildIfMissing(software, "CalixtoOptimizer"))
                createdParents.Add(SharedProductParent);
            using var product = software.OpenSubKey("CalixtoOptimizer", writable: true) ??
                throw new IOException("A chave pai CalixtoOptimizer não pôde ser aberta.");

            if (CreateChildIfMissing(product, "IntegrationTests"))
                createdParents.Add(ProductTestRoot);
            using var testsRoot = product.OpenSubKey("IntegrationTests", writable: true) ??
                throw new IOException("A chave pai IntegrationTests não pôde ser aberta.");

            using (var existing = testsRoot.OpenSubKey(childName, writable: false))
            {
                if (existing is not null)
                    throw new InvalidOperationException("GUID já existente; o teste recusou tocar em conteúdo preexistente.");
            }

            if (!CreateChildIfMissing(testsRoot, childName))
                throw new InvalidOperationException("A criação atômica encontrou uma chave GUID preexistente; nenhum valor foi escrito.");
            targetCreated = true;

            using (var freshTarget = testsRoot.OpenSubKey(childName, writable: false) ??
                throw new IOException("A chave GUID recém-criada não pôde ser relida."))
            {
                if (freshTarget.GetValueNames().Length != 0 || freshTarget.GetSubKeyNames().Length != 0)
                    throw new InvalidDataException("A chave GUID recém-criada contém conteúdo inesperado; o teste parou sem escrever valor.");
            }
            Console.WriteLine("PASS: chave GUID nova criada e confirmada vazia.");

            var before = adapter.Read(adapter.Address);
            Require(before.KeyExists && !before.ValueExists, "A leitura inicial não confirmou a chave vazia.");
            Console.WriteLine("PASS: adaptador leu a chave sem ProbeValue.");

            var zero = RegistryTypedValue.DWord(0);
            adapter.WriteValue(adapter.Address, zero);
            lastValueWrittenByTest = zero;
            var readZero = adapter.Read(adapter.Address);
            Require(readZero.ValueExists && readZero.Value == zero, "REG_DWORD 0 não foi confirmado pelo adaptador.");
            Console.WriteLine("PASS: ProbeValue criado como REG_DWORD 0 e relido.");

            var one = RegistryTypedValue.DWord(1);
            adapter.WriteValue(adapter.Address, one);
            lastValueWrittenByTest = one;
            var readOne = adapter.Read(adapter.Address);
            Require(readOne.ValueExists && readOne.Value == one, "REG_DWORD 1 não foi confirmado pelo adaptador.");
            Console.WriteLine("PASS: ProbeValue alterado para REG_DWORD 1 e relido.");

            adapter.DeleteValue(adapter.Address);
            lastValueWrittenByTest = null;
            var afterDelete = adapter.Read(adapter.Address);
            Require(afterDelete.KeyExists && !afterDelete.ValueExists, "O adaptador não confirmou a remoção somente do valor.");
            Console.WriteLine("PASS: somente ProbeValue removido; chave GUID continua vazia.");
            passed = true;
        }
        catch (Exception exception)
        {
            failure = $"{exception.GetType().Name}: {exception.Message}";
            Console.Error.WriteLine($"FAIL: {failure}");
        }
        finally
        {
            Cleanup(adapter, id, targetCreated, lastValueWrittenByTest, createdParents, residues);
        }

        if (failure is not null)
            Console.Error.WriteLine($"Falha registrada: {failure}");
        foreach (var residue in residues)
            Console.Error.WriteLine($"RESÍDUO: {residue}");
        if (passed && residues.Count == 0)
            Console.WriteLine("RESULTADO: integração concluída; valor e chaves criados pelo teste foram removidos e conferidos.");
        else if (residues.Count == 0)
            Console.WriteLine("RESULTADO: teste falhou; limpeza dos elementos do teste foi confirmada.");
        else
            Console.Error.WriteLine("RESULTADO: teste incompleto; os resíduos acima precisam de revisão manual.");

        return passed && residues.Count == 0 ? 0 : 1;
    }

    private static void Cleanup(
        IsolatedRegistryTestAdapter adapter,
        Guid id,
        bool targetCreated,
        RegistryTypedValue? lastValueWrittenByTest,
        IReadOnlyList<string> createdParents,
        List<string> residues)
    {
        var childName = id.ToString("D");
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var software = root.OpenSubKey("Software", writable: true);
            if (software is null)
            {
                if (targetCreated || createdParents.Count > 0)
                    residues.Add(@"HKCU\Software desapareceu antes da limpeza; confira manualmente o GUID " + childName + ".");
                return;
            }

            using var product = software.OpenSubKey("CalixtoOptimizer", writable: true);
            if (product is null)
            {
                if (targetCreated || createdParents.Count > 0)
                    residues.Add(@"HKCU\Software\CalixtoOptimizer desapareceu antes da limpeza; confira manualmente o GUID " + childName + ".");
                return;
            }

            using var testsRoot = product.OpenSubKey("IntegrationTests", writable: true);
            if (testsRoot is not null)
            {
                if (targetCreated)
                {
                    using (var target = testsRoot.OpenSubKey(childName, writable: true))
                    {
                        if (target is not null)
                        {
                            var names = target.GetValueNames();
                            var subKeys = target.GetSubKeyNames();
                            var unexpectedNames = names.Where(name => !string.Equals(name, ValueName, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (unexpectedNames.Length > 0 || subKeys.Length > 0)
                            {
                                residues.Add($@"HKCU\{ProductTestRoot}\{childName} contém valores ou subchaves inesperados; nada além do ProbeValue conhecido foi removido.");
                            }
                            else if (names.Length == 1)
                            {
                                var current = adapter.Read(adapter.Address);
                                if (lastValueWrittenByTest is not null && current.Value == lastValueWrittenByTest)
                                {
                                    adapter.DeleteValue(adapter.Address);
                                    Console.WriteLine("LIMPEZA: ProbeValue removido após confirmar que ainda corresponde ao último dado escrito pelo teste.");
                                }
                                else
                                {
                                    residues.Add($@"HKCU\{ProductTestRoot}\{childName}\{ValueName} mudou inesperadamente; foi preservado.");
                                }
                            }
                        }
                    }

                    using var emptyCheck = testsRoot.OpenSubKey(childName, writable: false);
                    if (emptyCheck is not null && emptyCheck.GetValueNames().Length == 0 && emptyCheck.GetSubKeyNames().Length == 0)
                    {
                        testsRoot.DeleteSubKey(childName, throwOnMissingSubKey: true);
                        Console.WriteLine("LIMPEZA: chave GUID vazia removida.");
                    }
                    else if (emptyCheck is not null && !residues.Any(item => item.Contains(childName, StringComparison.OrdinalIgnoreCase)))
                    {
                        residues.Add($@"HKCU\{ProductTestRoot}\{childName} não está vazia; a chave foi preservada.");
                    }
                }
            }

            foreach (var parentPath in createdParents.Reverse())
            {
                var parentLeaf = parentPath[(parentPath.LastIndexOf('\\') + 1)..];
                var parentParentPath = parentPath[..parentPath.LastIndexOf('\\')];
                using var parentParent = root.OpenSubKey(parentParentPath, writable: true);
                using var ownedParent = parentParent?.OpenSubKey(parentLeaf, writable: true);
                if (ownedParent is null) continue;
                if (ownedParent.GetValueNames().Length == 0 && ownedParent.GetSubKeyNames().Length == 0)
                {
                    parentParent!.DeleteSubKey(parentLeaf, throwOnMissingSubKey: true);
                    Console.WriteLine($"LIMPEZA: chave pai criada pelo teste e vazia removida ({parentPath}).");
                }
                else
                {
                    residues.Add($@"HKCU\{parentPath} foi criada pelo teste, mas não está vazia; foi preservada.");
                }
            }

            using var finalTestsRoot = root.OpenSubKey(ProductTestRoot, writable: false);
            using var finalTarget = finalTestsRoot?.OpenSubKey(childName, writable: false);
            if (finalTarget is not null && !residues.Any(item => item.Contains(childName, StringComparison.OrdinalIgnoreCase)))
                residues.Add($@"HKCU\{ProductTestRoot}\{childName} ainda existe após a limpeza.");
        }
        catch (Exception exception)
        {
            residues.Add($"Falha durante limpeza ({exception.GetType().Name}: {exception.Message}); confira o GUID {childName} em HKCU\\{ProductTestRoot}.");
        }
    }

    private static bool CreateChildIfMissing(RegistryKey parent, string childName)
    {
        using var existing = parent.OpenSubKey(childName, writable: false);
        if (existing is not null) return false;

        var status = RegCreateKeyExW(
            parent.Handle,
            childName,
            0,
            null,
            0,
            KeyRead | KeyWrite | KeyWow64_64Key,
            IntPtr.Zero,
            out var createdKey,
            out var disposition);
        using (createdKey)
        {
            if (status != 0) throw new Win32Exception(status, "RegCreateKeyExW falhou.");
        }
        return disposition == RegCreatedNewKey;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private const int KeyRead = 0x20019;
    private const int KeyWrite = 0x20006;
    private const int KeyWow64_64Key = 0x0100;
    private const uint RegCreatedNewKey = 1;

    [DllImport("advapi32.dll", EntryPoint = "RegCreateKeyExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegCreateKeyExW(
        SafeRegistryHandle hKey,
        string subKey,
        uint reserved,
        string? keyClass,
        uint options,
        int desiredAccess,
        IntPtr securityAttributes,
        out SafeRegistryHandle result,
        out uint disposition);

    private sealed class IsolatedRegistryTestAdapter(Guid id) : IRegistryPreferenceAdapter
    {
        public RegistryPreferenceAddress Address { get; } = new(
            RegistryPreferenceHive.CurrentUser,
            $@"{ProductTestRoot}\{id:D}",
            ValueName,
            RegistryPreferenceView.Registry64);

        public RegistryValueState Read(RegistryPreferenceAddress address)
        {
            Validate(address);
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = root.OpenSubKey(Address.KeyPath, writable: false);
            if (key is null) return RegistryValueState.MissingKey;

            var names = key.GetValueNames();
            if (key.GetSubKeyNames().Length != 0 || names.Any(name => !string.Equals(name, ValueName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("A chave isolada contém conteúdo inesperado; o adaptador de teste parou.");
            if (names.Length == 0) return RegistryValueState.MissingValue;
            if (names.Length != 1 || key.GetValueKind(ValueName) != RegistryValueKind.DWord)
                throw new InvalidDataException("ProbeValue não é o único valor REG_DWORD esperado.");
            if (key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not int raw)
                throw new InvalidDataException("ProbeValue não pôde ser lido como DWORD.");
            return new RegistryValueState(true, true, RegistryTypedValue.DWord(unchecked((uint)raw)));
        }

        public void WriteValue(RegistryPreferenceAddress address, RegistryTypedValue value)
        {
            Validate(address);
            if (value is null || value.Kind != RegistryStoredValueKind.DWord ||
                (value != RegistryTypedValue.DWord(0) && value != RegistryTypedValue.DWord(1)))
                throw new NotSupportedException("O adaptador de integração aceita somente DWORD 0 ou 1 para ProbeValue.");
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = root.OpenSubKey(Address.KeyPath, writable: true) ??
                throw new IOException("A chave GUID de teste não existe; o adaptador nunca a cria ao escrever.");
            key.SetValue(ValueName, unchecked((int)uint.Parse(value.Data, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture)), RegistryValueKind.DWord);
        }

        public void DeleteValue(RegistryPreferenceAddress address)
        {
            Validate(address);
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using var key = root.OpenSubKey(Address.KeyPath, writable: true) ??
                throw new IOException("A chave GUID de teste não existe.");
            key.DeleteValue(ValueName, throwOnMissingValue: true);
        }

        private void Validate(RegistryPreferenceAddress address)
        {
            if (address != Address)
                throw new NotSupportedException("Este adaptador de integração permite somente ProbeValue na chave GUID HKCU de teste.");
        }
    }
}
