# Arquitetura

## Aplicação e privilégios

- `Optimizer.Core` mantém modelos e fluxos puros, incluindo planos sintéticos, VIDEOIDLE e operações AC/DC do modo de energia configurado.
- `Optimizer.Windows` consulta WMI e implementa adaptadores Win32 PowrProf. Criar os adaptadores não chama setters.
- `Optimizer.Windows` lê as interfaces de rede por `NetworkInterface.GetAllNetworkInterfaces`; resolução DNS e ICMP ficam em métodos assíncronos e são chamados somente pelo botão de teste.
- `Optimizer.App` é WPF com manifesto `asInvoker`, inicia em diagnóstico. Leitura/captura real de VIDEOIDLE e modo de energia só ocorre após clique explícito. A UI do modo de energia tem comandos de aplicação/restauração que começam desabilitados e exigem consentimento, autorização separada e frase exata.
- As políticas de mutação real negam por padrão. Não há serviço, worker elevado, tarefa privilegiada ou daemon.
- `Optimizer.Core.Tests` executa cenários com adaptador/storage falsos; o harness não chama PowrProf.

## Simulação e operação real

A fixture sintética permanece em catálogo separado e só grava um resultado explicitamente rotulado como demonstração. Ela não cria capturas técnicas.

VIDEOIDLE usa outro fluxo e outro subdiretório de histórico. A prévia consulta o estado real sem alteração. A captura persiste o esquema ativo e o índice AC anterior, sem representar backup do Windows. O serviço de aplicação exige política habilitada e confirmação específica. Mesmo assim, o app atual injeta política bloqueada e não apresenta comando de escrita.

O modo de energia usa as funções `PowerGet/SetUserConfigured{AC,DC}PowerMode`, em outro serviço e histórico `RealPowerMode`. Uma captura versão 2 registra AC, DC e o GUID do plano ativo, mesmo para alvo de um lado. Esse estado é a preferência configurada pelo usuário, não leitura da preferência efetiva; o Windows pode sobrepor o sinal. Somente Windows 11 desktop com a API documentada e estado AC/DC/plano legível pode ser revisado. A UI separa revisão da consequência da autorização e exige confirmação específica; o serviço padrão nega setters.

Antes de aplicação, a UI e o serviço relêem AC, DC e plano ativo; qualquer divergência da captura invalida a revisão. A intenção é persistida antes da chamada nativa. O serviço verifica estado completo antes e depois de cada setter, incluindo preservação do lado não selecionado e plano ativo. A restauração exige uma autorização separada, compara o estado por etapas e recusa valores de terceiros. Mudança concorrente após a última leitura ainda é uma limitação porque as APIs não oferecem transação/CAS. Qualquer estado ambíguo exige revisão manual.

Nesta etapa, uma captura real v2 foi persistida e reaberta em processo separado; esquema, valores, GUID do plano e fingerprint foram validados. Nenhum setter foi chamado. A validação visual do painel de modo de energia ficou incompleta porque a automação detectou entrada do usuário antes de concluir a navegação; a correção XAML identificada foi compilada, mas exige revisão visual posterior.

O histórico usa `%LOCALAPPDATA%\CalixtoOptimizer\History`; capturas VIDEOIDLE ficam em `RealVideoIdle` e modo de energia em `RealPowerMode`, separados de `JsonOperationJournalStore` da demonstração. Fingerprint detecta corrupção/divergência acidental, não dá autenticidade contra o próprio usuário que controla a pasta. Nenhum worker privilegiado pode confiar nessa pasta como canal de comando.

## Inventário

Drivers vêm de Win32_PnPSignedDriver. Códigos de problema são lidos separadamente de Win32_PnPEntity e associados em memória por DeviceID/PNPDeviceID. Se uma consulta falha, a UI mostra erro ou aviso parcial em vez de converter a falha em lista vazia.

Hardware vem de consultas de leitura ao WMI e da chave HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion. Valores que não chegam do Windows ficam desconhecidos. O app não coleta número de série, MAC ou endereços IP na primeira versão. IDs PnP ficam somente em memória e não entram no CSV.

## Diagnóstico de rede

A página Rede mostra nomes/descritivos locais de interface, estado, endereços, gateways, DNS e velocidade de enlace reportada pelo adaptador. Essa velocidade não é velocidade contratada ou medida da internet. A enumeração .NET pode incluir interfaces virtuais, túneis e loopback; a interface informa esse escopo em vez de tratar cada linha como conexão física.

Carregar/atualizar adaptadores é leitura local e não envia sondagens. Um teste só começa pelo botão explícito, apresenta o host/IP e a finalidade antes do clique, tenta resolver nome se necessário e então envia até quatro pacotes ICMP pequenos, sequenciais, para um IP resolvido mostrado no resultado. O timeout é de 1.000 ms por amostra; o resultado mostra amostras concluídas/solicitadas, respostas, perda entre as concluídas, RTT e duração total. O cancelamento conserva resultados parciais. Não existe teste de download.

Falha do inventário local, ausência de interface operacional com endereço não loopback, falha DNS, timeouts sem resposta, outros erros ICMP, resposta parcial e cancelamento são resultados distintos. Ausência de resposta ICMP pode decorrer de bloqueio no host/rota/regras e não prova falta de internet. Nenhum código altera DNS, interface, firewall, Registro, políticas ou configurações de rede. Esses dados não são incluídos na exportação diagnóstica sanitizada.

## Inventário e preferência gráfica

O inventário gráfico reutiliza `WmiComputerInventoryService` e sua consulta existente a `Win32_VideoController.Name`. O resultado é descrito como controlador reportado pelo Windows; não demonstra que o adaptador está conduzindo a tela, nem qual dispositivo está renderizando uma aplicação. O provedor WMI pode reportar valores imprecisos em hardware/driver fora do WDDM.

Preferência por aplicativo e uso corrente são estados diferentes. O Windows documenta a página de Configurações `ms-settings:display-advancedgraphics` para consultar e configurar a preferência por aplicativo em dispositivos compatíveis. O app oferece essa página por clique e não grava tal preferência. Não foi validada uma API pública e estável para ler no app a preferência persistida de outros executáveis. A API DXGI `IDXGIFactory6::EnumAdapterByGpuPreference` ordena os adaptadores para a preferência escolhida pelo próprio app que cria o dispositivo Direct3D; ela não lê a configuração de Preferência gráfica de outro aplicativo.

Para observar qual motor gráfico está sendo usado por processo, o app oferece abrir o Gerenciador de Tarefas. A coluna **GPU Engine** é uma observação corrente, dependente de atividade, versão do Windows e driver; não é a preferência configurada nem uma promessa de uso constante. Este produto não coleta telemetria GPU programaticamente e não altera escolha gráfica. Não acessa chaves privadas do Registro.

## Dependências e evidência técnica

- .NET 10: [ciclo oficial de suporte](https://learn.microsoft.com/dotnet/core/releases-and-support).
- WMI de assinatura: [Win32_PnPSignedDriver](https://learn.microsoft.com/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver).
- Interfaces locais: [NetworkInterface.GetAllNetworkInterfaces](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface.getallnetworkinterfaces).
- Resolução: [Dns.GetHostAddressesAsync com CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.net.dns.gethostaddressesasync?view=net-10.0).
- ICMP assíncrono com timeout e cancelamento: [Ping.SendPingAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0).
- Inventário de controladores reportados: [Win32_VideoController](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller).
- Abrir a preferência gráfica nativa: [URI `ms-settings:display-advancedgraphics`](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings).
- Configuração por aplicativo e necessidade de reiniciar o app após salvar: [Microsoft Support — Graphics settings in Windows 11](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11).
- Ordenação DXGI escolhida pelo próprio aplicativo: [IDXGIFactory6::EnumAdapterByGpuPreference](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/nf-dxgi1_6-idxgifactory6-enumadapterbygpupreference).
- Verificação nativa do mecanismo por aplicativo: [Surface GPU overview — Task Manager GPU Engine](https://learn.microsoft.com/en-us/surface/surface-book-gpu-overview).
- Estado Plug and Play: [Win32_PnPEntity](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-pnpentity).
- Consulta WMI: [ManagementObjectSearcher](https://learn.microsoft.com/dotnet/api/system.management.managementobjectsearcher?view=net-10.0).
- Pacote: [System.Management 10.0.12](https://www.nuget.org/packages/System.Management/10.0.12).
- GUID/unidade de VIDEOIDLE: [Microsoft Learn](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/display-settings-display-idle-timeout).
- API de energia: [PowerGetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme), [PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex), [PowerWriteACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwriteacvalueindex), [PowerSetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme).
