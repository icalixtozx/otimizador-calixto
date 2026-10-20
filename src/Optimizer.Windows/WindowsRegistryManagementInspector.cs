using System.Management;
using System.Runtime.InteropServices;
using Optimizer.Core.Registry;

namespace Optimizer.Windows;

/// <summary>
/// Collects independent, read-only Windows management evidence for the one supported Registry
/// preference. Enrollment/join state is never treated as proof that the preference is managed.
/// </summary>
public sealed class WindowsRegistryManagementInspector : IRegistryManagementInspector
{
    public RegistryManagementAssessment Assess(RegistryPreferenceAddress address)
    {
        ValidateAddress(address);
        var evidence = new List<RegistryManagementEvidence>();
        evidence.Add(AssessDomainJoin());
        evidence.AddRange(AssessEntraJoin());
        evidence.Add(AssessMdmRegistration());
        evidence.Add(AssessRsopScope("User", @"\\.\root\RSOP\User"));
        evidence.Add(AssessRsopScope("Computer", @"\\.\root\RSOP\Computer"));
        return RegistryManagementAssessmentBuilder.FromEvidence(evidence);
    }

    private static RegistryManagementEvidence AssessDomainJoin()
    {
        IntPtr nameBuffer = IntPtr.Zero;
        try
        {
            var status = NetGetJoinInformation(null, out nameBuffer, out var joinStatus);
            if (status != 0)
            {
                var evidenceStatus = status == 5
                    ? RegistryManagementEvidenceStatus.AccessDenied
                    : RegistryManagementEvidenceStatus.Failed;
                return Evidence(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin, evidenceStatus,
                    "NetGetJoinInformation (Netapi32)", "Computador local",
                    $"Consulta falhou com código Win32 {status}; não conclui estado de ingresso nem gerenciamento da preferência.");
            }

            return joinStatus switch
            {
                NetSetupJoinStatus.DomainName => Evidence(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin,
                    RegistryManagementEvidenceStatus.Detected, "NetGetJoinInformation (Netapi32)", "Computador local",
                    "Ingresso em domínio Active Directory detectado. O nome do domínio foi omitido; isso não prova que HideFileExt esteja gerenciado."),
                NetSetupJoinStatus.WorkgroupName or NetSetupJoinStatus.Unjoined => Evidence(
                    RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin, RegistryManagementEvidenceStatus.NotDetected,
                    "NetGetJoinInformation (Netapi32)", "Computador local",
                    "A API informou grupo de trabalho ou ausência de ingresso em domínio; isso não prova ausência de gerenciamento MDM, scripts ou agentes."),
                _ => Evidence(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin,
                    RegistryManagementEvidenceStatus.Unknown, "NetGetJoinInformation (Netapi32)", "Computador local",
                    "A API retornou estado de ingresso desconhecido.")
            };
        }
        catch (DllNotFoundException)
        {
            return Unavailable(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin,
                "NetGetJoinInformation (Netapi32)", "Computador local", "Netapi32 não está disponível neste Windows.");
        }
        catch (EntryPointNotFoundException)
        {
            return Unavailable(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin,
                "NetGetJoinInformation (Netapi32)", "Computador local", "A função de consulta não está disponível neste Windows.");
        }
        catch (UnauthorizedAccessException)
        {
            return Evidence(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin,
                RegistryManagementEvidenceStatus.AccessDenied, "NetGetJoinInformation (Netapi32)", "Computador local",
                "O Windows negou acesso à consulta.");
        }
        catch (Exception exception) when (IsOperationalFailure(exception))
        {
            return Failed(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin,
                "NetGetJoinInformation (Netapi32)", "Computador local", exception);
        }
        finally
        {
            if (nameBuffer != IntPtr.Zero)
                _ = NetApiBufferFree(nameBuffer);
        }
    }

    private static IReadOnlyList<RegistryManagementEvidence> AssessEntraJoin()
    {
        IntPtr joinInfo = IntPtr.Zero;
        try
        {
            var result = NetGetAadJoinInformation(null, out joinInfo);
            if (result < 0)
            {
                var status = IsAccessDeniedHResult(result)
                    ? RegistryManagementEvidenceStatus.AccessDenied
                    : RegistryManagementEvidenceStatus.Failed;
                return [
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin, status,
                        "NetGetAadJoinInformation (Netapi32)", "Dispositivo e conta padrão do usuário atual",
                        $"Consulta falhou com HRESULT 0x{result:X8}; não conclui estado de ingresso nem gerenciamento da preferência."),
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount, status,
                        "NetGetAadJoinInformation (Netapi32)", "Conta padrão do usuário atual",
                        $"Consulta falhou com HRESULT 0x{result:X8}; contas e identificadores não foram coletados.")
                ];
            }

            if (joinInfo == IntPtr.Zero)
            {
                return [
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                        RegistryManagementEvidenceStatus.NotDetected, "NetGetAadJoinInformation (Netapi32)",
                        "Dispositivo local", "A API retornou ausência de registro Entra de dispositivo; isso não comprova ausência de gerenciamento da preferência."),
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                        RegistryManagementEvidenceStatus.NotDetected, "NetGetAadJoinInformation (Netapi32)",
                        "Contexto do usuário atual", "A API não encontrou ingresso de dispositivo nem conta de trabalho Entra padrão neste contexto.")
                ];
            }

            // DSREG_JOIN_INFO begins with the documented 32-bit DSREG_JOIN_TYPE enum. The remaining
            // structure contains tenant, device, and user identifiers and is intentionally not read.
            var joinType = (DsregJoinType)Marshal.ReadInt32(joinInfo);
            return joinType switch
            {
                DsregJoinType.DeviceJoin => [
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                        RegistryManagementEvidenceStatus.Detected, "NetGetAadJoinInformation (Netapi32)",
                        "Dispositivo local", "Ingresso de dispositivo Microsoft Entra detectado; tenant e identificadores foram omitidos."),
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                        RegistryManagementEvidenceStatus.Unknown, "NetGetAadJoinInformation (Netapi32)",
                        "Contexto do usuário atual", "A API retornou o ingresso padrão do dispositivo e não enumera todas as contas de trabalho adicionadas.")
                ],
                DsregJoinType.WorkplaceJoin => [
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                        RegistryManagementEvidenceStatus.NotDetected, "NetGetAadJoinInformation (Netapi32)",
                        "Dispositivo local", "O registro retornado é de conta de trabalho, não ingresso Entra do dispositivo."),
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                        RegistryManagementEvidenceStatus.Detected, "NetGetAadJoinInformation (Netapi32)",
                        "Contexto do usuário atual", "Conta de trabalho Microsoft Entra registrada detectada; UPN e tenant foram omitidos.")
                ],
                _ => [
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                        RegistryManagementEvidenceStatus.Unknown, "NetGetAadJoinInformation (Netapi32)",
                        "Dispositivo local", "A API retornou tipo de ingresso desconhecido."),
                    Evidence(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                        RegistryManagementEvidenceStatus.Unknown, "NetGetAadJoinInformation (Netapi32)",
                        "Contexto do usuário atual", "A API retornou tipo de ingresso desconhecido.")
                ]
            };
        }
        catch (DllNotFoundException)
        {
            return [
                Unavailable(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                    "NetGetAadJoinInformation (Netapi32)", "Dispositivo local", "Netapi32 não está disponível neste Windows."),
                Unavailable(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                    "NetGetAadJoinInformation (Netapi32)", "Contexto do usuário atual", "Netapi32 não está disponível neste Windows.")
            ];
        }
        catch (EntryPointNotFoundException)
        {
            return [
                Unavailable(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                    "NetGetAadJoinInformation (Netapi32)", "Dispositivo local", "A função não está disponível neste Windows."),
                Unavailable(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                    "NetGetAadJoinInformation (Netapi32)", "Contexto do usuário atual", "A função não está disponível neste Windows.")
            ];
        }
        catch (UnauthorizedAccessException)
        {
            return [
                Evidence(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                    RegistryManagementEvidenceStatus.AccessDenied, "NetGetAadJoinInformation (Netapi32)", "Dispositivo local", "O Windows negou acesso à consulta."),
                Evidence(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                    RegistryManagementEvidenceStatus.AccessDenied, "NetGetAadJoinInformation (Netapi32)", "Contexto do usuário atual", "O Windows negou acesso à consulta.")
            ];
        }
        catch (Exception exception) when (IsOperationalFailure(exception))
        {
            return [
                Failed(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin,
                    "NetGetAadJoinInformation (Netapi32)", "Dispositivo local", exception),
                Failed(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount,
                    "NetGetAadJoinInformation (Netapi32)", "Contexto do usuário atual", exception)
            ];
        }
        finally
        {
            if (joinInfo != IntPtr.Zero)
                NetFreeAadJoinInformation(joinInfo);
        }
    }

    private static RegistryManagementEvidence AssessMdmRegistration()
    {
        try
        {
            var result = IsDeviceRegisteredWithManagement(out var isRegistered, 0, IntPtr.Zero);
            if (result < 0)
            {
                var status = IsAccessDeniedHResult(result)
                    ? RegistryManagementEvidenceStatus.AccessDenied
                    : RegistryManagementEvidenceStatus.Failed;
                return Evidence(RegistryManagementEvidenceKind.MdmRegistration, status,
                    "IsDeviceRegisteredWithManagement (MDMRegistration)", "Registro MDM do dispositivo",
                    $"Consulta falhou com HRESULT 0x{result:X8}; não conclui estado de registro.");
            }

            return Evidence(RegistryManagementEvidenceKind.MdmRegistration,
                isRegistered ? RegistryManagementEvidenceStatus.Detected : RegistryManagementEvidenceStatus.NotDetected,
                "IsDeviceRegisteredWithManagement (MDMRegistration)", "Registro MDM do dispositivo",
                isRegistered
                    ? "O Windows informa que o dispositivo está registrado em um serviço MDM; isso não identifica se HideFileExt é gerenciado."
                    : "O Windows informa que o dispositivo não está registrado em MDM por esta interface; isso não exclui GPO, GPP, scripts ou agentes.");
        }
        catch (DllNotFoundException)
        {
            return Unavailable(RegistryManagementEvidenceKind.MdmRegistration,
                "IsDeviceRegisteredWithManagement (MDMRegistration)", "Registro MDM do dispositivo", "MDMRegistration.dll não está disponível neste Windows.");
        }
        catch (EntryPointNotFoundException)
        {
            return Unavailable(RegistryManagementEvidenceKind.MdmRegistration,
                "IsDeviceRegisteredWithManagement (MDMRegistration)", "Registro MDM do dispositivo", "A função não está disponível neste Windows.");
        }
        catch (UnauthorizedAccessException)
        {
            return Evidence(RegistryManagementEvidenceKind.MdmRegistration,
                RegistryManagementEvidenceStatus.AccessDenied, "IsDeviceRegisteredWithManagement (MDMRegistration)",
                "Registro MDM do dispositivo", "O Windows negou acesso à consulta.");
        }
        catch (Exception exception) when (IsOperationalFailure(exception))
        {
            return Failed(RegistryManagementEvidenceKind.MdmRegistration,
                "IsDeviceRegisteredWithManagement (MDMRegistration)", "Registro MDM do dispositivo", exception);
        }
    }

    private static RegistryManagementEvidence AssessRsopScope(string label, string namespacePath)
    {
        try
        {
            var scope = new ManagementScope(namespacePath);
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT registryKey, valueName, precedence, deleted FROM RSOP_RegistryPolicySetting"));
            using var rows = searcher.Get();
            var matches = new List<(uint Precedence, bool Deleted)>();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    var keyPath = row["registryKey"] as string;
                    var valueName = row["valueName"] as string;
                    if (!RegistryManagementAssessmentBuilder.IsExactHideFileExtPolicy(keyPath, valueName))
                        continue;
                    var precedence = row["precedence"] is null ? 0u : Convert.ToUInt32(row["precedence"], System.Globalization.CultureInfo.InvariantCulture);
                    var deleted = row["deleted"] is not null && Convert.ToBoolean(row["deleted"], System.Globalization.CultureInfo.InvariantCulture);
                    matches.Add((precedence, deleted));
                }
            }

            if (matches.Count == 0)
            {
                return Evidence(RegistryManagementEvidenceKind.GroupPolicyForPreference,
                    RegistryManagementEvidenceStatus.NotDetected, "WMI RSoP: RSOP_RegistryPolicySetting", label,
                    "Nenhuma entrada exata para HideFileExt foi encontrada nos dados RSoP consultáveis. Isso não prova ausência de MDM, GPP, scripts ou outros agentes.");
            }

            var effective = matches.Count(match => match.Precedence == 1 && !match.Deleted);
            var deletedCount = matches.Count(match => match.Deleted);
            var detail = effective > 0
                ? $"RSoP lista {matches.Count} entrada(s) exata(s) para HideFileExt; {effective} tem precedência vencedora. Evidência de Política de Grupo para esta preferência."
                : $"RSoP lista {matches.Count} entrada(s) exata(s) para HideFileExt, sem entrada vencedora ativa" +
                  (deletedCount > 0 ? $" ({deletedCount} marcada(s) para exclusão)" : string.Empty) +
                  ". Ainda é evidência de configuração de Política de Grupo para esta preferência.";
            return Evidence(RegistryManagementEvidenceKind.GroupPolicyForPreference,
                RegistryManagementEvidenceStatus.Detected, "WMI RSoP: RSOP_RegistryPolicySetting", label, detail);
        }
        catch (UnauthorizedAccessException)
        {
            return Evidence(RegistryManagementEvidenceKind.GroupPolicyForPreference,
                RegistryManagementEvidenceStatus.AccessDenied, "WMI RSoP: RSOP_RegistryPolicySetting", label,
                "A consulta RSoP foi negada; ausência de resultado não foi presumida.");
        }
        catch (ManagementException exception)
        {
            var status = exception.ErrorCode == ManagementStatus.AccessDenied
                ? RegistryManagementEvidenceStatus.AccessDenied
                : exception.ErrorCode is ManagementStatus.InvalidNamespace or ManagementStatus.InvalidClass or ManagementStatus.NotFound
                    ? RegistryManagementEvidenceStatus.Unavailable
                    : RegistryManagementEvidenceStatus.Failed;
            return Evidence(RegistryManagementEvidenceKind.GroupPolicyForPreference, status,
                "WMI RSoP: RSOP_RegistryPolicySetting", label,
                status switch
                {
                    RegistryManagementEvidenceStatus.AccessDenied => "A consulta RSoP foi negada; ausência de resultado não foi presumida.",
                    RegistryManagementEvidenceStatus.Unavailable => "O namespace/classe RSoP não está disponível; nenhuma conclusão sobre política foi feita.",
                    _ => $"A consulta RSoP falhou ({exception.ErrorCode}); nenhuma conclusão sobre política foi feita."
                });
        }
        catch (Exception exception) when (IsOperationalFailure(exception))
        {
            return Failed(RegistryManagementEvidenceKind.GroupPolicyForPreference,
                "WMI RSoP: RSOP_RegistryPolicySetting", label, exception);
        }
    }

    private static bool IsAccessDeniedHResult(int result) =>
        result == unchecked((int)0x80070005) || ((result >> 16) & 0x1FFF) == 7 && (result & 0xFFFF) == 5;

    private static RegistryManagementEvidence Evidence(
        RegistryManagementEvidenceKind kind,
        RegistryManagementEvidenceStatus status,
        string source,
        string scope,
        string detail) => new(kind, status, source, scope, detail);

    private static RegistryManagementEvidence Unavailable(
        RegistryManagementEvidenceKind kind, string source, string scope, string detail) =>
        Evidence(kind, RegistryManagementEvidenceStatus.Unavailable, source, scope, detail);

    private static RegistryManagementEvidence Failed(
        RegistryManagementEvidenceKind kind, string source, string scope, Exception exception) =>
        Evidence(kind, RegistryManagementEvidenceStatus.Failed, source, scope,
            $"A consulta falhou ({exception.GetType().Name}); nenhuma conclusão foi feita.");

    private static bool IsOperationalFailure(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not ArgumentException;

    private static void ValidateAddress(RegistryPreferenceAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address != RegistryPreferenceCatalog.FileExtensionsAddress)
            throw new NotSupportedException("A avaliação de gerenciamento só atende à preferência de extensões do Explorador descrita no catálogo.");
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint NetGetJoinInformation(string? server, out IntPtr nameBuffer, out NetSetupJoinStatus bufferType);

    [DllImport("Netapi32.dll", ExactSpelling = true)]
    private static extern uint NetApiBufferFree(IntPtr buffer);

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int NetGetAadJoinInformation(string? tenantId, out IntPtr joinInfo);

    [DllImport("Netapi32.dll", ExactSpelling = true)]
    private static extern void NetFreeAadJoinInformation(IntPtr joinInfo);

    [DllImport("MDMRegistration.dll", ExactSpelling = true)]
    private static extern int IsDeviceRegisteredWithManagement(
        [MarshalAs(UnmanagedType.Bool)] out bool isRegistered,
        uint characterCount,
        IntPtr upnBuffer);

    private enum NetSetupJoinStatus
    {
        UnknownStatus = 0,
        Unjoined = 1,
        WorkgroupName = 2,
        DomainName = 3
    }

    private enum DsregJoinType
    {
        UnknownJoin = 0,
        DeviceJoin = 1,
        WorkplaceJoin = 2
    }
}
