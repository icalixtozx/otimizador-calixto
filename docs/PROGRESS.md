# Progresso

Última atualização: 2026-10-19
Base preservada: c150a51430e603f4bc2ca9f0536383c337efab37.
Versão de desenvolvimento: 0.6.0-experimental (ZIP e instalador locais gerados). A tag e os artefatos 0.5.0 permanecem no commit de release anterior. Não validado em outro computador.

## 1. Validação do diagnóstico local

Somente consultas de leitura foram executadas. O processo não estava elevado.

- powercfg nativo listou cinco planos. A lista e os nomes devolvidos por PowrProf coincidiram; o plano ativo também coincidiu.
- O plano ativo apresentou VIDEOIDLE em 180 s para AC e 180 s para DC pelo aplicativo e por powercfg. O adaptador de prévia do aplicativo também leu 180 s AC. Sem divergência nos valores comparados.
- PowrProf respondeu sem avisos/erros. A interface não altera nem ativa planos.
- WMI retornou o inventário de computador e 179 linhas de dispositivos/drivers, sem avisos. Neste momento, nenhuma linha trouxe código PnP positivo; nenhuma ficou sem metadados correspondentes de driver.
- Uma validação temporária executou os serviços reais WMI e PowrProf e construiu o JSON em memória. O relatório omitiu todos os 179 IDs PnP e todos os cinco GUIDs dos planos. Nenhum identificador ou inventário bruto foi publicado.
- Um teste PowerShell isolado não conseguiu carregar System.Management por incompatibilidade de versão no host PowerShell; a validação foi repetida com um harness .NET que referencia os projetos e as dependências corretas e concluiu com êxito.
- A consulta real não comprova comportamento em outros PCs, todos os dispositivos possíveis, nem execução de ajuste.

## 2. Funcionalidades implementadas

- Inventário PowrProf somente leitura: enumera nomes, identifica plano ativo e lê VIDEOIDLE AC/DC em segundos.
- Inventário de dispositivos PnP associado aos metadados de driver retornados por Win32_PnPSignedDriver. Dados sem metadados são diferenciados de ausência comprovada; códigos aparecem apenas como valores reportados pelo Windows.
- A tela não classifica versões antigas/desatualizadas porque não há catálogo externo comparável.
- Exportação JSON sanitizada com prévia modal antes de salvar. Omite IDs PnP, GUIDs de planos, nome de conta/computador, números de série, endereços de rede e caminhos locais. A prévia contém modelos e nomes de hardware/driver; revisar antes de compartilhar.
- Exportação CSV de drivers permanece disponível e omite IDs.
- Não há telemetria nem envio automático do JSON.
- VIDEOIDLE continua como teste técnico; aplicação e reversão seguem bloqueadas por padrão e sem botões na UI.

## 3. Arquivos desta etapa

- Core: Inventory/PowerPlanInfo.cs, Inventory/SanitizedDiagnosticReportBuilder.cs e Inventory/DriverInfo.cs.
- Windows: WindowsPowerPlanInventoryService.cs e WmiDriverInventoryService.cs.
- WPF: MainWindowViewModel.cs, OverviewPageViewModel.cs, DriversPageViewModel.cs, MainWindow.xaml, OverviewPage.xaml/.cs, DriversPage.xaml, DiagnosticExportPreviewWindow.xaml/.cs e DriverStatusConverters.cs.
- Testes: tests/Optimizer.Core.Tests/Program.cs.
- Documentação e distribuição: README.md, docs/DIAGNOSTICS.md, docs/PROGRESS.md, packaging/README-INSTALADOR.md, packaging/README-PACOTE.txt, packaging/CalixtoOptimizer.iss e metadados de versão.
- Artefatos sob artifacts/ são ignorados pelo Git.

## 4. Build e testes

O dotnet global não localiza o SDK solicitado por global.json. Foi usado o SDK portátil .NET 10.0.402 já existente no workspace, compatível com rollForward latestFeature.

- dotnet build CalixtoOptimizer.sln --configuration Release: êxito, 0 avisos e 0 erros.
- Harness Optimizer.Core.Tests: 39/39 aprovados. São testes de modelo e adaptadores falsos; não provam aplicação/reversão real.
- Harness temporário local: serviços WMI/PowrProf reais e teste de omissão de identificadores; êxito, não foi mantido no repositório.
- Nenhuma alteração de energia, Registro, política, rede, serviço, prioridade, driver ou firmware foi feita.

## 5. Pacote experimental

- ZIP: artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip
- Publicação autocontida: artifacts/release-v0.5.0-experimental/CalixtoOptimizer
- ZIP: 65.449.733 bytes, 406 entradas.
- SHA-256: 1947C4DA73EFC897C76D8EC42E774D2B2C265975FD3FB5D2ED3D6804BCF92C2E.
- Conteúdo verificado: Optimizer.App.exe, runtimeconfig e README-PACOTE.txt.
- ZIP foi produzido e conferido; não representa teste de instalação ou compatibilidade em outro PC.

## 6. Instalador e publicação

- Inno Setup 7.1.0 x64 é a versão estável listada oficialmente em 2026-10-19. O ID WinGet confirmado pelo fornecedor é JRSoftware.InnoSetup.7. A consulta winget show encontrou o manifesto disponível, mas a atualização da fonte falhou.
- ISCC.exe não está instalado nos caminhos comuns nem no PATH. O usuário precisa confirmar/realizar uma única instalação interativa; instrução em packaging/README-INSTALADOR.md.
- Script .iss atualizado, mas não compilado; o instalador EXE não foi produzido.
- Instalação e desinstalação não foram testadas.
- Não foi criada GitHub Release: sem EXE do instalador e sem CLI gh de publicação disponível. ZIP e código local não foram apresentados como Release.
- Fonte, assinatura, licença e comandos do Inno Setup estão documentados com links oficiais em packaging/README-INSTALADOR.md.

## 7. Bloqueios, aprovação e próximos passos

- Pedido pendente para eventual aplicação única do VIDEOIDLE AC: plano ativo, estado lido 180 s, alvo proposto 900 s. Isto aumentaria de 3 para 15 minutos o tempo até desligar o monitor por inatividade quando ligado à tomada. Não toca DC nem outros valores. Não foi aplicado.
- A reversão implementada é condicional: só aceita o mesmo plano ativo com VIDEOIDLE AC ainda no alvo 900 s e tenta restaurar o valor capturado (180 s), verificando por leitura. Se plano/valor divergir ou ocorrer falha/interrupção, a recuperação fica manual e exige revisão. Não há compare-and-swap; ainda há uma pequena janela para corrida externa.
- O app continua sem expor comandos de aplicação/reversão e a política injetada nega escrita.
- Ação necessária para o instalador: usuário disponibilizar Inno Setup pelo comando documentado; depois compilar .iss e validar existência/hash do EXE. A instalação/desinstalação precisam de testes dedicados em Windows de teste.
- Em outro PC ainda pendem compatibilidade da UI, WMI/PowrProf, Modern Standby/OEM, exportação, pacote portátil e instalação/desinstalação.


## 8. Continuação de distribuição — 2026-10-19

- HEAD revisado antes desta atualização: c313e6d453970e64bbfbc553200041d725f4438d; árvore limpa. O ZIP experimental existente já havia sido verificado e não contém relatório diagnóstico.
- O usuário autorizou instalar Inno Setup pelo procedimento oficial, sem alterar configurações do Windows; VIDEOIDLE e valores atuais foram preservados, sem nova consulta ou gravação nesta etapa.
- Executado o comando oficial interativo `winget install --id JRSoftware.InnoSetup.7 -e -s winget -i`. WinGet encontrou Inno Setup 7.1.0, mas a janela aguarda seleção de idioma. A segunda tentativa terminou em 0x80070020 porque a primeira ainda usava o executável temporário. A interface não está acessível para interação nesta sessão. Não afirmar instalação do Inno Setup/ISCC.
- Por consequência, o .iss não foi compilado, o EXE do instalador não foi produzido e instalação/desinstalação do Calixto Optimizer não foram executadas. Nenhuma GitHub Release foi publicada.
- Não houve alterações em configurações, serviços, Registro, políticas, energia, rede, drivers, prioridades, firmware ou VIDEOIDLE.
- Propostas, documentação técnica, compatibilidade, consequências, privilégios, captura, reversão e avaliação: `docs/PERFORMANCE-CANDIDATES.md`. Apenas propostas, sem aplicação.
- Instruções para upload manual do ZIP e futuro EXE experimental pelo GitHub, com o repositório privado identificado: `docs/RELEASE-EXPERIMENTAL.md`.
- Último build/teste local do código, no commit acima: Release com 0 avisos/0 erros e 39/39 testes do harness. Esta etapa só adiciona documentação; `git diff --check` foi executado após as edições com código 0; Git informou apenas normalização de LF para CRLF em dois arquivos.
- Próximo passo: concluir a janela do Inno Setup e responder “pronto”; então localizar ISCC, compilar o .iss, conferir conteúdo e SHA-256, atualizar este registro e pedir aprovação separada antes de executar o instalador do app para testar instalação/desinstalação.


## 9. Verificação após documentação — 2026-10-19

- Build Release executado com o SDK portátil .NET 10.0.402: êxito, 0 avisos e 0 erros.
- Harness Optimizer.Core.Tests: 39/39 aprovados. Os casos de adaptadores/falhas são simulados e não aplicam mudanças ao Windows.
- ZIP conferido novamente: 65.449.733 bytes, 406 entradas; contém Optimizer.App.exe, Optimizer.App.runtimeconfig.json e README-PACOTE.txt; SHA-256 1947C4DA73EFC897C76D8EC42E774D2B2C265975FD3FB5D2ED3D6804BCF92C2E.
- Não existe arquivo em artifacts/installer e não há ISCC confirmado. O assistente oficial continua aberto aguardando idioma/termos; ainda não foi possível compilar o instalador.
- O build e os testes acima não comprovam instalação, desinstalação ou compatibilidade em outro PC. Nenhuma Release foi publicada.


## 10. Inno Setup instalado e distribuição compilada — 2026-10-19

Este registro substitui os estados intermediários dos itens 8 e 9 quanto à disponibilidade do compilador e à existência do EXE. Os resultados antigos descrevem o momento anterior à conclusão do assistente interativo.

- O usuário concluiu o assistente do Inno Setup. ISCC.exe 7.1.0 está em %LOCALAPPDATA%\\Programs\\Inno Setup 7\\ISCC.exe. Get-AuthenticodeSignature retornou Valid; certificado Pyrsys B.V.
- dotnet publish Release para win-x64 autocontido: exit 0. A pasta publicada contém 406 arquivos e 147.082.618 bytes, incluindo Optimizer.App.exe, .runtimeconfig.json, .deps.json, coreclr.dll, hostfxr.dll e README-PACOTE.txt.
- Primeira compilação Inno revelou aviso de identificador x64 depreciado. Após conferir documentação oficial, packaging/CalixtoOptimizer.iss passou a usar x64compatible em ArchitecturesAllowed e ArchitecturesInstallIn64BitMode.
- Compilação final com Inno Setup 7.1.0: exit 0, “Successful compile”, 0 avisos e 0 erros, 406 entradas comprimidas.
- Instalador produzido: artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe; 46.692.928 bytes; SHA-256 EF1D03A6518079BE12843FD0CD2B433F747F99660B22427B6FE591EC7C7D2B9B. Assinatura Authenticode do setup: NotSigned. Não executar/publicar como assinado.
- ZIP regenerado da publicação: artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip; 65.449.730 bytes; 406 entradas; SHA-256 586CD10AE589985FA84E8EF6C306D6CDB815B2995A2907F767FAA93567C1476A. Arquivos principais verificados.
- Build Release repetido: êxito, 0 avisos e 0 erros. Harness Optimizer.Core.Tests: 39/39 aprovados; são testes locais, incluindo simulações, e não validam instalação/desinstalação.
- O instalador do Calixto Optimizer não foi executado. Instalação/desinstalação ficam pendentes de aprovação explícita do usuário. Não houve mudanças de configurações de energia, Registro, política, rede, serviços, prioridades, drivers, firmware ou VIDEOIDLE.
- Nenhuma GitHub Release foi publicada. Procedimento manual pelo site em docs/RELEASE-EXPERIMENTAL.md; o repositório permanece privado.
- O motor Inno informou “Non-commercial use only”. O build é para o projeto experimental/pessoal; uso comercial futuro requer verificar/obter a licença comercial aplicável do fornecedor.
- As três propostas para desempenho permanecem documentadas, não implementadas e não aplicadas, em docs/PERFORMANCE-CANDIDATES.md.

## 11. Pacotes finais e teste real de instalação — 2026-10-19

Este registro atualiza a distribuição do item 10 com a documentação final e o teste aprovado pelo usuário. O item 10 descreve o estado anterior à autorização do teste de instalação.

- O README-PACOTE foi revisado e copiado para a publicação autocontida; as duas cópias possuem SHA-256 idêntico `D480B3AFD7E1B09A8875707D344BF5CACED236A7828BC15BB5C7ED5720D6E679`. O ZIP foi regenerado com essa versão e tem 406 entradas; sua listagem confirma executável, runtime, dependências e README atualizado.
- O Inno Setup 7.1.0 recompilou `packaging/CalixtoOptimizer.iss`: exit 0, compilação bem-sucedida, 0 avisos e 0 erros. Foram incorporados os 406 arquivos da publicação.
- ZIP final: `artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip`, 65.449.812 bytes, SHA-256 `20704611C9967EBE3C47EB809C4079E2BC09227804A00759C25B1EB5C5EB916C`.
- Instalador final: `artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe`, 46.681.264 bytes, SHA-256 `D13B0A31222EA213444E7995B761AB67590420BCC28B7DE5E1E5E2B2BECFB19B`; assinatura Authenticode do EXE: `NotSigned`.
- Teste real de instalação do EXE final no diretório temporário `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Test`: exit 0; encontrados 408 arquivos (406 do payload mais os auxiliares do desinstalador), executável e runtimes presentes, entrada de desinstalação em HKCU com versão e localização esperadas e atalho do Menu Iniciar criado.
- Teste real de desinstalação por `unins000.exe`: exit 0; pasta de teste removida, entrada de desinstalação removida e atalho removido. O aplicativo não foi iniciado. A primeira checagem de localização da entrada precisou normalizar a barra final de `InstallLocation`; a verificação corrigida passou.
- O teste temporário criou e removeu apenas os arquivos, a entrada de desinstalação e o atalho do aplicativo. VIDEOIDLE foi preservado; nenhuma configuração do Windows ou driver foi alterado.
- Build Release: 0 avisos/0 erros. Harness core: 39/39 aprovados. Isso não substitui teste em outro PC; instalação/desinstalação continuam pendentes em outro computador e não há validação ampla de compatibilidade.
- Nenhuma GitHub Release foi publicada. Instruções para o upload manual de ZIP e EXE como pre-release: `docs/RELEASE-EXPERIMENTAL.md`. Os três candidatos de desempenho continuam apenas propostas documentadas em `docs/PERFORMANCE-CANDIDATES.md`; nenhum foi implementado ou aplicado.


## 12. Modo de energia do usuário (desenvolvimento 0.6.0) — 2026-10-19

### Estado da base e distribuição 0.5.0

- A etapa continuou do commit `a60a20419206cad515a24aee5defeae60adb3510`, que contém o preparo e o push da tag `v0.5.0-experimental`. Essa tag permanece no commit anterior a este recurso.
- ZIP e instalador 0.5.0 permanecem os artefatos já testados e documentados acima; não foram substituídos. A GitHub pre-release não foi publicada nesta sessão. O envio manual pelo site continua documentado em `docs/RELEASE-EXPERIMENTAL.md`; selecione a tag existente e anexe os dois arquivos listados lá.
- A versão do código em `main` e o script Inno foram avançados para `0.6.0-experimental` para não rotular este código novo como o conteúdo do pacote 0.5.0. Nenhum pacote 0.6 foi publicado nem gerado.
- Tentativa de preparar a Release pelo navegador encerrou antes de qualquer clique porque o controle do navegador não conseguiu identificar a URL atual. Nenhum anexo foi carregado, formulário enviado ou Release criada; os passos manuais permanecem em `docs/RELEASE-EXPERIMENTAL.md`.

### API confirmada e implementação

- A Microsoft documenta `PowerGet/SetUserConfiguredACPowerMode` e `PowerGet/SetUserConfiguredDCPowerMode` para desktop Windows 11. Os três GUIDs suportados são Melhor eficiência, Equilibrado e Melhor desempenho. A preferência configurada é um voto/sinal que pode ser sobreposto por outros sinais do sistema. Ela é separada do plano ativo, que usa `PowerGet/SetActiveScheme`.
- Arquivos de motor/adaptador: `src/Optimizer.Core/Adjustments/PowerModeOperationModels.cs`, `PowerModeOperationService.cs`, `JsonPowerModeOperationStore.cs` e `src/Optimizer.Windows/WindowsPowerModeAdapter.cs`.
- Arquivos de UI: `src/Optimizer.App/ViewModels/PowerModePanelViewModel.cs`, `ProfilesPageViewModel.cs`, `MainWindowViewModel.cs`, `Views/ProfilesPage.xaml`, `MainWindow.xaml` e `Optimizer.App.csproj`.
- Arquivo de cobertura: `tests/Optimizer.Core.Tests/Program.cs`.
- A UI lê valores AC/DC sob demanda, mostra compatibilidade e consequência por lado, permite escolher alvos e salvar captura persistida local separada do histórico sintético. O fingerprint inclui operação/valores; a captura não é backup do Windows. Os seletores só habilitam um lado se a leitura do mesmo lado for suportada.
- O serviço de motor exige consentimento de cada lado selecionado, confirmação com frase vinculada à operação/fingerprint e política explicitamente habilitada. Antes da chamada nativa persiste intenção; compara o estado anterior, verifica cada resultado por getter e registra falha/conflito como não verificado. Restauração é condicional e recusa se o atual for um terceiro estado. AC e DC são independentes.
- O aplicativo sempre instancia `BlockedPowerModeMutationPolicy`; UI não contém comando de aplicação/restauração. Checkboxes na tela servem à revisão de consequência; não autorizam setter. Alterações reais permanecem bloqueadas até aprovação específica do usuário após apresentação de plano.

### Verificação executada

- Build completo Release: `dotnet build CalixtoOptimizer.sln --configuration Release` — êxito, 0 avisos e 0 erros.
- Harness local: 50/50 checks aprovados. Inclui adaptadores simulados para consentimento, captura AC/DC, aplicação/reversão, conflito, interrupção, falha parcial, falha de gravação, captura adulterada e estado persistido desconhecido. Esses casos não chamam setters do Windows.
- Smoke test somente leitura do `WindowsPowerModeAdapter` no PC local: Windows build 26300; `ApiAvailable=True`; AC=`Equilibrado`; DC=`Equilibrado`. O probe chamou somente os getters. Nenhum setter de modo de energia foi chamado.
- Nenhuma configuração de Windows, plano, Registro, política, rede, serviço, driver, prioridade ou firmware foi alterada. Não foi medido desempenho e nenhum ganho de FPS é alegado.

### Limites e próximo passo

- Aplicação e restauração reais não foram executadas; portanto não são verificações reais. O histórico persistido real de captura ainda não foi criado nesta máquina, apesar do fluxo e cobertura simulada implementados.
- Compatibilidade e permissões de escrita em outro hardware/OEM/build não foram testadas. O retorno por getter confirma preferência configurada, não modo efetivo; outros sinais podem sobrepor. As APIs não oferecem compare-and-swap, então a corrida entre última leitura e setter permanece possível.
- Para primeira execução quando houver aprovação, proposta de plano: reler estado; capturar somente AC; manter DC intocado; alvo AC “Melhor desempenho”; consentimento e confirmação específicos; verificar AC por getter; reverter para o AC capturado (“Equilibrado”) somente se AC ainda estiver no alvo. Conflito ou estado ambíguo encerra a automação e exige revisão manual. Nenhum passo desse plano foi iniciado.
- Candidatos GPU continuam propostos e não implementados. Este ciclo conclui apenas o recurso de modo de energia; não há módulos novos em paralelo.

## 13. Captura integral, confirmação explícita e pacotes 0.6.0 — 2026-10-19

### Implementação

- A leitura de modo de energia agora inclui os getters documentados AC/DC e `PowerGetActiveScheme`; plano ativo e preferência configurada são estados separados.
- Capturas novas usam esquema 2 e persistem valores anteriores AC e DC mais o GUID do plano ativo, mesmo se o alvo selecionado for de um lado. Captura exige todos os três estados legíveis. Capturas v1 seguem disponíveis como histórico, mas são bloqueadas para execução/restauração por não terem baseline completo.
- Aplicação exige atualização de leitura; se AC, DC ou plano ativo diferirem da captura, a UI pede nova leitura/captura/revisão e não chama setter. O serviço repete a comparação e relê antes de cada setter.
- Verificação exige o modo alvo no lado selecionado, o modo capturado no lado não selecionado e o plano ativo capturado após cada setter e na leitura final. Restauração tem confirmação própria, compara o estado esperado por etapas e recusa terceiro estado ou mudança do plano.
- A UI diferencia `Revisei` (somente revisão) de consentimento/autorização real. Uma ação futura exige aceite por consequência, caixa de autorização da ação e frase exata. O serviço continua negando por padrão; somente o comando explícito cria uma política temporária para aquela chamada. Nenhum comando foi acionado nesta etapa.
- A navegação para Planos revelou `XamlParseException`: estilo `SecondaryButton` ausente. Foi definido no tema preto/branco e incluído no build/publicação final. Não foi possível terminar a inspeção visual do painel: a automação do Windows detectou entrada do usuário e o navegador ficou em primeiro plano; nenhum clique adicional foi feito. A primeira tela de diagnóstico foi aberta e visualizada, mas o fluxo de modo de energia, consequências e histórico não é declarado visualmente validado.

### Validação com dados reais somente leitura

- Em 2026-10-19 às 23:18:25 UTC, uma leitura nova pelo `WindowsPowerModeAdapter` retornou AC=`Balanced`/`Equilibrado`, DC=`Balanced`/`Equilibrado` e um GUID do plano ativo. Os valores foram lidos nesta etapa, não reutilizados da observação antiga.
- Um registro real esquema 2 foi gravado com alvos iguais ao estado acabado de ler, de modo que a captura não descreveu uma mudança. O aplicativo de smoke usou apenas `ReadConfiguredModes` e `CaptureAsync`; nenhum setter foi invocado.
- O processo terminou; um segundo processo carregou o registro por `JsonPowerModeOperationStore.LoadAsync`. Identidade, esquema, AC/DC, plano e fingerprint foram aceitos. A captura está em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealPowerMode`. Ela não é backup do Windows.
- A consulta inicial de inventário do app mostrou quatro planos e marcou um ativo. O modo AC/DC não foi lido automaticamente ao abrir a tela; a consulta ocorreu sob demanda no smoke de captura. Não houve alteração de energia.

### Build, testes e distribuição

- `dotnet build CalixtoOptimizer.sln --configuration Release`: êxito; 0 avisos, 0 erros.
- `dotnet run --project tests/Optimizer.Core.Tests/Optimizer.Core.Tests.csproj --configuration Release --no-build`: 54/54 checks aprovados. Cenários de aplicação, conflito, preservação DC/plano, interrupção e reversão usam adaptadores falsos; não são validação de setters reais.
- Tentativa de repetição ao fechar esta revisão: não iniciou, pois global.json exige SDK 10.0.100 e este ambiente agora só tem runtime Microsoft.NETCore.App 8.0.21, sem SDK instalado. O build e os 54 checks citados acima foram executados com sucesso após a última mudança de código; as alterações posteriores foram apenas documentação e hashes de pacotes. Não instalei outro SDK.
- `dotnet publish ... --runtime win-x64 --self-contained true`: êxito; 406 arquivos, 147.173.185 bytes. Runtime .NET Desktop incluído.
- Inno Setup 7.1.0 compilou `packaging/CalixtoOptimizer.iss` com “Successful compile”; compilador indicou “Non-commercial use only”.
- ZIP: `artifacts/CalixtoOptimizer-0.6.0-experimental-win-x64.zip`, 65.483.173 bytes, 406 entradas, app/README presentes e probe temporário ausente; SHA-256 `2ADC4EADBDF8309D0F1DD5416FB4FE452E7EDA5428A8C591DFD7D98D3C27E712`.
- Instalador: `artifacts/installer/CalixtoOptimizer-0.6.0-experimental-win-x64-setup.exe`, 46.707.153 bytes; SHA-256 `1C90177F72F6DDBF89587D3CBADB044A8339EAC96A7AC3DE1C20F6E463223A12`; assinatura Authenticode `NotSigned`.
- Instalação/desinstalação 0.6.0 não foram executadas. Nenhuma GitHub Release foi publicada. Os artefatos 0.5.0 e a tag `v0.5.0-experimental` não foram alterados.

### Limites e próximo passo

- Nenhum setter de modo de energia, VIDEOIDLE ou plano foi chamado. Não houve alteração de energia, Registro, políticas, serviços, rede, drivers, prioridade ou firmware.
- O fluxo completo de modo de energia precisa de inspeção visual com o app em primeiro plano; esse teste ficou pendente por entrada do usuário no Windows. A captura e a integridade do arquivo foram validadas em processos separados, não pela tela de histórico.
- Instalação/desinstalação 0.6.0 e compatibilidade em outro computador continuam pendentes. Também faltam teste real aprovado de aplicação e restauração; elas não foram autorizadas nesta etapa.
- Próximos passos: a revisão, o commit e o push desta etapa estão sendo fechados nesta continuação. A inspeção visual do painel, instalação/desinstalação 0.6.0 e testes em outro PC permanecem pendentes; qualquer aplicação/restauração real exige aprovação específica.
