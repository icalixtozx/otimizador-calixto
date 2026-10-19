# Calixto Optimizer

Aplicativo local para identificar hardware e drivers no Windows. O app inicia em diagnóstico somente leitura; não aplica ajustes, não instala drivers e não envia inventário pela rede.

## Estado

- Inventário local do Windows, CPU, GPU, memória, placa-mãe, armazenamento e adaptadores físicos de rede.
- Identificação de drivers por Win32_PnPSignedDriver, com busca, filtro de assinatura e códigos de problema Plug and Play quando o Windows os informa.
- Exportação CSV sanitizada: IDs PnP internos não são exportados.
- Interface em português com alto contraste preto e branco.
- O Core contém um catálogo vazio, avaliação de compatibilidade, gerador de planos em simulação e políticas de perfil Leve, Equilibrado, Extremo e Personalizado.
- A interface permite escolher Leve, Equilibrado, Extremo ou Personalizado, revisar o plano e consequências e registrar consentimento por item. O catálogo de produto segue vazio.
- Há uma demonstração explicitamente sintética, desligada por padrão, que exercita simulação em memória e grava histórico local sem snapshots ou backups do Windows.
- Histórico e avaliação conservadora de retomada estão conectados. Não existe executor real, instalação assistida de drivers nem integração de GPU confirmada.
- ZIP autocontido experimental Windows x64 gerado localmente em `artifacts/`; os artefatos não são versionados nem anexados a uma Release. Não validado em outro computador.
- O fonte do instalador Inno Setup está em `packaging/CalixtoOptimizer.iss`; o EXE de instalação não foi produzido porque ISCC.exe não está disponível neste ambiente. Ver `packaging/README-INSTALADOR.md`.

O inventário descreve o que o Windows reportou. Ele não verifica se há versões mais novas, não prova que um driver é adequado e não promete ganho de desempenho. Simulação não altera o Windows.

## Ambiente

- Windows x64; alvo net10.0-windows.
- SDK .NET 10.0.100 ou faixa de recursos .NET 10 mais recente (global.json).
- A coleta usa WMI por meio do pacote Microsoft System.Management 10.0.12.
- Para executar o build framework-dependent, é necessário o .NET 10 Desktop Runtime. O pacote portátil experimental inclui runtime e está em artifacts; ainda não foi testado em outro computador.

Instale o SDK pelo canal oficial da Microsoft: [Instalar .NET no Windows](https://learn.microsoft.com/dotnet/core/install/windows). A documentação oficial descreve o suporte em [releases e suporte do .NET](https://learn.microsoft.com/dotnet/core/releases-and-support).

## Compilar, executar e conferir

Execute os comandos na raiz deste repositório:

~~~powershell
dotnet restore .\CalixtoOptimizer.sln
dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore
dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release
dotnet run --project .\src\Optimizer.App\Optimizer.App.csproj --configuration Release
~~~

O harness executa verificações do inventário/exportação, catálogo, compatibilidade, perfis, consentimento, diário, snapshots, recuperação e simulações de sucesso, falha, interrupção, corrupção, falha de gravação, espaço insuficiente, conflito e retomada. Ele não altera o Windows.

## Estrutura

~~~text
src/Optimizer.App/          WPF, navegação e interface
src/Optimizer.Core/         inventário, catálogo, planos, perfis e simulação
src/Optimizer.Windows/      adaptadores de inventário Windows somente leitura
tests/Optimizer.Core.Tests/ harness executável
docs/                       arquitetura, limitações, validação e progresso
~~~

## Privacidade

O app não possui conta, servidor ou telemetria remota. IDs de dispositivo ficam apenas em memória para correlacionar driver e dispositivo. A exportação CSV inclui nomes e metadados de drivers, mas omite esses IDs. Revise o CSV antes de compartilhá-lo.
