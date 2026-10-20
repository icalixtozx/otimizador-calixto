# Validação

## Verificações incluídas

`tests/Optimizer.Core.Tests` verifica filtragem sem diferenciar maiúsculas/minúsculas, separação entre assinatura desconhecida e assinada/não assinada, escape de CSV e omissão de IDs PnP.

Comandos:

```powershell
dotnet restore .\CalixtoOptimizer.sln
dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore
dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release
dotnet run --project .\src\Optimizer.App\Optimizer.App.csproj --configuration Release
```

## Validação pendente

Ainda é necessário compilar e executar no Windows com o SDK .NET 10, confirmar a versão/build do Windows, observar a coleta real e comparar os nomes reportados com o Gerenciador de Dispositivos. A consulta CIM do ambiente desta sessão retornou acesso indisponível; isso não prova que a consulta falhará dentro do app instalado.

Este conjunto não testa compatibilidade de drivers, benchmarks de jogos ou ajustes de sistema: tais funcionalidades não fazem parte da primeira fatia.

## Estado da validação 0.6.0 (2026-10-19)

A seção “Validação pendente” acima descreve o estado inicial e foi superada pelos registros posteriores em docs/PROGRESS.md. As consultas WMI/PowrProf reais, a captura real AC/DC/plano e a reabertura em outro processo estão descritas lá. A execução atual do SDK também está registrada na matriz 0.6: nenhum SDK está instalado neste ambiente, apesar dos builds anteriores documentados.

### Checklist visual manual — somente leitura

Abra artifacts/release-v0.6.0-experimental/CalixtoOptimizer/Optimizer.App.exe e confira:

1. A visão geral abre em diagnóstico, sem mudança automática.
2. Entre em **Planos**; confirme que a tela carrega e que o histórico existente aparece como captura, sem chamá-lo de backup do Windows.
3. No cartão de modo de energia, use somente **Ler modo configurado (somente leitura)**. Confira AC, DC, plano ativo e compatibilidade; não use comandos de aplicação/restauração.
4. Escolha um alvo para exibir a consequência AC/DC. Isso invalida a captura selecionada apenas na tela; não apaga o histórico nem altera o Windows.
5. Para verificar a separação de consentimento, opcionalmente clique em “Capturar estado e alvos no histórico”. Isso grava uma nova captura JSON local com leitura fresca de AC, DC e plano ativo, sem chamar setters. Depois marque apenas “Revisei a consequência” e confirme que a aplicação continua desabilitada.
6. Desmarque “Revisei” e pare. Não marque caixas “Autorizo”, não digite a frase e não clique em aplicar/restaurar. Confira que as capturas sintéticas, VIDEOIDLE e modo de energia estão em áreas/estados distintos.

Relate o item que falhar ou “checklist concluído”. A nova captura é opcional e afeta somente o histórico local. Não publique screenshot com identificadores/hardware.

### Plano de instalação, atualização e desinstalação — roteiro executado

Pacotes existentes: instalador 0.5.0 em artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe e instalador 0.6.0 em artifacts/installer/CalixtoOptimizer-0.6.0-experimental-win-x64-setup.exe. Os dois scripts têm o mesmo AppId; ambos são por usuário (PrivilegesRequired=lowest). O diretório usado no teste foi %LOCALAPPDATA%\Programs\CalixtoOptimizer-Test, passado como /DIR nas duas versões, para testar upgrade sobre a mesma pasta.

O roteiro executado foi:

1. Preflight somente leitura: confirmar que o app está fechado e que não há instalação anterior com esse AppId. Se existir, parar e pedir revisão.
2. Registrar contagem e SHA-256 dos arquivos em %LOCALAPPDATA%\CalixtoOptimizer\History e guardar uma cópia local temporária de recuperação, sem imprimir nem enviar conteúdo.
3. Instalar 0.5.0 no diretório de teste; conferir executáveis, versão, chave de desinstalação do usuário e atalho do Menu Iniciar.
4. Instalar 0.6.0 no mesmo /DIR; conferir substituição/versão 0.6, chave e atalho, e comparar o histórico com o manifesto inicial.
5. Executar unins000.exe da pasta de teste; conferir remoção apenas da instalação, chave e atalho, e confirmar que o histórico do usuário continua com a mesma contagem e hashes.
6. Se qualquer dado diferir, interromper o restante e pedir revisão. Manter a cópia de recuperação até os checks concluírem; não restaurar sobre arquivos concorrentes sem aprovação.

Efeitos temporários observados e removidos: arquivos no diretório de teste, chave de desinstalação em HKCU, atalho do Menu Iniciar e cópia local do histórico para recuperação. O instalador não inicia o aplicativo. Não será alterado nenhum estado de energia, Registro de ajustes, política, serviço, rede, driver ou prioridade pelo app. O código atual não tem arquivo/store separado de configurações do aplicativo; portanto, o teste pode verificar preservação do histórico existente, mas não uma migração de configurações que não existem.

**Status do roteiro:** executado após autorização; resultados efetivos e hashes estão em docs/PROGRESS.md, seção 15. A compilação e a inspeção do .iss foram complementadas por instalação, upgrade e desinstalação reais.

### Resultado real do ciclo 0.5.0 → 0.6.0 → desinstalação (2026-10-19)

- Setup 0.5.0 instalado no destino isolado; código 0, executável 0.5.0.0, chave de desinstalação e atalho presentes.
- Setup 0.6.0 aplicado sobre o mesmo destino; código 0, executável 0.6.0.0 e entrada 0.6.0-experimental presentes.
- unins000.exe executado; código 0. O diretório de teste, chave HKCU e atalho foram removidos.
- O histórico do usuário tinha um arquivo antes e depois, com os mesmos hashes. O aplicativo não foi iniciado; nenhum setter foi chamado.
- Não há store separado de configurações do app para testar migração. Este ciclo não testa UI nem outro computador.

### Resultado de build/testes com SDK 10.0.402 (2026-10-19)

Restore concluído, build Release concluído com 0 avisos e 0 erros, e 54/54 model checks aprovados. Veja docs/PROGRESS.md, seção 15, para os comandos e limites do que foi verificado.

## Diagnóstico de rede somente leitura (2026-10-19)

### Build e testes simulados

O repositório continuou do commit `57c1bf3d714f6809f33eb9479dbc575bc75c0511`. Build e testes usaram explicitamente o SDK local `10.0.402`, selecionado pelo caminho `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, compatível com `global.json` (10.0.100, `latestFeature`).

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" restore .\CalixtoOptimizer.sln
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" build .\CalixtoOptimizer.sln --configuration Release --no-restore
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build
```

Restore passou. Build Release passou com 0 avisos e 0 erros. O harness passou 63/63 verificações, incluindo nove casos novos de rede: sucesso/RTT, falha DNS, timeouts ICMP, erro de destino inalcançável, ausência de link local, falha de leitura local, resultado parcial, cancelamento preservando amostras concluídas e validação dos limites. Todo probe nesses casos usa adaptador falso e não envia tráfego.

### Verificação real somente de leitura

O método real `WindowsNetworkDiagnosticsAdapter.ReadAdapters()` foi invocado em um smoke harness temporário, que não foi incluído no repositório. Resultado agregado: 33 interfaces retornadas; 6 com estado `Up`; 1 `Up` com endereço não loopback; 1 `Up` com gateway; 2 `Up` expondo servidores DNS pela API; zero avisos. Nomes, endereços, MACs e descrições não foram registrados.

Comparação local com `Get-NetAdapter -IncludeHidden` e `Get-NetIPConfiguration -All`, ambos consultas somente leitura: 10 adaptadores no cmdlet (`Up=1`, `Disconnected=5`, `Not Present=4`); 6 configurações; 1 configuração `Up`, com gateway; 1 configuração `Up` com DNS. O adaptador ativo com endereço/gateway concorda entre as leituras. A enumeração .NET retorna também interfaces virtuais, de túnel e loopback que não aparecem como adaptadores equivalentes no cmdlet; por isso os totais brutos diferem. O segundo servidor DNS associado a interfaces `Up` na enumeração .NET não apareceu como uma configuração `Up` em `Get-NetIPConfiguration`; permanece uma divergência de visibilidade entre APIs, e a UI mostra os valores reportados por interface sem tratá-los como prova de acesso DNS/Internet.

Nenhuma resolução DNS nem sondagem ICMP real foi executada nesta validação; o botão do app exige ação explícita e não foi acionado. Nenhuma alteração em DNS, adaptadores, firewall, Registro, políticas, energia ou rede foi feita.

### Revisão manual da interface ainda pendente

O XAML e os bindings foram compilados, mas a página não foi aberta para inspeção visual. Checklist manual curto:

1. Abra **Rede** e confira que a lista apresenta link, endereços, gateway e DNS; identifique interfaces virtuais sem as confundir com conexões físicas.
2. Antes do teste, confira o destino editável, a finalidade, quatro amostras e timeout de 1.000 ms. Não há teste de download.
3. Inicie somente se quiser enviar a resolução DNS/sondagens ICMP descritas. Confira IP resolvido, RTT, amostras, perda e duração; teste **Cancelar** durante a sequência para ver o resultado parcial.
4. Interprete “sem resposta ICMP” como inconclusivo para acesso geral à internet.

Na etapa inicial de 2026-10-19 ainda não havia teste DNS/ICMP real; o teste real subsequente está registrado abaixo. Validação em outro PC permanece pendente. Nenhum instalador ou pacote de distribuição foi regenerado nesta etapa.

### Continuação da validação de rede e GPU (2026-10-19)

- Build após os atalhos nativos de GPU: SDK `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` (10.0.402), `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore`: êxito, 0 avisos/erros. Testes `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: 63/63 aprovados; casos de rede são simulados. Restore da solução também passou. Execuções no `%USERPROFILE%\Downloads\CalixtoOptimizer`.
- A checagem visual da página **Rede** continua pendente: esta sessão não abriu janela nem disputou mouse/teclado. Checklist curto: abrir Rede; conferir a indicação de link e campos de endereço/gateway/DNS; confirmar destino/finalidade/amostras/timeout antes de iniciar; confirmar RTT/perda/duração; usar Cancelar durante uma nova sondagem apenas se desejar; tratar ausência ICMP como inconclusiva.
- Teste ativo autorizado, executado pelo `NetworkDiagnosticsService` real com `WindowsNetworkDiagnosticsAdapter`: destino literal `1.1.1.1`, finalidade de eco ICMP, quatro sondagens sequenciais com payload de 32 bytes e timeout de 1.000 ms. Resultado: 4/4 respostas, 0 timeout, 0 falhas, perda observada 0%, RTTs 16, 17, 18 e 16 ms, duração total 130 ms. Nenhuma resolução DNS ou download foi necessário. Isso confirma somente respostas ICMP daquele destino durante as quatro tentativas, não velocidade, qualidade geral nem disponibilidade universal da internet.
- O limite autorizado de até quatro sondagens foi respeitado. Não foi executado segundo `ping.exe` nativo, porque isso enviaria sondagens adicionais. A leitura local de adaptadores segue comparada com `Get-NetAdapter`/`Get-NetIPConfiguration` na seção anterior; o teste ativo específico permanece sem comparação nativa independente.
- Investigação GPU: código WMI existente reutilizado e documentação/links oficiais revisados. O inventário do app é evidência de controlador reportado, não de preferência configurada nem de uso efetivo. Preferência pode ser revisada nas Configurações > Sistema > Tela > Gráficos; uso corrente por processo pode ser observado no Gerenciador de Tarefas > GPU Engine. O app ganhou botões que abrem essas páginas nativas. Não lê nem grava preferências, não coleta uso GPU e não usa chaves privadas do Registro. A abertura visual dos novos atalhos não foi feita nesta sessão.
- Fontes: [Win32_VideoController](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller), [URI oficial de Configurações gráficas](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings), [preferência por aplicativo no Windows 11](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11), [DXGI enumera adaptadores segundo preferência escolhida pelo app](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/nf-dxgi1_6-idxgifactory6-enumadapterbygpupreference), [GPU Engine no Gerenciador de Tarefas](https://learn.microsoft.com/en-us/surface/surface-book-gpu-overview).
- Não houve alteração de configuração de rede, GPU, energia, Registro, política, serviço ou driver.
