# Calixto Optimizer

Aplicativo local para identificar hardware e drivers no Windows. Esta primeira entrega é somente leitura: não aplica ajustes, não instala drivers e não envia inventário pela rede.

## Estado desta entrega

- Inventário local do Windows, CPU, GPU, memória, placa-mãe, armazenamento e adaptadores físicos de rede.
- Identificador de drivers baseado em `Win32_PnPSignedDriver`, com busca, filtro de assinatura e códigos de problema Plug and Play quando o Windows os informa.
- Exportação CSV sanitizada: IDs PnP internos não são exportados.
- Interface em português, alto contraste preto e branco, estados de carregamento, falha, resultado vazio e resultado parcial.
- Perfis, Registro, políticas, energia, rede, prioridade de processos e instalação de drivers estão desativados nesta fase.

O inventário descreve o que o Windows reportou. Ele não verifica se há versões mais novas, não prova que um driver é adequado e não promete ganho de desempenho.

## Ambiente

- Windows x64. O alvo planejado pelo documento é Windows 11 x64; esta cópia foi preparada para `net10.0-windows`.
- .NET SDK 10.0.100 ou uma faixa de recursos .NET 10 mais recente. `global.json` permite avançar dentro do major 10.
- A coleta usa WMI/CIM por meio do pacote Microsoft `System.Management` 10.0.12.

O diagnóstico local encontrou apenas o runtime .NET 8.0.21, sem SDK. Também encontrou metadados de Windows que dizem “Windows 10 Pro”, versão 26H2 e build 26300. Essa combinação não pôde ser confirmada pela consulta CIM do ambiente. A instalação do .NET 10 e a compatibilidade deste build precisam ser verificadas antes de declarar execução suportada.

Instale o SDK pelo canal oficial da Microsoft: [Instalar .NET no Windows](https://learn.microsoft.com/dotnet/core/install/windows). O SDK é requisito para compilar; instalar apenas runtime não basta. A documentação oficial informa que .NET 10 LTS tem suporte até novembro de 2028 e descreve as limitações de versões do Windows em [releases e suporte do .NET](https://learn.microsoft.com/dotnet/core/releases-and-support) e [instalação no Windows](https://learn.microsoft.com/dotnet/core/install/windows).

## Compilar, executar e conferir o modelo

Abra PowerShell nesta pasta (`outputs\CalixtoOptimizer`):

```powershell
dotnet --info
dotnet restore .\CalixtoOptimizer.sln
dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore
dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release
dotnet run --project .\src\Optimizer.App\Optimizer.App.csproj --configuration Release
```

O app abre sem privilégios administrativos. Consultas de inventário ficam em segundo plano e não modificam o sistema. O comando de teste executa verificações sem dependências externas do modelo de busca e da exportação sanitizada.

## Estrutura

```text
src/Optimizer.App/         WPF, navegação e interface
src/Optimizer.Core/        modelos e operações puras do catálogo
src/Optimizer.Windows/     adaptadores de inventário Windows somente leitura
tests/Optimizer.Core.Tests/ verificações executáveis do modelo
docs/                      arquitetura, limitações, validação e progresso
```

## Privacidade

O app não possui conta, servidor ou telemetria remota. IDs de dispositivo ficam apenas em memória para correlacionar driver e dispositivo. A exportação CSV inclui nomes e metadados de drivers, mas omite esses IDs. Revise o CSV antes de compartilhá-lo.
