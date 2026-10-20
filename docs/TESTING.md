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

## Programas pós-formatação e status vigente — 2026-10-20

### Verificações simuladas

- Com SDK `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` 10.0.402:
  `dotnet build .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-restore`
- Execução direta do harness: `dotnet .\tests\Optimizer.Core.Tests\bin\Release\net10.0\Optimizer.Core.Tests.dll` — **121/121 checks simulados aprovados** após corrigir um assert que dependia de capitalização.
- Os checks de programas usam `IProgramInstallRunner` falso; cobrem IDs fixos, origem e consentimento por pacote, WinGet ausente, ID não encontrado, código de falta de conectividade, falha parcial, cancelamento, interrupção do restante da seleção, reinício pendente e bloqueio da política de produção sem chamada ao runner.
- Os checks não executam `winget.exe`, não instalam aplicativos, não chamam o leitor real de Registro e não validam UAC, MSI/EXE, download, reinício ou remoção.

### Verificação de pacote/origem, separada dos testes

Os manifests exatos de `7zip.7zip` 26.04 e `VideoLAN.VLC` 3.0.24 foram lidos diretamente de `microsoft/winget-pkgs` em 2026-10-20. As versões são snapshots do manifesto. Os hashes x64 registrados ali não foram recalculados porque os instaladores não foram baixados. WinGet local já reportou `v1.29.380`; `winget show` falhou ao atualizar a origem com HTTP 404 (`0x80190194`). O metadata atual da origem permanece indisponível nesta tentativa; nenhuma fonte foi adicionada/resetada nem WinGet instalado.

### Build concluído e inspeção ainda pendente

- Após o usuário confirmar que fechou a janela, o build normal Release da solução foi executado com `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` (10.0.402): `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore` — **sucesso, 0 avisos e 0 erros**, incluindo WPF/apphost; não foi usado `UseAppHost=false` nem encerrado processo. O harness foi repetido depois do build: **121/121 checks simulados aprovados**.
- O build não equivale a executar o executável final. A compilação atual não foi aberta nem inspecionada visualmente; o checklist manual abaixo continua pendente. O CUA identificou anteriormente uma instalação Preview diferente do PID/caminho do executável Release, por isso a checagem não foi contornada.

### Checklist visual manual consolidado

- [ ] Programas: navegar para Programas pós-formatação; ler entradas instaladas; confirmar que as caixas permitem seleção múltipla e que “Não identificado localmente” não diz “não instalado”.
- [ ] Programas: selecionar 7-Zip e VLC; revisar para cada um ID/origem, versão snapshot, ação, escopo declarado, licença/acordos pendentes, UAC, reinício e observação de metadata datado.
- [ ] Programas: acionar apenas “Verificar bloqueio por pacote”; conferir resultado BLOQUEADA por item e nenhuma instalação iniciada.
- [ ] Drivers: candidatos/análise/prévia e bloqueio; não clicar em staging ou associação.
- [ ] Processos, Registro, Rede e Energia: seguir o checklist manual consolidado registrado acima; não encerrar processo, iniciar sondagem ou chamar setter.
- [ ] Histórico: distinguir seleção/revisão/captura de instalação efetivamente confirmada.

Instalação limpa, atualização real do catálogo por WinGet, instalação de pacote, UAC, falha e cancelamento durante instalador, reinício pendente, desinstalação, execução/recuperação real de driver, instalação limpa/desinstalação do Preview atual e teste em outro PC continuam pendentes.

## Revalidação visual e WinGet — 2026-10-20

- **Visual real, DLL do commit 7994bf6:** navegação e catálogo de Programas foram inspecionados; a seleção simultânea de 7-Zip e VLC montou as duas revisões; a leitura consultou 89 entradas locais sem avisos; os cartões individuais terminaram em `BLOQUEADA`. O botão não chamou o runner e não havia `winget.exe` antes nem depois. Nenhuma instalação ocorreu.
- **Leitura real de instalado:** HKLM contém `7-Zip 26.04 (x64)`, `Igor Pavlov`, versão `26.04`. A UI anterior não o reconheceu porque faltava essa variante de nome exata. Um alias exato e uma asserção foram adicionados; a validação lógica simulada passou. Build WPF e inspeção visual depois dessa correção aguardam o fechamento da janela PID 25128, que mantém DLLs de saída bloqueadas. Não foi encerrada.
- VLC não apareceu nas entradas Uninstall consultadas em HKLM/HKCU nas visões 32/64. A classificação continua “não identificado”, sem inferir ausência absoluta.
- **WinGet real, somente consulta:** versão `v1.29.380`; fonte padrão `winget`, `https://cdn.winget.microsoft.com/cache`, trusted, último update registrado 2026-09-30. Logs 16:57/16:58 registram HTTP 404 (`0x80190194`); tentativa 17:19 registra falha MSIX staging `0x87E80034`. `winget show` retornou cache `26.03` mesmo avisando falha de atualização. A API GitHub confirma o manifesto público 7-Zip `26.04`, mas não valida a disponibilidade atual no CDN WinGet.
- **Harness:** `Optimizer.Core.Tests` Release, SDK 10.0.402: build 0 aviso/erro; 121/121 checks simulados. A verificação nova de `7-Zip 26.04 (x64)` foi incluída no teste de detecção existente. Nenhum runner do harness executa instaladores.
- **Build solução:** tentativa após a correção falhou porque PID 25128 segurava `Optimizer.Core.dll` e `Optimizer.Windows.dll` em `src/Optimizer.App/bin/Release`; 20 avisos/4 erros após retries. Não reexecutei nem encerrei o processo. Repetir após o usuário fechar a janela.
- A próxima ação de diagnóstico proposta é atualizar só a metadata existente: `winget source update --name winget --disable-interactivity --verbose-logs`. Não executada; aguarda aprovação. Não usar reset/remove/add, aceitar acordos ou reparar/reinstalar App Installer sem nova revisão.
- Candidato de teste real posterior: VLC `VideoLAN.VLC` `3.0.24`, x64, machine, elevates-self, ainda não instalado segundo as entradas consultadas. Antes, exigir atualização e revisão de metadata/acordos. Verificação planejada por resultado WinGet e entrada de desinstalação versão/publicador/executável; remoção manual no Configurações > Aplicativos requer aprovação separada e não garante remover preferências/cache/associações. Instalação não aprovada nem executada.
- Drivers assistidos permanecem **parciais**. Nenhum pacote de distribuição foi gerado nesta validação.

### Resultado final após fechar a janela bloqueadora — 2026-10-20

- O usuário fechou a janela; nenhum processo foi encerrado automaticamente. Com o SDK explícito `.NET 10.0.402`, `dotnet build .\CalixtoOptimizer.sln -c Release --no-restore` passou com **0 avisos/0 erros**. O harness Release passou **121/121 checks simulados**.
- A versão recém-compilada abriu da DLL Release em `src\Optimizer.App\bin\Release\net10.0-windows`. A identidade do HWND foi cruzada com PID, caminho executável e linha de comando; a cópia de `dotnet.exe` indicada pelo helper visual teve o mesmo SHA-256 que o dotnet usado para iniciar a DLL. A inspeção foi feita somente nessa janela identificada.
- Na página Programas, o botão de leitura consultou 89 entradas; 7-Zip apareceu como `Instalação identificada · versão 26.04`, com a ressalva visível de que nome/publicador não provam a origem de instalação nem integridade. VLC permaneceu `Não identificado localmente` e a UI avisou que isso não comprova ausência.
- A seleção múltipla 7-Zip + VLC foi refletida como `Selecionados: 2`. A revisão visual exibiu origem, ação, escopo, termos/licença ainda não carregados/aceitos, permissões/elevação, reinício possível e cautela da metadata datada. O bloqueio foi acionado: resultado por pacote `BLOQUEADA` para ambos; texto informou que nenhuma chamada ao `winget.exe` ocorreu. A lista de processos confirmou nenhum `winget.exe` ativo após a ação. Não houve instalação.
- A etiqueta visual permanece `v0.6.0 · Experimental`; a inspeção confirma o comportamento do código do HEAD `7994bf6`, mas não corrige a etiqueta nesta entrega.
- Os 121 checks são simulados. A observação da UI e a leitura do Registro local são validações reais separadas. A consulta do source/log WinGet indicou HTTP 404 durante refresh; o manifest GitHub atual existe, mas isso não comprova disponibilidade no CDN. Nenhuma ação de atualização da origem foi feita.
- Não houve validação em outro PC nem teste real de instalação/desinstalação. VLC continua apenas candidato para futura aprovação; drivers assistidos continuam **parciais**. Nenhum ZIP/instalador foi gerado nesta etapa.

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

## Processos e prioridade — 2026-10-19

### Build e testes simulados

O módulo usa `System.Diagnostics.Process` para consulta local e `PriorityClass` como API suportada do Windows. A escrita depende do direito `PROCESS_SET_INFORMATION`; acesso negado e limites de caminho/bitness são apresentados sem presumir permissão. A aplicação mantém `BlockedProcessPriorityMutationPolicy` como padrão, e a interface não expõe uma chamada a setter.

Executado em PowerShell no diretório `%USERPROFILE%\Downloads\CalixtoOptimizer`, com SDK explícito `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` (10.0.402; `global.json` preservado em 10.0.100 com `latestFeature`):

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" build .\CalixtoOptimizer.sln --configuration Release
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build
```

Resultado final: build da solução passou com 0 avisos e 0 erros; harness passou **72/72** checks. Os novos casos de processos usam somente `FakeProcessPriorityAdapter` e cobrem: item individual/consequências/ausência de Tempo real; plano, consentimento e snapshot no diário comum; política bloqueada; acesso negado; processo encerrado; mudança de estado; reutilização de PID entre pré-checagem e setter e durante restauração; falha de persistência antes do setter; falha de persistência após escrita simulada sem rollback automático; restauração condicional verificada; conflito externo. Outro teste fixa os valores numéricos originais do diário JSON e impede substituir estado de mutação verificada por simulação.

O projeto `tests/Optimizer.ProcessProbe` também compilou. Ele imprime PID, criação UTC e caminho e aguarda Enter; não altera prioridade. O executável não foi iniciado neste marco.

### Diagnóstico real e validação visual

- Não foi feita consulta do inventário de processos neste computador. Nenhum PID/caminho pessoal foi coletado para este registro e nenhum setter de prioridade foi chamado.
- A página foi integrada por XAML/ViewModel, mas a janela não foi aberta para inspeção visual; o mouse/teclado não foi controlado. A interface está compilada, mas o resultado visual está pendente.
- Checklist manual: abra **Processos**, use **Atualizar processos**, confira estados de acesso negado/parcial e os campos de PID/criação/caminho; escolha uma única linha; confirme que a lista de destino não inclui Tempo real; solicite a revisão e confira a consequência; se desejar validar a persistência, marque o consentimento e clique **Registrar consentimento e captura**, depois confira o evento no histórico. O banner deve continuar informando que a mutação real está bloqueada. Não há botão para aplicar/restaurar nesta versão.
- Em um futuro teste real, o processo descartável `CalixtoOptimizer.ProcessProbe.exe` deve ser iniciado e identificado por leitura atual. Só depois de mostrar o PID, criação, caminho e prioridade atual será pedido consentimento específico para uma mudança única; o teste de reversão também requer aprovação e será condicionado à identidade/estado atuais. Nenhuma dessas mutações foi autorizada ou executada agora.

Nenhum ZIP ou instalador foi gerado. A validação visual em outro computador, teste real com o probe, instalação do build atual e teste de uso/desempenho continuam pendentes.

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

### Probe de processos: execução real somente de leitura (2026-10-19)

- Artefato usado: `tests/Optimizer.ProcessProbe/bin/Release/net10.0/Optimizer.ProcessProbe.exe`, já compilado. A tentativa inicial via host padrão não iniciou o app porque nele só havia .NET 8. O runtime existente .NET 10.0.13 foi selecionado apenas para o processo do probe por `DOTNET_ROOT`; não houve instalação nem alteração persistente de ambiente.
- O probe aguardou Enter, sem trabalho intensivo. `Get-Process` leu PID, `StartTime` convertido para UTC, `Path`, `PriorityClass` e `Responding`. Resultado: caminho acessível, prioridade `Normal`, `Responding=True`; PID/horário/caminho literal foram mantidos fora da documentação versionada. A hora e o caminho bateram com o que o probe imprimiu.
- Encerramento: Enter enviado ao processo, código de saída 0; consulta seguinte não encontrou aquele PID. Nenhuma chamada de escrita foi executada. Este resultado é uma observação real do Windows via PowerShell, não uma validação da página WPF ou da implementação de aplicação do app.
- Plano para um futuro probe novo: revisar de novo PID + criação UTC + caminho + prioridade. Só se o estado novo for `Normal`, apresentar captura de `Normal` como anterior e `BelowNormal` como alvo. Revalidar a mesma identidade e `Normal` imediatamente antes da escrita; verificar `BelowNormal` depois. A recuperação proposta volta a `Normal` somente com a mesma identidade e prioridade ainda `BelowNormal`. Processo encerrado, identidade não verificável, PID reutilizado ou prioridade divergente significam conflito e nenhuma escrita/restauração automática.
- Consequência: `BelowNormal` reduz a prioridade relativa de escalonamento de CPU do probe sob contenção; pode atrasar esse processo. Não produz ganho prometido de FPS. Restauração também altera estado e exige aprovação separada. O app ainda bloqueia escrita por padrão e a interface não oferece aplicar/restaurar.
- Nenhuma captura persistida no diário do app foi criada: `CaptureAsync` também registra uma decisão explícita de consentimento, que não foi presumida. O processo já havia terminado; a captura posterior requer uma instância nova e leitura nova. Aceitar registrar consequência/captura não autoriza aplicar a prioridade.
- A verificação visual permanece pendente. O inventário CUA não mostrou aplicações abertas e a sessão não trouxe janela do app; para não tomar foco, a janela não foi lançada nem operada. Checklist manual: abrir **Processos**; atualizar a lista; selecionar somente `Optimizer.ProcessProbe`; confirmar identidade/criação/caminho e prioridade `Normal`; escolher `BelowNormal`; conferir a consequência; revisar que a captura registra o estado anterior e o alvo; conferir que não há ação de escrita habilitada pelo modo bloqueado. Não testar em jogo, anticheat, serviço ou processo crítico.
- Os **72/72** do registro anterior são simulados e não fazem parte desta validação real. Nenhum build ou harness foi executado neste registro documental.

### Captura persistida pelo adaptador real de processos (2026-10-19)

- Após autorização do usuário somente para persistir captura/consentimento, foi iniciado um probe novo. `WindowsProcessPriorityAdapter.Inspect` retornou identidade completa disponível e prioridade `Normal`; comparação com a leitura independente `Get-Process` confirmou PID, criação UTC e caminho.
- Um runner temporário, compilado com SDK local 10.0.402 contra `Optimizer.Core` e `Optimizer.Windows` (0 avisos, 0 erros), chamou `CreateReview` e `CaptureAsync` com o consentimento explícito recebido. A leitura posterior do mesmo adaptador confirmou identidade inalterada e prioridade ainda `Normal`.
- Persistência verificada: o `JsonOperationJournalStore` reabriu o JSON recém-criado em `%LOCALAPPDATA%\CalixtoOptimizer\History`. O registro contém estado `SnapshotsCaptured`, recibo de consequência aceito, prioridade anterior `Normal`, alvo `BelowNormal` e fingerprint não vazio. O PID, horário literal, caminho de perfil e nome do arquivo foram omitidos dos documentos versionados; o JSON permanece no diário local do aplicativo.
- No momento desta captura, a execução limitou-se a leitura e captura: não chamou `ApplyAsync`, `SetPriority` ou restauração. Depois da aprovação específica, o ciclo real separado está registrado na seção seguinte. O recibo de consentimento não autorizou a escrita; a política de produção continua bloqueada.
- A janela do app ainda não foi aberta nem manipulada; validação visual pendente. Compilar o runner não compila toda a solução nesta etapa e não comprova a UI, a escrita real ou a reversão.

### Ciclo real autorizado no probe (2026-10-19)

- Depois da aprovação explícita do ciclo completo, o runner usou `ProcessPriorityOperationService` e `WindowsProcessPriorityAdapter` com `ConfirmedProcessPriorityMutationPolicy(enabled: true)` somente para este teste. A política padrão do aplicativo permaneceu bloqueada e a UI não foi usada.
- Pré-escrita: identidade persistida comparada com a leitura atual como `Match`, estado `Normal`. Aplicação real: `ApplyAsync` concluiu `MutationVerified`; leitura posterior mostrou `BelowNormal` para a mesma identidade.
- Restauração real: `RestoreAsync` exigiu a mesma identidade e o estado proposto `BelowNormal`, chamou a restauração condicional e concluiu `RestoredVerified`. Leitura final confirmou identidade igual e prioridade `Normal`.
- O JSON no diário local foi relido após o ciclo; sequência registrada: `Planned`, `ConsentRecorded`, `SnapshotCaptured`, `MutationArmed`, `MutationVerified`, `RestorationArmed`, `RestoredVerified`. O probe recebeu Enter, terminou com código 0 e foi confirmado ausente em consulta posterior.
- Escopo efetivo: somente o probe descartável. Não houve alteração em jogo, serviço, outro processo ou configuração de energia, rede, Registro, política, driver ou firmware. O resultado verifica o caminho real de captura/aplicação/verificação/restauração do serviço e adaptador nesta máquina e com esta instância; não valida a UI, outro computador ou outros tipos de processo.
- O runner compilou com o SDK local 10.0.402, 0 avisos e 0 erros. Nenhuma compilação completa da solução nem reexecução do harness de 72 testes foi feita nesta etapa. Esses 72 continuam sendo testes simulados.
- Validação visual da página **Processos** segue pendente; checklist manual acima.

## Registro e políticas — protótipo simulado no commit anterior (2026-10-19)

Build e harness foram executados no repositório `%USERPROFILE%\Downloads\CalixtoOptimizer`, em PowerShell, com o SDK explicitamente escolhido `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` (`10.0.402`). `global.json` continua em `10.0.100` com `latestFeature`.

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" build .\CalixtoOptimizer.sln --configuration Release
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build
```

Resultado observado: solução completa (WPF, Core, Windows, testes e probe) compilou com 0 avisos e 0 erros; harness passou **85/85** verificações. Treze verificações novas usam somente `FakeRegistryPreferenceAdapter` e storage de diário local de teste. Cobrem plano/consequência/compatibilidade x64; captura que distingue valor ausente de `REG_DWORD 0`; recusa de estado alterado desde a prévia; restauração de tipos `REG_SZ`, `REG_EXPAND_SZ`, `REG_DWORD`, `REG_QWORD`, `REG_MULTI_SZ`, `REG_BINARY` e `REG_NONE`; remoção apenas do valor criado e preservação de outros valores; acesso negado; conflitos antes da aplicação/restauração e entre persistir intenção e chamar setter; gerenciamento desconhecido; política padrão bloqueada; falha de gravação por espaço insuficiente injetada; interrupção após setter simulado sem rollback automático; e recusa de chave pai ausente.

Os casos de Registro são **simulados**. Nenhuma API do Registro foi chamada pelo harness; o adaptador `WindowsRegistryPreferenceAdapter` apenas foi compilado. Nenhuma chave ou valor real, inclusive de teste, foi criado, alterado ou removido. A tela ainda não integra o protótipo. A visibilidade efetiva no Explorador, permissões reais, edição/build diferente, conflito com gestão corporativa e comportamento em outro PC permanecem pendentes.

### Procedimento de integração planejado naquele commit

Usar uma chave única `HKCU\Software\CalixtoOptimizer\IntegrationTests\<GUID>` e o valor `ProbeValue` (`REG_DWORD`, visão 64 bits). Conferir primeiro que a chave GUID inexiste; criar valor `0`; ler hive/visão/tipo/dado; definir `1`; ler novamente; excluir somente `ProbeValue`; confirmar chave vazia; excluir apenas a chave GUID e confirmar que ela não existe. Se surgir valor/subchave inesperado, parar sem excluir. Os pais compartilhados ficam intactos. O teste valida acesso e leitura/escrita do Registro sob uma chave isolada; não toca `HideFileExt`, não valida UI do Explorador e requer um runner isolado. Não foi executado; autorização para desenvolvimento e testes sem mutação não cobre criar essa chave.

Esse registro descreve o estado anterior. O resultado atualizado está abaixo.

## Registro — teste isolado real e página integrada (2026-10-19)

### Mecanismo de Registro em chave isolada — executado com aprovação

Comando opt-in (cada nova execução precisa de aprovação própria para escrita na chave temporária):

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk-10.0.402\dotnet.exe" run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build -- --windows-registry-integration
```

O runner gerou `HKCU\Software\CalixtoOptimizer\IntegrationTests\6e55acf9-2b7e-4581-95fb-3bd3b1f84684`, visão `Registry64`, valor único `ProbeValue`. Confirmou GUID ausente, chave nova vazia, leitura sem valor, criação e leitura de `REG_DWORD 0`, alteração e leitura de `REG_DWORD 1`, remoção apenas do valor e remoção da chave GUID vazia. O processo terminou código 0. A chave-pai `CalixtoOptimizer` e o nó `IntegrationTests` foram criados por não existirem e removidos após confirmar que estavam vazios. Uma consulta posterior confirmou ausência dos dois pais e do GUID. Nenhuma falha ou resíduo foi reportado.

O adaptador do runner é exclusivo do projeto de testes e está limitado ao GUID da execução mais `ProbeValue`; a escrita aceita apenas DWORD 0/1. Ele não habilita caminhos arbitrários no app nem injeta política de mutação no serviço do candidato. Nenhum valor sob `Policies` ou em `HideFileExt` foi lido ou alterado.

### Preferência funcional e política

O teste acima não chama `WindowsRegistryPreferenceAdapter`, não lê `HideFileExt`, não valida edição/build do Windows, não prova ausência de gerenciamento organizacional e não comprova que o Explorador aplicaria a preferência. A página WPF integra leitura sob demanda, consequências, compatibilidade, estado de gestão, consentimento/captura e histórico compartilhado. A página não oferece aplicar/restaurar; `BlockedRegistryMutationPolicy` continua padrão e o inspetor retorna `Unknown`. Nenhuma leitura ou mutação da preferência funcional ocorreu nesta etapa.

### Validação visual

O XAML e code-behind da página foram compilados na solução WPF, mas a janela não foi aberta nem inspecionada em screenshot. Logo, a aparência e os bindings visuais ainda não foram confirmados. A pendência anterior de inspeção visual de **Processos** continua. Nenhuma interação com mouse/teclado foi feita.

Build Release e os 85 testes simulados mais recentes estão registrados no final da seção correspondente em `docs/PROGRESS.md`. O teste do mecanismo Registry é real e separado desses testes simulados; ele não transforma os 85 casos em validação real de `HideFileExt`.

## Checklist manual consolidado da interface — Processos, Registro, Rede e Energia

Este é o checklist vigente para a próxima validação visual das quatro páginas. A interface não foi aberta nem observada nesta etapa, então todos os itens abaixo começam **pendentes**. Não use comandos de aplicação/restauração. Uma captura significa persistir dados no histórico do aplicativo; não aplica configuração e não é backup do Windows.

- [ ] Abra o Calixto Optimizer e navegue por **Processos → Registro e políticas → Rede → Planos/Energia**, voltando entre páginas. Confirme título, item selecionado, carregamento sem página vazia e retorno à página anterior. Se uma falha ocorrer naturalmente, leia a mensagem; não provoque falta de permissão ou falha de armazenamento.
- [ ] Em **Processos**, atualize a lista e selecione um processo comum que reconheça. Confira PID, criação, caminho quando disponível, estado e prioridade; prepare a prévia individual e leia a consequência. Registre uma captura somente se quiser validar a persistência no diário. Confirme que a tela indica revisão/captura e não oferece aplicação real.
- [ ] Em **Registro e políticas**, acione “Ler estado e preparar prévia”; confirme se o valor real foi lido ou se a leitura falhou, sem inferir compatibilidade a partir do teste GUID. Troque o alvo e confirme que a revisão anterior é invalidada; revise consequência/gestão `Unknown`; se quiser, registre captura e atualize o histórico. Não tente alterar nem restaurar `HideFileExt`.
- [ ] Em **Rede**, atualize os adaptadores e confira estados de link, endereços, gateway, DNS e o aviso sobre velocidade do enlace. O teste ativo é opcional; se o executar, use somente o destino `1.1.1.1` e até quatro sondagens pequenas já autorizadas, confira finalidade antes de iniciar e teste **Cancelar** durante a sequência. Registre amostras concluídas, timeout, RTT, perda e duração; ausência de resposta ICMP não prova falta de Internet. A página não tem captura/histórico de configuração.
- [ ] Em **Planos/Energia**, confirme que a demonstração é identificada como sintética. Use “Ler modo configurado” para a consulta AC/DC/plano ativo; escolha um alvo, confira consequências separadas por tomada/bateria e, se quiser, persista a captura. Confirme no histórico “captura persistida · não aplicada”. **Não marque as autorizações específicas, não digite frases e não clique em aplicar/restaurar.** VIDEOIDLE continua com aplicação bloqueada.
- [ ] Feche e reabra o app; atualize o histórico das páginas para confirmar que as capturas escolhidas permanecem e que a interface não as apresenta como otimizações realizadas ou como backup restaurável do Windows. Só marque “operação verificada” se houver uma operação real autorizada seguida da leitura de verificação correspondente; leitura/captura/consentimento não contam.

Este checklist não valida compatibilidade em outro computador. Erros de serviço/armazenamento simulados pelo harness também não comprovam como a mensagem de erro ficará visualmente; anote como pendente qualquer estado que não apareça durante a sessão manual.

### Execução desta revisão — 2026-10-19

- Diretório: `%USERPROFILE%\Downloads\CalixtoOptimizer`. SDK explicitamente chamado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402; Windows 10.0.26300, `win-x64`.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release`: sucesso, 0 avisos e 0 erros.
- `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: **85/85** checks aprovados; os checks de falhas/ajustes continuam sendo de modelo e adaptadores simulados.
- O build recompilou a solução WPF e, portanto, validou compilação XAML. Nenhuma janela foi aberta, screenshot obtido ou teste visual executado; os itens do checklist acima continuam pendentes.
- Nenhuma leitura de configuração, captura, sondagem de rede, aplicação, reversão ou mutação do Windows foi executada nesta revisão.

## Publicação autocontida e abertura pelo atalho — 2026-10-19

- Ambiente: raiz Git `<raiz do repositório>`, branch `main`, commit de origem `a5d2db890fc5a4f99373fecf16a5f31b4899c6d0`. SDK explicitamente usado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402; RID `win-x64`. Compilador Inno Setup 7.1.0.
- `dotnet publish` Release com `--runtime win-x64 --self-contained true` concluiu com sucesso. A publicação inclui os runtimes .NET 10.0.13 e WPF; o executável foi aberto diretamente da pasta `artifacts/preview-v0.6.0-experimental-selfcontained-20261019/CalixtoOptimizer`, sem usar o `bin` framework-dependent. Uma janela `Calixto Optimizer` apareceu e o processo respondeu. SHA-256 do executável publicado: `B0C3A45341764118569C08475AE91687E13946EE6DCA250D7EDC7A32AA633E9D`.
- O script único `packaging/CalixtoOptimizer-Preview-20261019.iss` compilou com sucesso no Inno Setup. O instalador novo e isolado foi instalado com código de saída 0 em `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019`. O hash do executável instalado corresponde ao publicado.
- O atalho do Menu Iniciar foi inspecionado: destino e diretório de trabalho apontam para essa instalação exclusiva. A inicialização feita pelo atalho produziu uma janela responsiva com título `Calixto Optimizer`; ela foi deixada aberta para a inspeção manual.
- O erro específico de runtime ausente não ocorreu na publicação autocontida nem na abertura pelo atalho. Isso valida somente a inicialização. As páginas não foram percorridas visualmente. Não foram executadas desinstalação, atualização de versão anterior ou publicação de Release. O instalador não é assinado.
- Build/publicação são resultados reais desta máquina. O harness `Optimizer.Core.Tests` Release passou **85/85** checks de modelo; esses checks não comprovam todas as páginas WPF nem comportamento em outro computador. Nenhuma otimização ou alteração de energia, Registro, rede, prioridade ou driver foi executada.

## Identificação Windows 10/11 — 2026-10-19

- Raiz: `<raiz do repositório>`; SDK explícito `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402 (único SDK em `--list-sdks`); `dotnet --info` reportou MSBuild 18.9.15, runtime 10.0.13; Windows `win-x64`.
- Build completo Release: `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore`: sucesso, 0 avisos e 0 erros. Harness `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: **91/91** checks aprovados, incluindo seis testes de identidade.
- Smoke de integração real chamou o `WmiComputerInventoryService` compilado por runner temporário fora do repositório, sem setters. Resultado: `WindowsName=Windows 11`, `Edition=Professional`, `Version=26H2`, `Build=26300.9711`, motherboard `ASUSTeK COMPUTER INC. PRIME B550M-A AC`; avisos sobre `ProductName` divergente e canal Insider não consultado. O Registro isolado reportou `ProductName=Windows 10 Pro`, `EditionID=Professional`, `DisplayVersion=26H2`, `CurrentBuildNumber=26300`, `UBR=9711`; CIM reportou caption `Microsoft Windows 11 Pro`, versão `10.0.26300`, build `26300`, produto cliente e `OperatingSystemSKU=48` (Professional).
- Confirmação independente somente leitura foi feita com `Get-CimInstance Win32_OperatingSystem` e leitura de `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`. A documentação Microsoft lista `26300.9711` como Windows 11 26H2 GA em 13/10/2026. `BuildLabEx` apresentou base `26100.1...ge_release`, que não foi usada como build atual nem como confirmação de canal.
- A saída do app identifica Windows 11 e conserva o aviso de divergência com `ProductName`. O aviso de canal Insider não verificado é intencional: a coleta não consulta a associação do dispositivo ao programa. Não foi feita captura de tela nem inspeção manual da página.
- Para o diagnóstico físico de rede, consulta PowerShell/PnP somente leitura confirmou placa PRIME B550M-A AC, Realtek Ethernet `PCI\VEN_10EC&DEV_8168&SUBSYS_86771043` e Wi-Fi `PCI\VEN_10EC&DEV_C821&SUBSYS_304A1A3B`; ambas com status `OK` e código de problema 0. Versão, provider e INF estão em `docs/DRIVER-ASSISTED-INSTALLATION.md`. IDs de instância completos, serial, hostname, usuário, IP e MAC foram omitidos.
- Nenhum driver foi baixado, instalado, substituído ou removido. O inventário ainda não comprova correspondência dos IDs aos INF dos pacotes ASUS; drivers candidatos e fluxo estão documentados em `docs/DRIVER-ASSISTED-INSTALLATION.md`. Não houve pacote de app novo nem alteração em energia, Registro, rede ou outras configurações.

## Candidatos e conferência offline de drivers — 2026-10-20

O harness Core cobre correspondência por Hardware ID exato, diferenciação de Compatible ID genérico, recusa de seleção quando há múltiplos dispositivos com o mesmo ID e reconhecimento de arquivo local pelos hashes revisados. Hash desconhecido permanece não validado. Esses testes usam dados simulados; não verificam assinatura, instalação ou funcionamento de drivers.

### Verificação real

- Raiz: `%USERPROFILE%\Documents\meu otimizador`; SDK chamado explicitamente em `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402.
- Build Release da solução: sucesso, 0 avisos e 0 erros. Harness: **94/94** checks aprovados.
- Consulta real do `WmiDriverInventoryService`: disponibilidade `Available`, 183 dispositivos e zero avisos. A lógica de candidato encontrou correspondência exata para Ethernet Realtek RTL8111H e Wi-Fi Realtek RTL8821CE. Windows reporta Ethernet Microsoft `1.0.0.14`/`rtcx21x64.inf` e Wi-Fi Microsoft `2024.0.8.108`/`netrtwlane.inf`.
- ZIPs oficiais locais em `artifacts/driver-review-20261020/`: SHA-256 LAN igual ao publicado pela ASUS; SHA-256 Wi-Fi calculado localmente, sem valor oficial publicado. CRC32 e comprimentos descompactados foram verificados para todas as entradas. Todos os catálogos `.cat` reportaram assinatura válida no Windows, publisher Microsoft WHCP; os INFs alvo foram confirmados como membros dos catálogos assinados usando `WinVerifyTrust` com `DRIVER_ACTION_VERIFY` (retorno zero).
- Os INFs conferem Hardware IDs exatos, arquitetura amd64 e alvos Windows compatíveis com o Windows 11 x64 build 26300.9711 observado. Não se testou instalação, atualização, rede após substituição, recuperação/desinstalação ou desempenho.
- A integração visual foi compilada pelo build WPF, mas a janela não foi aberta ou inspecionada. Nenhuma tela é declarada visualmente aprovada.
- A ação **Conferir ZIP local** calcula SHA-256 sem extrair ou executar. Só reconhece hashes dos arquivos revisados; hash desconhecido não é validado. Esse fluxo não prova o conteúdo de ZIPs diferentes.

## Correção do escopo INF e dependências — 2026-10-20

Este resultado substitui a conclusão Wi-Fi de “arquivos ausentes” registrada na primeira versão do analisador genérico. A lista antiga confundia todas as entradas de `[SourceDisksFiles]` com arquivos obrigatórios; a documentação Microsoft exige também a diretiva `CopyFiles` aplicável.

- SDK executado explicitamente: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão **10.0.402**; `global.json` permaneceu sem alterações.
- `dotnet build CalixtoOptimizer.sln --configuration Release --no-restore`: **sucesso**, 0 avisos e 0 erros. O primeiro build da rodada foi interrompido por bloqueio do `Optimizer.App.exe` pelo processo Release PID 9280 que eu havia aberto para inspeção; encerrei somente esse processo e repeti a compilação com sucesso.
- Harness `Optimizer.Core.Tests` Release: **103/103** checks de modelo aprovados. Novos casos verificam DDInstall aplicável e variante para build futuro (Include/Needs e arquivo ausente fora da variante não bloqueiam), dependência aplicável ausente e separação entre seção de instalação e IDs. Os testes são simulados; não chamam SetupAPI de instalação, PnPUtil ou setters.
- Probe real WMI: inventário `Available`, 196 linhas, zero avisos. Windows 11 Professional, versão exibida 26H2, build 26300.9711, amd64. O parser/assinatura analisou os ZIPs ASUS locais sem executar conteúdo.
- LAN: `rt68cx21x64.inf`, Hardware ID exato único, `RTL8168H.ndi.NT`, payload alcançável `rt68cx21x64.sys`; INF/catálogo e payload confirmados por SetupAPI/WinTrust. Include/Needs `pci.inf` → `PciIoSpaceNotRequired.HW` encontrado em `%WINDIR%\INF`. Resultado “compatível segundo as verificações realizadas”.
- Wi-Fi: `netrtwlane.inf`, Hardware ID exato único, `[ASUS8821ce_FCC_REG.ndi.NT]`, `CopyFiles=RTWlanE.CopyFiles` alcança `rtwlane.sys` e `rtldata.txt`; ambos existem e passam membership no catálogo. `netvwifibus.inf` e `machine.inf` existem em `%WINDIR%\INF`, e as seções Needs relevantes foram encontradas. Resultado “compatível segundo as verificações realizadas”.
- `rtlCoInst.dll` e `rtlCoInst.dat` não estão no ZIP, mas suas entradas em `[SourceDisksFiles]`/`[CoInstaller.CopyFiles]` não são alcançáveis pelas seções DDInstall aplicáveis ao Hardware ID, arquitetura e build deste PC. A conclusão anterior de bloqueio por essas ausências foi um falso bloqueio do analisador antigo. Nenhuma DLL avulsa foi baixada.
- Limite: o verificador de dependências confirma existência do INF Windows e das seções Needs diretas; não valida novamente a assinatura dessas dependências nem expande recursivamente suas próprias diretivas. O analisador continua conservador em construções não entendidas.
- Alcance da associação: `PnPUtil /install` pode atingir qualquer devnode correspondente, e o resumo lista todos os alvos do inventário na análise. A prévia não restringe o comando a um só dispositivo. Microsoft documenta `DiInstallDevice` para um devnode e driver pré-staged específicos; a API ainda não está integrada. Staging e associação reais permanecem bloqueados.
- Visual: abri a janela do executável Release atual e tentei capturá-la com CUA. O helper mapeou o HWND do build à identidade do Preview instalado e falhou duas vezes: `window id 279447742 no longer belongs to ...Optimizer-Preview-20261019\\Optimizer.App.exe; current owner is ...`. Depois de atualizar a lista, parei. A tela não foi validada visualmente; nenhuma ação de consentimento/histórico/instalação foi clicada. O executável atualizado foi reaberto para checklist manual.
- Nenhum ZIP/instalador novo foi produzido; R2 e seus hashes permanecem preservados. Nenhum driver foi staged, associado, instalado ou removido.

## Análise genérica e preparação bloqueada de driver — 2026-10-20

### Build e harness

- Raiz: `<raiz do repositório>`; SDK explicitamente chamado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão `10.0.402`.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore`: sucesso, 0 avisos/0 erros.
- `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: **100/100** verificações. Casos do pacote usam verificadores e instaladores falsos: ZIP comum/root-level, traversal, limite de tamanho, `Autorun.inf`, INF direto, Hardware ID exato, Compatible ID, conflito/ambiguidade, incompatibilidade, payload ausente, preview, captura e política padrão bloqueada. A prova de staging/associação no harness não chama PnPUtil.

### Verificações reais do Windows e dos arquivos locais

- Probe temporário leu WMI/PnP sem escrita: inventário `Available`, 193 linhas; Windows `10.0.26300.0`, x64 (`amd64`). IDs completos/identificadores pessoais não foram registrados neste relatório.
- `LocalDriverPackageAnalyzer` foi executado nos dois ZIPs ASUS ignorados pelo Git. LAN: 722 entradas, 34.802.917 bytes; hash coincide com valor ASUS, correspondência exata única, INF/catálogo/payload passaram nas APIs de assinatura/integridade, resultado “compatível segundo verificações”. Wi-Fi: 37 entradas, 31.111.699 bytes; hash local somente, Hardware ID exato e INF/catálogo válidos, mas `rtlCoInst.dll` e `rtlCoInst.dat` declarados no INF não estão no ZIP; resultado “análise incompleta”.
- Esses resultados reais não provam seleção de rank, instalação ou operação de hardware. Nenhum instalador OEM, PnPUtil ou API de instalação foi iniciado. O serviço de produção retornaria bloqueado antes de chamar o backend. Nenhuma alteração de driver/Windows foi feita.
- Compilação WPF não é validação visual; a janela não foi aberta para esta versão do código. A Preview R2 em uso foi preservada e não inclui estas mudanças.
- Instalação limpa, atualização funcional de driver, desinstalação/rollback de driver e teste em outro PC estão pendentes. Também seguem pendentes instalação limpa/desinstalação do Setup R2.

## Preview autocontido R2 — upgrade e validação visual (2026-10-20)

### Build e harness

- SDK explícito: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402; `global.json` permaneceu em 10.0.100 com `latestFeature`.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release`: passou, 0 avisos e 0 erros. Harness `Optimizer.Core.Tests`: **94/94** checks aprovados. São checks do modelo Core, com adaptadores simulados; não comprovam 94 operações reais do Windows.
- `dotnet publish` para `win-x64`, `--self-contained true`: 406 arquivos. Inno Setup 7.1.0 compilou `packaging/CalixtoOptimizer-Preview-20261020-r2.iss` com sucesso. A compilação não comprova instalação em outro PC.

### Upgrade real R1 para R2

- Instalador Preview R2 executado silenciosamente sobre a instalação Preview existente, por usuário; código de saída 0. Nenhum processo do app estava aberto para ser fechado. O app foi iniciado depois pelo executável instalado.
- 406 arquivos da publicação foram comparados ao instalado por SHA-256: sem divergências. Dois arquivos de histórico local tiveram nome, tamanho e SHA-256 idênticos antes/depois.
- O atalho `Calixto Optimizer Preview 20261020.lnk` foi conferido por leitura do `.lnk`; alvo `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019\Optimizer.App.exe`. O app abriu e permaneceu rodando. Instalação limpa e desinstalação R2 não testadas.

### Inspeção visual sem tomar o foco

Usei captura `PrintWindow` da janela enquanto ela estava atrás de outra janela, sem manipular mouse ou teclado físicos. A página Drivers mostrou 183 linhas WMI e os candidatos ASUS para Ethernet e Wi-Fi, com origem, status de correspondência e pendências. O fluxo de hash local foi exercitado com os dois ZIPs revisados no Preview anterior, cujo código de Drivers permaneceu idêntico ao R2. O app não extraiu, executou nem instalou os pacotes.

Processos carregou a lista local e relatou acesso negado/terminado por algumas entradas; nenhum processo foi encerrado e nenhuma prioridade foi alterada. Registro abriu após a correção WPF que fixa os bindings de texto como `OneWay`; não foi consultado nem alterado `HideFileExt`. Rede carregou adaptadores locais, mas não iniciou DNS/ICMP ativo. Planos/Energia mostrou leituras reais ainda não solicitadas e execução/restauração bloqueadas; nenhum getter/setter/captura foi chamado nesta inspeção. A página Drivers ficou aberta ao final. Capturas de Processo/Rede, que continham informação local, não foram versionadas.

### Artefatos

- Origem: commit `bb3c5eb60c310cbaaa18d20f370b67c7da10d81a`.
- ZIP: `artifacts/preview-v0.6.0-experimental-selfcontained-20261020-r2/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-portable.zip`; SHA-256 `20FAD64165F5166F5A7FDFB46BF30724ACBF2EE9BE649EEF07F10DA63031EF0D`.
- EXE: `artifacts/installer-preview-20261020-r2/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-setup.exe`; SHA-256 `ADE4D74A0C7E75E86BE70C9E722FCE758D0C01EABC73CE99911577DCF95966DD`.

Limites conhecidos: Setup sem assinatura digital própria; sem teste em outro PC, instalação limpa/desinstalação do R2 ou validação funcional de driver; ações reais de energia, Registro, políticas e prioridade permanecem bloqueadas pela política do aplicativo.

## Associação direcionada e dependências INF — 2026-10-20

### Verificações simuladas automatizadas

O harness Core usa INF temporários, sistema INF em pasta descartável, inventário simulado e `IDriverPackageInstaller` falso. A cobertura inclui Include/Needs recursivo, nome de seção decorado literal, ciclos, dependência ausente, `CopyINF` alcançável, entradas vazias, `AddSoftware`, registro de co-installer e limites; seleção única de melhor rank, empate/ausência/rank inferior; remoção do dispositivo, mudança de identidade, staging recusado, associação recusada e pedido de reinício. O caso de reinício permanece pendente e não afirma que o INF novo está ativo. Esses testes não chamam `SetupCopyOEMInfW`, `DiInstallDevice`, PnPUtil, co-installers nem alteram drivers.

Resultado desta execução com SDK 10.0.402: a tentativa normal de build falhou após retries porque o apphost Release estava em uso pelo PID 24540, preservado para inspeção manual. `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore -p:UseAppHost=false` compilou toda a solução com 0 erros e 1 aviso MSB3061 (não conseguiu excluir o apphost em uso). A DLL do harness foi executada diretamente com `dotnet.exe`: **109/109 checks simulados aprovados**.

O probe real dos ZIPs ASUS foi repetido em modo somente leitura após a mudança. Inventário WMI: `Available`, 189 itens, zero avisos; Windows 11 Professional, build 26300.9711, x64. Ethernet `rt68cx21x64.inf` e Wi-Fi `netrtwlane.inf` ainda retornam “Compatível segundo as verificações realizadas” com uma correspondência exata e prévia elegível; Include/Needs do Windows foram percorridos recursivamente. Não foram chamados SetupCopyOEMInf, DiInstallDevice, PnPUtil ou instaladores OEM.

### Build não equivale a integração real

O build Release compila WPF, P/Invokes e os contratos entre Core/Windows. Não valida execução das APIs nativas, desenho da UI, permissões elevadas, Driver Store ou funcionamento de dispositivo. A aplicação mantém `BlockedDriverPackageMutationPolicy`; a compilação não libera staging nem associação.

### Checklist manual visual pendente

O Windows confirmou PID **24540** em `<raiz do repositório>\src\Optimizer.App\bin\Release\net10.0-windows\Optimizer.App.exe`. `sky.list_apps` retornou uma janela `Optimizer.App.exe`, mas a identidade informada pela ferramenta era `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019\Optimizer.App.exe`. Parei antes de `get_window`, captura ou clique e não contornei a checagem. A validação visual fica pendente; o executável Release permanece aberto para checklist manual. Use este checklist sem clicar em staging/associação:

- [ ] Drivers: identificar candidato/INF e motivos; conferir `Include`/`Needs`, resultado parcial/incompleto e aviso de que staging afeta o Driver Store da máquina.
- [ ] Prévia: confirmar devnode, driver atual, pacote escolhido, rank/recusa, consequências e limites de recuperação. A associação é direcionada a um devnode, mas efeitos INF podem ultrapassar o alvo.
- [ ] Histórico: distinguir revisão/captura, staging, associação, reinício pendente, driver ativo lido e associação verificada. Consentimento/captura não são instalação.
- [ ] As demais páginas do checklist manual consolidado já existente continuam pendentes se não forem observadas nesta compilação; não provocar sondagem de Rede nem aplicar ajustes de Energia/Registro/Processos.

## Restrições da lista PnP nesta etapa

O adaptador define `DI_FLAGSEX_ALLOWEXCLUDEDDRVS` para que `SetupDiBuildDriverInfoList` enumere a lista compatível PnP. A flag inclui drivers `ExcludeFromSelect` na lista, e a documentação Microsoft alerta que dispositivos PnP normalmente têm essa condição e não devem ser instalados manualmente. Nenhuma chamada de staging ou associação foi executada; compatibilidade/rank selecionado em operação nativa continuam sem validação.

Nenhum staging, associação, instalação, remoção ou reinício foi executado nesta etapa. Nenhum ZIP/instalador foi gerado. A validação funcional dos drivers ASUS, recuperação/desinstalação e teste em outro PC continuam pendentes.
