# Arquitetura

## Primeira fatia

- Optimizer.Core mantém contratos e operações puras sem APIs específicas do Windows.
- Optimizer.Windows consulta WMI em uma tarefa fora da thread da interface; o escopo é somente leitura.
- Optimizer.App é WPF e abre com asInvoker, sem pedir UAC para inventário básico.
- Optimizer.Core.Tests é um harness executável sem dependências externas.

Não há worker elevado, serviço residente, servidor local, banco de dados ou rotina de inicialização. Nenhum código do app grava em Registro, políticas, rede, energia ou arquivos de drivers.

## Catálogo, perfis e simulação

Optimizer.Core/Adjustments contém definições de catálogo, avaliação de compatibilidade, geração de planos e um adaptador de simulação em memória. O catálogo de produto está vazio. A interface usa um marcador separado e explicitamente sintético; ele não é incluído no catálogo do produto.

Optimizer.Core/Profiles define políticas Leve, Equilibrado, Extremo e Personalizado. A interface permite escolher um perfil, incluir opcionalmente o marcador sintético, gerar e revisar o plano. Extremo não seleciona itens automaticamente; requer seleção explícita. Não há execução real.

O simulador não conhece APIs de configuração do Windows. Estados de falha e cancelamento são explícitos e não transformados em sucesso. A interface exibe consequências, registra consentimento individual somente para a demonstração sintética e salva um evento de histórico local após clique explícito. O histórico pode classificar retomada; isso não retoma mutações nem executa recuperação. A demonstração sintética não cria snapshots. Snapshots nos testes são sintéticos e não são backups do Windows.

`JsonOperationJournalStore` separa serialização de `IOperationJournalStorage`. O adaptador físico faz escrita temporária e substituição atômica; falha de disco, erro de escrita e cancelamento podem ser injetados nos testes. O armazenamento de demonstração fica em `%LOCALAPPDATA%\CalixtoOptimizer\History`.

## Inventário

Drivers vêm de Win32_PnPSignedDriver. Códigos de problema são lidos separadamente de Win32_PnPEntity e associados em memória por DeviceID/PNPDeviceID. Se uma consulta falha, a UI mostra erro ou aviso parcial em vez de converter a falha em lista vazia.

Hardware vem de consultas de leitura ao WMI e da chave HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion. Valores que não chegam do Windows ficam desconhecidos. O app não coleta número de série, MAC ou endereços IP na primeira versão. IDs PnP ficam somente em memória e não entram no CSV.

## Dependências e evidência técnica

- .NET 10 LTS: [ciclo de suporte oficial](https://learn.microsoft.com/dotnet/core/releases-and-support).
- Instalação e restrições por versão do Windows: [Microsoft Learn — instalar .NET no Windows](https://learn.microsoft.com/dotnet/core/install/windows).
- WMI de assinatura de drivers: [Microsoft Learn — Win32_PnPSignedDriver](https://learn.microsoft.com/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver).
- Estado Plug and Play: [Microsoft Learn — Win32_PnPEntity](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-pnpentity).
- API de consultas WMI: [Microsoft Learn — ManagementObjectSearcher](https://learn.microsoft.com/dotnet/api/system.management.managementobjectsearcher?view=net-10.0).
- Pacote fixado: [System.Management 10.0.12 no NuGet](https://www.nuget.org/packages/System.Management/10.0.12).

## Segurança e limites

Inventário não significa diagnóstico de atualização. A versão e a data brutas são preservadas sem classificar driver como antigo ou atual. Um código de problema só é mostrado quando o Windows o retorna. Não informado não quer dizer que o dispositivo foi aprovado.

O candidato real único documentado está em `docs/ADJUSTMENTS.md`; não foi implementado nem executado. Qualquer alteração real requer suporte detectado, consequência visível, consentimento vinculado ao plano, captura persistida, verificação e estratégia de reversão. O perfil Extremo não seleciona mudanças automaticamente. Operações administrativas ficam fora da versão atual.
