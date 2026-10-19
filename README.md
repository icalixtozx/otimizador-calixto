# Calixto Optimizer

Aplicativo local de diagnóstico de hardware/drivers para Windows e laboratório técnico de ajustes. É experimental e não foi validado em outro computador. O app inicia em diagnóstico; nenhuma mudança é aplicada automaticamente.

## Estado real

- Inventário WMI de hardware/drivers e exportação CSV sanitizada continuam somente leitura.
- Interface em português, alto contraste preto e branco.
- Catálogo de desempenho para os perfis permanece sem ajustes habilitados. A demonstração sintética, separada e desligada por padrão, só exercita simulação em memória.
- VIDEOIDLE é o primeiro adaptador técnico: leitura real e captura persistida sob clique explícito. A aplicação/reversão foram codificadas no Core, mas continuam bloqueadas por padrão e não estão expostas como ação no app.
- A captura VIDEOIDLE é o índice AC do plano ativo, não inclui bateria/DC e não é backup do Windows. Histórico técnico fica separado da demonstração.
- O pacote é v0.4.0 experimental, portátil autocontido Windows x64. Não validado em outro PC.
- Script Inno Setup por usuário existe em `packaging/`; o EXE do instalador ainda não foi produzido enquanto `ISCC.exe` não estiver disponível.

Veja [estado de implementação](docs/PROGRESS.md), [ajuste VIDEOIDLE](docs/ADJUSTMENTS.md), [catálogo de candidatos](docs/INITIAL-CATALOG.md), [recuperação](docs/RECOVERY.md) e [instruções do instalador](packaging/README-INSTALADOR.md).

## Segurança e limitações

Aplicar e reverter real exigem uma política habilitada e aprovação específica, vinculada à operação, ação, fingerprint da captura, consequência e frase de confirmação. O app distribui e injeta a política que sempre bloqueia. Aprovação da demonstração sintética nunca habilita escrita real.

O mecanismo persiste uma intenção antes da primeira escrita, recusa conflitos observados e verifica por nova leitura. As APIs não oferecem operação transacional/comparação atômica, então uma mudança concorrente entre a última leitura e a chamada nativa ainda pode requerer revisão manual. A captura local não é autenticada nem protegida contra edição pelo mesmo usuário. Não existe worker elevado.

Processos, Registro, políticas, rede, instalação assistida de drivers e integrações de GPU continuam indisponíveis. Não prometemos ganho de FPS. Não há mudança em BIOS, firmware, tensão, frequência ou proteção.

## Ambiente

- Windows 10+ x64 para o aplicativo distribuído; alvo `net10.0-windows`.
- SDK .NET 10.0.100 ou faixa de recursos mais recente conforme `global.json`.
- Inventário usa `System.Management 10.0.12`.
- ZIP autocontido inclui runtime .NET; publicação e startup local não equivalem a teste de instalação nem validação em outro PC.

Instale o SDK pelo canal oficial: [Instalar .NET no Windows](https://learn.microsoft.com/dotnet/core/install/windows). Consulte [releases e suporte do .NET](https://learn.microsoft.com/dotnet/core/releases-and-support).

## Compilar, executar e testar

Na raiz do repositório:

~~~powershell
dotnet restore .\CalixtoOptimizer.sln
dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore
dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release
dotnet run --project .\src\Optimizer.App\Optimizer.App.csproj --configuration Release
~~~

O harness testa catálogo/perfis/consentimento/diário/simulação e o fluxo VIDEOIDLE usando somente adaptadores e storage falsos. Ele não chama as APIs reais de escrita do Windows e não comprova reversão real.

## Estrutura

~~~text
src/Optimizer.App/          WPF, navegação e interface
src/Optimizer.Core/         inventário, catálogo, perfis e fluxo de operações
src/Optimizer.Windows/      inventário WMI e adaptador PowrProf VIDEOIDLE
tests/Optimizer.Core.Tests/ harness com falhas e adaptadores simulados
docs/                       arquitetura, catálogo, recuperação e progresso
packaging/                  fonte do Inno Setup e documentação
~~~

## Privacidade

Não há conta, servidor ou telemetria remota. IDs PnP ficam em memória e não entram no CSV. Histórico técnico é gravado somente após captura explícita em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealVideoIdle`; contém apenas o GUID do plano, valores AC do ajuste, estado e timestamps. Revise qualquer arquivo antes de compartilhá-lo.
