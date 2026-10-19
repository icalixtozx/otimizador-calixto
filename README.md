# Calixto Optimizer

Aplicativo experimental local para diagnóstico de hardware, dispositivos, drivers e energia no Windows. Inicia somente em diagnóstico e não aplica alterações automaticamente.

## Funcionalidades disponíveis

- Lista os planos de energia através de APIs PowrProf, marca o ativo e mostra valores VIDEOIDLE AC/DC em segundos.
- No código de desenvolvimento 0.6.0, consulta por clique a preferência de modo de energia do Windows 11 em AC e DC separadamente e permite salvar uma captura local antes de qualquer execução.
- Lê inventário local de dispositivos PnP e metadados de driver reportados pelo Windows: fornecedor, versão, data, INF, assinatura e código PnP.
- Um código PnP é mostrado como dado reportado pelo Windows; o app não o converte automaticamente em “driver ausente”. Uma linha sem metadados de driver também não prova ausência de driver.
- Não compara versões com catálogo externo e não classifica drivers como desatualizados.
- Pré-visualiza um relatório JSON sanitizado antes de oferecer a gravação escolhida pelo usuário. O relatório não é enviado.
- Interface em português, alto contraste preto e branco.
- A demonstração sintética é separada e desligada por padrão.

## Estado do VIDEOIDLE

VIDEOIDLE é um teste técnico do motor, não uma otimização de desempenho. Prévia real e captura persistida são acionadas explicitamente. Aplicação e reversão do índice AC estão bloqueadas no app por padrão e não têm ação exposta na interface. Não há leitura ou escrita automática ao iniciar.

A captura técnica não inclui bateria/DC, não é backup do Windows e fica separada da demonstração. Nenhum ajuste, driver, Registro, política, serviço, rede, prioridade ou firmware foi alterado para desenvolver ou gerar este pacote.

## Modo de energia do usuário

As APIs documentadas `PowerGet/SetUserConfiguredAC/DC` são distintas das APIs de plano (`PowerGet/SetActiveScheme`). A aplicação lê AC, DC e o GUID do plano ativo sob demanda. Uma captura versão 2 persiste os dois lados e o plano, mesmo para uma proposta de um lado só. Uma divergência em qualquer um deles invalida a revisão e impede a escrita.

A tela separa “revisei” da autorização: a escrita exige consentimento por consequência, autorização da ação e frase exata por captura; restauração tem consentimento e frase próprios. Serviço e tela iniciam bloqueados. Mesmo depois de uma autorização futura, não há compare-and-swap nas APIs, então uma corrida entre a última leitura e o setter ainda exige cautela. Nenhum setter foi chamado nesta etapa.

Em 19/10/2026 foi executada uma leitura nova, depois da alteração da lógica: AC e DC retornaram `Equilibrado`, e o plano ativo foi lido separadamente. Uma captura persistida com alvos iguais aos valores atuais foi reaberta em um segundo processo; esquema, AC/DC, GUID do plano e fingerprint passaram na validação. Os nomes do modo vêm desta consulta atual, não de uma observação anterior. Veja [documentação do modo de energia](docs/ADJUSTMENTS.md) e [progresso/testes](docs/PROGRESS.md).

## Distribuição experimental

Os pacotes 0.5.0 correspondem à tag `v0.5.0-experimental` e permanecem inalterados; eles não incluem este fluxo. A versão 0.6.0 é experimental, não validada em outro computador e o instalador não tem assinatura digital própria. Pacotes 0.6.0, hashes e estado de teste estão em `artifacts/` e `docs/PROGRESS.md`. Não foram executados testes de instalação/desinstalação do pacote 0.6.0 nesta etapa. Nenhuma GitHub Release foi publicada.

Veja [diagnóstico e privacidade](docs/DIAGNOSTICS.md), [estado de implementação](docs/PROGRESS.md), [ajuste VIDEOIDLE](docs/ADJUSTMENTS.md), [catálogo de candidatos](docs/INITIAL-CATALOG.md), [recuperação](docs/RECOVERY.md), [artefatos e instalador](packaging/README-INSTALADOR.md) e [passos para publicar a pre-release](docs/RELEASE-EXPERIMENTAL.md).

## Segurança e limitações

A leitura local requer apenas as permissões que o Windows conceder ao processo. O app não solicita elevação. Falhas ou dados parciais são reportados; não são tratados como sucesso.

O protocolo codificado para escrita persiste a intenção antes da mutação, recusa conflitos observados e verifica por leitura posterior. As APIs não oferecem operação transacional/comparação atômica, então uma mudança concorrente entre a última leitura e a chamada nativa ainda pode exigir revisão manual. A captura local não é autenticada nem protegida contra edição pelo mesmo usuário. Não existe worker elevado.

Processos, Registro, políticas, rede, instalação assistida de drivers e integrações de GPU continuam indisponíveis. Não prometemos ganho de FPS. Não há mudança em BIOS, firmware, tensão, frequência ou proteção.

## Ambiente

- Windows 10+ x64 para o aplicativo distribuído; alvo net10.0-windows.
- SDK .NET 10.0.100 ou faixa de recursos mais recente conforme global.json.
- Inventário usa System.Management 10.0.12.
- ZIP autocontido inclui runtime .NET; publicação e startup local não equivalem a teste de instalação nem validação em outro PC.

Instale o SDK pelo canal oficial: [Instalar .NET no Windows](https://learn.microsoft.com/dotnet/core/install/windows). Consulte [releases e suporte do .NET](https://learn.microsoft.com/dotnet/core/releases-and-support).

## Compilar, executar e testar

Na raiz do repositório:

    dotnet restore .\CalixtoOptimizer.sln
    dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore
    dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release
    dotnet run --project .\src\Optimizer.App\Optimizer.App.csproj --configuration Release

O harness Core cobre catálogo/perfis/consentimento/diário/simulação e os fluxos VIDEOIDLE e modo de energia com adaptadores falsos. Ele não chama APIs de escrita nem comprova reversão real. O inventário, planos, VIDEOIDLE e getters do modo de energia foram validados separadamente no Windows local, somente leitura.

## Estrutura

    src/Optimizer.App/          WPF, navegação e interface
    src/Optimizer.Core/         inventário, catálogo, perfis e fluxo de operações
    src/Optimizer.Windows/      WMI, inventário PowrProf, VIDEOIDLE e modo de energia do usuário
    tests/Optimizer.Core.Tests/ harness Core com falhas e adaptadores simulados
    docs/                       arquitetura, diagnóstico, catálogo, recuperação e progresso
    packaging/                  fonte do Inno Setup e documentação

## Privacidade

Não há conta, servidor ou telemetria remota. IDs PnP são mantidos somente em memória para cruzar as consultas e não entram no CSV ou JSON. O JSON também omite GUIDs de planos, nomes da conta/computador, números de série, endereços de rede e caminhos locais; ainda contém nomes/modelos de componentes e drivers. Revise a prévia antes de salvar ou compartilhar. Históricos técnicos VIDEOIDLE e de modo de energia só são gravados após captura explícita em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealVideoIdle` e `RealPowerMode`.
