# Arquitetura

## Aplicação e privilégios

- `Optimizer.Core` mantém modelos e fluxos puros, incluindo planos sintéticos e o serviço de operação VIDEOIDLE.
- `Optimizer.Windows` consulta WMI e implementa o adaptador de energia Win32 PowrProf. A classe tem métodos de leitura e escrita, mas criar o adaptador não chama as APIs.
- `Optimizer.App` é WPF com manifesto `asInvoker`, inicia em diagnóstico. A leitura/captura VIDEOIDLE só ocorre após clique explícito; a UI não expõe aplicação nem reversão.
- `BlockedVideoIdleMutationPolicy` é o default de escrita e sempre nega. Não há serviço, worker elevado, tarefa privilegiada ou daemon.
- `Optimizer.Core.Tests` executa cenários com adaptador/storage falsos; o harness não chama PowrProf.

## Simulação e operação real

A fixture sintética permanece em catálogo separado e só grava um resultado explicitamente rotulado como demonstração. Ela não cria capturas técnicas.

VIDEOIDLE usa outro fluxo e outro subdiretório de histórico. A prévia consulta o estado real sem alteração. A captura persiste o esquema ativo e o índice AC anterior, sem representar backup do Windows. O serviço de aplicação exige política habilitada e confirmação específica. Mesmo assim, o app atual injeta política bloqueada e não apresenta comando de escrita.

A aplicação persiste o ponto de recuperação antes da chamada nativa, verifica conflito antes da escrita, muda somente AC, solicita ativação do mesmo plano e verifica por leitura posterior. Reversão exige estado atual igual ao valor proposto e restaura o valor AC original. Mudança concorrente após a última leitura ainda é uma limitação porque as APIs não oferecem transação/CAS. Qualquer estado ambíguo exige revisão manual.

O histórico usa `%LOCALAPPDATA%\CalixtoOptimizer\History`; capturas VIDEOIDLE ficam em `RealVideoIdle`, separado de `JsonOperationJournalStore` da demonstração. Fingerprint detecta corrupção/divergência acidental, não dá autenticidade contra o próprio usuário que controla a pasta. Nenhum worker privilegiado pode confiar nessa pasta como canal de comando.

## Inventário

Drivers vêm de Win32_PnPSignedDriver. Códigos de problema são lidos separadamente de Win32_PnPEntity e associados em memória por DeviceID/PNPDeviceID. Se uma consulta falha, a UI mostra erro ou aviso parcial em vez de converter a falha em lista vazia.

Hardware vem de consultas de leitura ao WMI e da chave HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion. Valores que não chegam do Windows ficam desconhecidos. O app não coleta número de série, MAC ou endereços IP na primeira versão. IDs PnP ficam somente em memória e não entram no CSV.

## Dependências e evidência técnica

- .NET 10: [ciclo oficial de suporte](https://learn.microsoft.com/dotnet/core/releases-and-support).
- WMI de assinatura: [Win32_PnPSignedDriver](https://learn.microsoft.com/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver).
- Estado Plug and Play: [Win32_PnPEntity](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-pnpentity).
- Consulta WMI: [ManagementObjectSearcher](https://learn.microsoft.com/dotnet/api/system.management.managementobjectsearcher?view=net-10.0).
- Pacote: [System.Management 10.0.12](https://www.nuget.org/packages/System.Management/10.0.12).
- GUID/unidade de VIDEOIDLE: [Microsoft Learn](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/display-settings-display-idle-timeout).
- API de energia: [PowerGetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme), [PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex), [PowerWriteACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwriteacvalueindex), [PowerSetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme).
