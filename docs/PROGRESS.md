# Progresso

Última atualização: 2026-10-19
Base das etapas anteriores: c150a51430e603f4bc2ca9f0536383c337efab37. Continuação atual iniciada no commit 6138ef27d69c3ac305f32e1bbb515f29433ed757; matriz atual na seção 17.
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
- Revisão do diff/segredos, commit `77cddf1dc67d9838a3823b0a8ab045314b1bebc5` e push para `origin/main` concluídos. A inspeção visual do painel, instalação/desinstalação 0.6.0 e testes em outro PC permanecem pendentes; qualquer aplicação/restauração real exige aprovação específica.

## 14. Matriz oficial, SDK e validações pendentes — 2026-10-19

Base examinada: commit 88e03ea8aa34a30c2952516cc25a9c4c47907253, árvore limpa no início da revisão. Nenhum patch de código futuro foi incorporado nem alterado nesta etapa. Os estados iniciais do SDK e do instalador foram atualizados após autorização na seção 15.

### Diagnóstico do SDK

Os comandos foram executados em PowerShell nesta máquina Windows x64. dotnet.exe resolve para C:\Program Files\dotnet\dotnet.exe.

| Diretório da execução | global.json encontrado | dotnet --info | dotnet --list-sdks |
| --- | --- | --- | --- |
| %USERPROFILE%\Documents\Codex\2026-10-19\cara-eu | Nenhum no diretório nem nos pais | Host 8.0.21 x64; nenhum SDK; runtime Microsoft.NETCore.App 8.0.21; exit 0 | Saída vazia; exit 0 |
| %USERPROFILE%\Downloads\CalixtoOptimizer | global.json do repositório: SDK 10.0.100, rollForward=latestFeature, prerelease falso | Mesmo host/runtime 8.0.21, nenhum SDK instalado; identificou o global.json do repo; exit 0 | Saída vazia; exit 0 |

No diretório do repositório, dotnet build não começou: a CLI não encontrou SDK compatível. Não foi alterado global.json nem foi instalado SDK.

**Proveniência 0.6.0:** os registros históricos das fases 4 e 9 dizem que o SDK portátil .NET 10.0.402 produziu builds anteriores. A publicação 0.6 contém runtime .NET e Windows Desktop 10.0.13; a página oficial lista o SDK 10.0.402 com esses runtimes, o que é consistente com o pacote. Assim, 10.0.402 é a identificação mais sustentada para o build/publicação 0.6, mas a atribuição exata não pode ser provada: nenhum binlog/manifesto de proveniência foi guardado com os artefatos. Runtime 10.0.13 não é, por si só, o número do SDK. Para confirmar de forma reproduzível, aguarda-se aprovação para instalar o SDK oficial x64 10.0.402, permitido pelo latestFeature sem mudar o alvo declarado. Fontes: [downloads oficiais do .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) e [regras de seleção de SDK de global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

### Matriz de todas as fases sugeridas no plano oficial

| Fase | Estado | Evidência e pendência que determina o estado |
| --- | --- | --- |
| 0 — Ambiente e decisões | **Parcial** | SDK 10.0.402 agora está instalado em pasta do usuário e build/testes locais passaram; a proveniência histórica exata do pacote 0.6 segue sem binlog/manifesto. Compatibilidade em outro PC permanece. |
| 1 — Inventário e drivers somente leitura | **Concluído localmente** | WMI/PowrProf reais foram comparados no PC de desenvolvimento; 179 dispositivos/drivers e 5 planos foram vistos; exportação sanitizada e filtros têm cobertura. Não demonstra compatibilidade em outro PC. |
| 2 — Motor, planos, diário e simulação | **Concluído no escopo simulado** | Catálogo tipado, perfis, prévia, consentimento, diário e recuperação simulada existem; 54 checks foram registrados como aprovados. Nenhum desses testes fake prova escrita real. |
| 3 — Primeiro ajuste real reversível | **Bloqueado para validação real** | Código técnico VIDEOIDLE e modo AC/DC existe; capturas reais são somente leitura. Ciclo real de aplicação/verificação/reversão não foi autorizado nem executado nesta etapa. |
| 4 — Energia, Registro, políticas e processos | **Parcial** | Energia tem leitura/captura e fluxo com confirmação; Registro, políticas e prioridade de processos continuam indisponíveis. Escrita de energia permanece sem validação real. |
| 5 — Rede e GPU compatível | **Não iniciado** | Há propostas documentadas; não há diagnóstico de rede nem integração que configure preferência de GPU. Inventariar um adaptador não equivale a um módulo de rede/GPU. |
| 6 — Pós-formatação e drivers assistidos | **Parcial** | Identificação de drivers foi validada localmente. Seleção de fontes oficiais, análise e instalação assistida de INF, e seleção/instalação de programas não existem. |
| 7 — Publicação, instalador e recuperação | **Parcial** | ZIP/Setup 0.6 foram compilados e identificados por hash; instalação 0.5.0, upgrade 0.6.0 e desinstalação passaram em diretório de teste neste PC. Compatibilidade em outro PC e recuperação prática permanecem pendentes. |
| 8 — Controles extremos específicos por hardware | **Não iniciado** | Não há matriz de CPU/GPU/placa-mãe, integração de fabricante, licença e validação por modelo. O documento de candidatos não constitui suporte implementado. |

Esta matriz não declara o produto completo. Empacotar não conclui as fases de funcionalidade, validação ou recuperação.

### Validação visual

- A visão geral de diagnóstico foi aberta e observada.
- A navegação para Planos encontrou um estilo XAML ausente; a correção foi incorporada antes do commit 0.6.
- Após reabrir, a automação detectou entrada do usuário na janela. Os cliques foram interrompidos para não disputar o controle. Leitura de modo, consequências, consentimentos e histórico ainda não foram inspecionados visualmente de ponta a ponta.
- Checklist manual curto consta em docs/TESTING.md; a execução deve deixar aplicação e restauração sem autorização/sem clique.

### Instalação, atualização e desinstalação 0.6.0

O roteiro em docs/TESTING.md foi preparado antes da autorização e executado conforme a seção 15. A instalação por usuário usa o mesmo AppId nas versões 0.5 e 0.6, grava em LocalAppData/Programs, cria atalho e chave de desinstalação HKCU e não abre o app automaticamente. O script não define a seção UninstallDelete. O histórico persistente está em %LOCALAPPDATA%\CalixtoOptimizer\History; o código não possui armazenamento separado de configurações do usuário. A comparação de histórico pode ser testada; preservação de configurações inexistentes não pode ser alegada.

Na data deste registro, nenhuma instalação, atualização ou desinstalação 0.6 havia sido executada; veja o resultado posterior na seção 15. Nenhum setter de energia foi chamado.

### Próxima menor entrega de desenvolvimento proposta

Após fechar a reprodutibilidade do SDK, a menor fatia nova prevista é a fase 5: um diagnóstico de conectividade, RTT e perda para destino informado pelo usuário, somente leitura, com resultado inconclusivo explícito quando ICMP estiver bloqueado. Não escrever DNS, MTU, TCP, RSS, offloads ou propriedades da NIC. Isso não repete inventário, motor simulado ou modo de energia. Nenhum código foi iniciado nesta etapa.

## 15. SDK reproduzido e ciclo do instalador 0.6.0 — 2026-10-19

Esta seção atualiza o diagnóstico inicial da seção 14 após as autorizações recebidas.

### SDK, build e testes

- WinGet estava disponível, mas não encontrou a versão exata 10.0.402 no catálogo consultado. Usei o script oficial da Microsoft, https://dot.net/v1/dotnet-install.ps1, documentado em https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script, para instalar SDK 10.0.402 x64 em %LOCALAPPDATA%/dotnet-sdk-10.0.402 com -NoPath. Não foi alterado o PATH permanente nem global.json.
- dotnet --list-sdks usando esse executável lista 10.0.402. dotnet --info mostra SDK 10.0.402, MSBuild 18.9.15, host/runtime 10.0.13, Windows x64 build 26300. Restore/build/test foram executados no diretório do repositório Downloads, chamando esse dotnet.exe explicitamente. A seleção no repo respeita global.json 10.0.100 com latestFeature.
- dotnet restore CalixtoOptimizer.sln: êxito.
- dotnet build CalixtoOptimizer.sln --configuration Release --no-restore: êxito, 0 avisos e 0 erros.
- dotnet run --project tests/Optimizer.Core.Tests/Optimizer.Core.Tests.csproj --configuration Release --no-build: 54/54 model checks aprovados; os adaptadores de escrita continuam simulados.
- O build novo confirma compilação local pelo SDK 10.0.402, mas não recria ZIP/EXE nem prova retrospectivamente qual SDK gerou cada pacote anterior. Não houve republicação.

### Instalação, upgrade e desinstalação reais

A autorização foi executada apenas em %LOCALAPPDATA%/Programs/CalixtoOptimizer-Test. A pré-checagem não encontrou instalação com o mesmo AppId, processo do app, diretório de teste ou atalho correspondente. Uma cópia temporária do histórico foi mantida até a comparação terminar.

| Operação | Resultado verificado |
| --- | --- |
| Instalar setup 0.5.0 | Exit code 0; Optimizer.App.exe versão 0.5.0.0; DisplayVersion 0.5.0-experimental; uma chave de desinstalação e um atalho. |
| Atualizar para setup 0.6.0 | Exit code 0; executável versão 0.6.0.0; DisplayVersion 0.6.0-experimental; uma chave e um atalho no mesmo destino. |
| Desinstalar 0.6.0 | Exit code 0; removidos diretório de teste, chave HKCU e atalho; nenhum processo do app permaneceu. |
| Preservação do histórico | Um arquivo, 609 bytes; manifesto e SHA-256 antes/depois idênticos. Cópia temporária e logs foram removidos após a comparação aprovada. |

- SHA-256 do setup 0.5.0 usado: D13B0A31222EA213444E7995B761AB67590420BCC28B7DE5E1E5E2B2BECFB19B, 46.681.264 bytes.
- SHA-256 do setup 0.6.0 usado: 1C90177F72F6DDBF89587D3CBADB044A8339EAC96A7AC3DE1C20F6E463223A12, 46.707.153 bytes.
- Nenhum dos apps foi iniciado; os testes não avaliaram a UI, migração de configurações separadas (o app não tem esse store) ou compatibilidade em outro PC.
- Nenhum setter de energia, registro de ajustes, serviço, política, rede, driver ou prioridade foi chamado.

A matriz da seção 14 foi atualizada para refletir o SDK e o ciclo de instalação. A fase 7 continua parcial: teste de setup passou neste PC, mas não conclui recuperação prática nem compatibilidade em outro computador. A próxima menor fatia continua o diagnóstico somente de leitura de conectividade/RTT/perda da fase 5, ainda não iniciado.

## 16. Diagnóstico de rede somente leitura — 2026-10-19

### Base e implementação

- Continuação do commit `57c1bf3d714f6809f33eb9479dbc575bc75c0511`, com árvore limpa antes das alterações. A navegação **Rede** estava indisponível.
- `Optimizer.Core/Network` agora contém contratos/modelos e classificador testável. Leitura local falha, ausência de interface operacional com endereço não loopback, falha de DNS, timeout sem resposta, outros erros ICMP, resposta parcial e cancelamento são estados separados.
- `WindowsNetworkDiagnosticsAdapter` consulta `NetworkInterface.GetAllNetworkInterfaces`, `Dns.GetHostAddressesAsync` e `Ping.SendPingAsync` assíncronos. Endereços MAC não são lidos. A enumeração pode incluir loopback, túnel e interfaces virtuais.
- `NetworkPageViewModel` e `Views/NetworkPage.xaml` integram inventário e teste à navegação existente. A navegação apenas lê inventário local. DNS/ICMP só começam pelo botão explícito, com destino editável e finalidade visíveis. O teste seleciona um IP resolvido, envia no máximo 4 sondagens sequenciais de 32 bytes, com timeout individual de 1.000 ms, atualiza progresso e aceita cancelamento; resultados parciais são mantidos. Não há download ou gravação de configuração.
- A UI apresenta link speed como velocidade do enlace reportada pelo adaptador, nunca como banda contratada/velocidade medida da internet. Ping sem resposta declara explicitamente que bloqueio ICMP/rota/host podem explicar o resultado e que isso não prova falta de internet.
- Nenhuma otimização, setter de energia, mudança DNS/adaptador/firewall/Registro/política/rede, instalador ou pacote de distribuição foi executado ou gerado nesta etapa.

### Arquivos alterados

- Núcleo: `src/Optimizer.Core/Network/NetworkModels.cs`, `NetworkDiagnosticsService.cs`.
- Windows: `src/Optimizer.Windows/WindowsNetworkDiagnosticsAdapter.cs`.
- Interface: `src/Optimizer.App/ViewModels/NetworkPageViewModel.cs`, `MainWindowViewModel.cs`, `MainWindow.xaml`, `Views/NetworkPage.xaml`, `Views/NetworkPage.xaml.cs`.
- Verificação/documentação: `tests/Optimizer.Core.Tests/Program.cs`, `README.md`, `docs/ARCHITECTURE.md`, `docs/TESTING.md`, este arquivo.

### Build e testes simulados

- Ambiente: PowerShell em `Downloads/CalixtoOptimizer`; SDK explicitamente chamado em `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`; `dotnet --version` = `10.0.402`; `dotnet --list-sdks` lista somente `10.0.402` nesse caminho. `global.json` preservado em 10.0.100, `latestFeature`.
- Restore da solução: êxito.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore`: êxito, 0 avisos e 0 erros.
- `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: 63/63 checks aprovados. Os nove novos testes usam adaptadores simulados; não emitem tráfego.

### Leitura real local e comparação nativa

- Um harness temporário invocou o leitor real do aplicativo. Resultado agregado: 33 interfaces; 6 com estado `Up`; 1 `Up` com endereço não loopback; 1 `Up` com gateway; 2 `Up` expondo DNS; 0 avisos. Identificadores e endereços locais foram omitidos deste registro.
- `Get-NetAdapter -IncludeHidden`: 10 adaptadores (`Up=1`, `Disconnected=5`, `Not Present=4`). `Get-NetIPConfiguration -All`: 6 configurações; 1 `Up` com gateway e DNS.
- A única interface operacional com endereço não loopback/gateway concordou com a configuração `Up` do cmdlet. O total bruto difere porque .NET também enumera interfaces virtuais, loopback e túneis. DNS em interfaces `Up` teve diferença (2 na lista .NET contra 1 perfil `Up` no cmdlet); a UI conserva o dado reportado sem inferir conectividade. A leitura real não fez consulta DNS do destino nem teste ICMP.
- Comparação/fonte técnica: APIs oficiais Microsoft [.NET NetworkInterface](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface.getallnetworkinterfaces), [DNS assíncrono com cancelamento](https://learn.microsoft.com/en-us/dotnet/api/system.net.dns.gethostaddressesasync?view=net-10.0) e [Ping assíncrono com timeout/cancelamento](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0).

### Estado da fase, limitações e próximo marco

- A fase 5 oficial **passa de não iniciada para parcial**: diagnóstico de rede local e teste explícito DNS/ICMP estão implementados; compatibilidade GPU continua não iniciada. A fase 5 não está concluída.
- Implementação/compilação da interface foi validada, mas a página não foi aberta para inspeção visual automatizada. O checklist manual está em `docs/TESTING.md`; não foi usado mouse nem alterado foco do usuário.
- Os resultados ativos reais de DNS/ICMP, cancelamento físico, destinos que bloqueiam ICMP, interface offline e testes em outro PC continuam pendentes. A suíte cobre essas classes somente por simulação.
- Proposta de próximo marco na época: investigar GPU; foi atendida parcialmente pela etapa documentada na seção 17, sem integração de escrita/leitura da preferência pelo app.

## 17. Validação de rede e investigação de GPU — 2026-10-19

Continuação direta de `6138ef27d69c3ac305f32e1bbb515f29433ed757`; árvore estava limpa antes desta etapa. A matriz abaixo substitui o estado resumido da seção 14 e as previsões da seção 16. Ela preserva a diferença entre diagnóstico, capacidade de configuração e execução real.

### Rede: teste real autorizado

- Foi usado `NetworkDiagnosticsService` com o `WindowsNetworkDiagnosticsAdapter` já empregado pelo aplicativo. Destino literal `1.1.1.1`; quatro ICMP Echo Requests sequenciais de 32 bytes, timeout de 1.000 ms cada. Não houve DNS nem download.
- Resultado observado: 4/4 respostas; 0 timeouts; 0 falhas; perda amostral 0%; RTTs 16, 17, 18 e 16 ms; tempo total 130 ms. Isso só confirma respostas ao destino durante essas quatro amostras. Não mede largura de banda nem estabelece disponibilidade geral da internet. O limite autorizado foi respeitado; não foi chamado `ping.exe`, pois isso acrescentaria tráfego além do limite.
- O teste foi executado pelo serviço real em harness, não pelo botão da interface. O fluxo interativo na UI ainda depende da validação visual manual; não foi aberta janela para não disputar mouse/teclado.
- Comparação nativa de inventário de adaptadores está registrada na seção 16. Nenhuma comparação nativa independente do resultado ICMP foi feita devido ao limite de quatro pacotes.

### GPU: limites verificados e acesso nativo

- Reutilizado `WmiComputerInventoryService` e sua consulta já existente `Win32_VideoController.Name`; a consulta local atual retornou inventário disponível, 1 nome de controlador reportado, 0 avisos. O nome bruto não foi publicado. A classe documenta controladores, mas não prova qual GPU renderiza um processo; em drivers sem WDDM ela pode reportar valores imprecisos.
- A documentação oficial confirma o URI `ms-settings:display-advancedgraphics` como página nativa para preferências de gráficos, sujeito a dispositivo compatível. Adicionados à visão geral botões explícitos para abrir Configurações de gráficos e Gerenciador de Tarefas. O primeiro abre consulta/configuração nativa do usuário; o app não grava essa preferência.
- A interface DXGI `IDXGIFactory6::EnumAdapterByGpuPreference` permite ao próprio aplicativo ordenar adaptadores conforme uma preferência escolhida para criar dispositivo Direct3D. Não lê a preferência persistida de outro app. Não foi validada API pública/estável para o Calixto ler a preferência por app configurada no Windows; não se usa chave privada do Registro.
- O uso gráfico corrente por processo é observável na coluna **GPU Engine** do Gerenciador de Tarefas. É uma observação momentânea e separada da preferência configurada. O Calixto não coleta essa telemetria programaticamente. Os novos atalhos não foram abertos/observados visualmente.
- Fontes Microsoft: [WMI Win32_VideoController](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller), [URI oficial de Configurações](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings), [preferência gráfica por app](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11), [DXGI GPU preference](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/nf-dxgi1_6-idxgifactory6-enumadapterbygpupreference), [GPU Engine no Task Manager](https://learn.microsoft.com/en-us/surface/surface-book-gpu-overview).

### Matriz completa e atual do plano oficial

| Fase oficial | Diagnóstico | Configuração / geração de plano | Execução real | Estado global e evidência |
| --- | --- | --- | --- | --- |
| 0 — Ambiente e decisões | **Concluído nesta máquina**: Windows x64, SDK e build identificados. | **Parcial**: versões-alvo e regras registradas; ambiente limpo/segundo PC pendente. | **Parcial**: build/testes locais possíveis; compatibilidade externa não verificada. | **Parcial** — SDK 10.0.402 local; artefatos anteriores sem proveniência reproduzível completa; outro PC pendente. |
| 1 — Inventário de sistema, dispositivos e drivers | **Concluído localmente**: WMI/PowrProf; 179 dispositivos/drivers, cinco planos; códigos PnP e exportação sanitizada. Controlador gráfico reportado reusado do inventário. | **Não aplicável**: inventário não edita estes estados. | **Somente leitura verificada** neste PC; nenhum ajuste decorrente aplicado. | **Concluído localmente** no escopo consultado; outras versões/OEMs e outro PC pendentes. Não há comparação de versão contra catálogo externo. |
| 2 — Catálogo, compatibilidade, perfis, consentimento, diário e simulação | **Parcial**: regras de compatibilidade e prévia usam estado observado; catálogo real continua pequeno. | **Concluído em simulação**: perfis leve/equilibrado/extremo/personalizado, consequência, consentimento e plano simulado. | **Não habilitada por padrão**; nenhum plano simulado é escrito no Windows. | **Concluído no escopo simulado**, não como otimizador executável. O modo extremo não aplica tudo automaticamente; snapshots sintéticos não recuperam Windows. |
| 3 — Primeiro ajuste real reversível | **Concluído localmente em leitura**: PowrProf e getter de modo configurado lidos; capturas persistidas testadas. | **Parcial**: fluxo VIDEOIDLE/energia e consequências codificados; política de escrita fica negada. | **Bloqueada**: VIDEOIDLE/modo de energia não aplicados nem revertidos; execução exige aprovação específica posterior. | **Parcial** — leitura/captura verificadas; ciclo de escrita real não validado e não autorizado. |
| 4 — Módulos de energia, Registro, políticas, processos e rede | **Parcial**: planos/modos de energia e adaptadores/rede têm diagnóstico; não há diagnóstico equivalente para todos os demais domínios. | **Parcial**: fluxo de energia codificado, mas bloqueado; módulos de Registro, políticas, processos e configuração de rede indisponíveis. | **Bloqueada/ausente**: nenhum setter de energia; nenhuma escrita de Registro, política, prioridade ou rede. | **Parcial** — energia e rede apenas no escopo diagnóstico/fluxo bloqueado; Registro/políticas/processos e ajuste de rede não iniciados. |
| 5 — Integrações GPU compatíveis | **Parcial**: WMI local reporta um controlador; preferência por app consultável no Windows; uso corrente pode ser observado no Gerenciador de Tarefas. | **Não implementada pelo Calixto**: sem API pública/estável validada para ler preferência de outros apps; não escrever Registro. | **Nenhuma**: sem alteração de preferência nem telemetria GPU do produto. | **Parcial como encaminhamento nativo** — controlador reportado, preferência configurada e GPU usada são estados distintos. |
| 6 — Pós-formatação, fontes e instalação assistida de drivers/programas | **Parcial**: identificar drivers e códigos PnP localmente. | **Não iniciada**: seleção/verificação de pacotes oficiais e plano de instalação assistida não existem. | **Não iniciada/bloqueada pela ausência de módulo**: nenhum INF/driver/programa instalado. | **Parcial** — leitura existe; atualização/instalação assistida e seleção de programas continuam pendentes. |
| 7 — Publicação portátil, instalador e recuperação | **Parcial**: conteúdo/hashes e logs dos pacotes revisados. | **Parcial**: ZIP/Setup 0.6 compilados em estágio anterior; pacotes atuais não contêm estas alterações. | **Parcial local**: instalação 0.5, upgrade para 0.6 e desinstalação executados; sem instalação do build atual/segundo PC. | **Parcial** — empacotamento não conclui funcionalidade; recuperação prática e validação em outro PC pendentes. |
| 8 — Controles extremos específicos por hardware | **Não iniciado**: nenhuma matriz/validação por CPU/GPU/placa/OEM. | **Não iniciado**: sem integração fabricante/firmware. | **Bloqueada/não autorizada**: nenhuma escrita de BIOS, firmware, tensão ou frequência. | **Não iniciado**; candidatos documentados não são suporte implementado. |

### Build, testes e mudança do repositório

- SDK escolhido explicitamente: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402; `global.json` 10.0.100 com `latestFeature` mantido. Diretório das execuções: `%USERPROFILE%\Downloads\CalixtoOptimizer`.
- Alterações de código nesta etapa: `src/Optimizer.App/Views/OverviewPage.xaml` e `.xaml.cs`; documentação em `README.md`, `docs/ARCHITECTURE.md`, `docs/TESTING.md` e `docs/PROGRESS.md`.
- Restore da solução: êxito; build Release final: êxito, 0 avisos e 0 erros; harness: 63/63 checks aprovados, incluindo os nove casos simulados de rede da etapa anterior. Uma primeira tentativa do build encontrou falta de `using System.IO` no code-behind dos atalhos; foi corrigida antes do build final. `dotnet --info` mostrou MSBuild 18.9.15, runtime 10.0.13 e Windows x64 build 26300. Não foram adicionados testes de UI automatizados.
- Não foi recriado ZIP/instalador e nenhuma release foi publicada. Não houve alterações em energia, rede, GPU, Registro, políticas, processos, serviços, drivers ou firmware; os únicos pacotes enviados foram as quatro sondagens ICMP autorizadas.

### Pendências e próxima fatia

- Revisar visualmente a página Rede e testar cancelar/resultado parcial manualmente; conferir os novos atalhos GPU sem abrir janela nesta validação; outro PC permanece pendente.
- Manter como pendentes: aplicação/reversão reais de energia, VIDEOIDLE, módulos de Registro/políticas/processos, configuração de rede, leitura programática confiável da preferência GPU, telemetria GPU do produto, drivers assistidos, validação por hardware e recuperação prática.
- Próxima menor entrega prevista após build/teste: não escrever preferências GPU. Fazer uma validação visual manual curta da visão geral (inventário/atalhos) e da página Rede; depois retomar o próximo módulo funcional que o plano prioriza, mantendo ajustes reais bloqueados por consentimento e validação individual.

## 18. Módulo de processos e prioridade individual — 2026-10-19

Continuação do commit `4e359a10258ed648da68c2da1c30765e405f5dd3`; o branch estava limpo antes desta etapa. As alterações anteriores de compatibilidade/consentimento/diário foram necessárias para aceitar um plano real revisável sem transformar consentimento em autorização de escrita.

### O que foi implementado

- `WindowsProcessPriorityAdapter` enumera processos e tenta ler nome, PID, instante de criação, caminho do executável quando acessível e `PriorityClass`. A UI diferencia disponível, parcial, acesso negado, encerrado, ausente e falha; caminho inacessível não apaga os demais campos lidos.
- A identidade persistida e revalidada é PID + criação UTC + caminho quando disponível na captura. Nome é apenas rótulo. Se processo acaba ou PID passa a outra instância, aplicação e restauração param; nunca buscam um novo alvo só pelo PID.
- Um alvo por plano. Os destinos são Ocioso, Abaixo do normal, Normal, Acima do normal e Alta. Tempo real não aparece no seletor nem é aceito como alvo; uma prioridade atual Tempo real bloqueia a preparação.
- O plano usa `AdjustmentPlanService`/`ProfilePlanService`, consequência por processo e consentimento existente. `CaptureAsync` grava o plano, recibo e estado anterior no `JsonOperationJournalStore` já usado pelo app em `%LOCALAPPDATA%\CalixtoOptimizer\History`.
- Aplicação exige política de mutação e confirmação de operação separadas do consentimento. A política padrão injetada no app nega tudo e a tela não oferece setter. Serviço tem caminhos de aplicação/verificação e restauração condicional para testes/injeção futura; falhas ou conflito geram eventos no diário. Um resultado incerto nunca aciona rollback automático.
- O enum do diário preserva os valores numéricos antigos 0–6 para não reinterpretar histórico anterior. Novos estados foram acrescentados depois desses valores.
- Adicionado `tests/Optimizer.ProcessProbe`, um pequeno processo descartável que mostra PID/criação/caminho e espera Enter. Não define prioridade, não foi lançado nem testado em execução real.

O mecanismo suportado é `System.Diagnostics.Process.PriorityClass`, que representa classe de escalonamento da CPU. O setter documentado requer `PROCESS_SET_INFORMATION`; acesso por usuário/administrador não é presumido. Prioridade maior não garante FPS; Classe Alta pode consumir quase todo o tempo de CPU e causar atraso em outros aplicativos. O catálogo e as fontes estão em `docs/INITIAL-CATALOG.md`; arquitetura em `docs/ARCHITECTURE.md`.

### Arquivos alterados

- Núcleo: `src/Optimizer.Core/Processes/ProcessPriorityModels.cs`, `ProcessPriorityOperationService.cs`.
- Adaptador Windows: `src/Optimizer.Windows/WindowsProcessPriorityAdapter.cs`.
- UI: `src/Optimizer.App/ViewModels/ProcessesPageViewModel.cs`, `MainWindowViewModel.cs`, `MainWindow.xaml`, `Views/ProcessesPage.xaml`, `Views/ProcessesPage.xaml.cs`; histórico comum em `ProfilesPageViewModel.cs` e `Views/ProfilesPage.xaml`.
- Motor compartilhado: `AdjustmentModels.cs`, `AdjustmentCatalog.cs`, `AdjustmentPlanService.cs`, `PlanConsentService.cs`, `OperationJournalModels.cs`.
- Probe: `tests/Optimizer.ProcessProbe/Optimizer.ProcessProbe.csproj`, `Program.cs`; solução `CalixtoOptimizer.sln`.
- Testes e docs: `tests/Optimizer.Core.Tests/Program.cs`, `README.md`, `docs/INITIAL-CATALOG.md`, `docs/ARCHITECTURE.md`, `docs/TESTING.md`, este arquivo.

### Build, testes, consulta real e validação visual

- Execuções em PowerShell no diretório `%USERPROFILE%\Downloads\CalixtoOptimizer`, chamando explicitamente `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`; versão `10.0.402`. `global.json` preservado em `10.0.100` com `latestFeature`.
- Build final `dotnet build CalixtoOptimizer.sln --configuration Release`: êxito, todos os projetos incluindo WPF e probe, 0 avisos e 0 erros.
- Harness `dotnet run --project tests/Optimizer.Core.Tests/Optimizer.Core.Tests.csproj --configuration Release --no-build`: **72/72** checks aprovados. Novos cenários exclusivamente simulados cobrem identidade/plano/consentimento, acesso negado, processo encerrado, conflito de prioridade, PID reutilizado entre leituras, PID reutilizado durante restauração, persistência falha antes do setter, falha de diário após setter simulado sem rollback, restauração condicional, conflito externo e ausência de escrita com a política padrão.
- Uma execução intermediária dos testes apontou uma expectativa incorreta do estado de recuperação de uma captura sem mutação; a expectativa foi corrigida e a suíte completa passou depois. Uma tentativa intermediária de build não encontrou `System.IO` no ViewModel; o `using` foi corrigido antes do build final.
- **Dados reais:** nenhum processo foi enumerado neste marco; nenhum PID, caminho ou prioridade real foi coletado. Nenhum setter de prioridade foi chamado. O probe foi compilado, não iniciado.
- **Validação visual:** pendente. A página não foi aberta nesta sessão para evitar assumir o controle do mouse/teclado. Checklist manual curto está em `docs/TESTING.md`.
- Não foram gerados ZIP/instalador nem pacotes de distribuição. Não houve alterações no Windows.

### Matriz atualizada de todas as fases oficiais

Esta tabela substitui as classificações anteriores onde houve progresso e conserva pendências que não pertencem a este marco.

| Fase oficial | Diagnóstico | Configuração / plano | Execução real | Estado e evidência |
| --- | --- | --- | --- | --- |
| 0 — Ambiente e decisões | Parcial: SDK/build desta máquina identificados. | Parcial: regras/versões registradas. | Parcial: apenas o ambiente local validado. | **Parcial**; segundo PC e origem reproduzível de artefatos anteriores pendentes. |
| 1 — Inventário de sistema, dispositivos e drivers | Concluído localmente no escopo consultado, inclusive planos de energia, PnP e inventário de drivers. | Não aplicável para diagnóstico. | Somente leitura verificada no computador atual. | **Concluído localmente**; outros OEMs/Windows e comparação externa de versões pendentes. |
| 2 — Catálogo, compatibilidade, perfis, consentimento e diário | Parcial: leitura/previews reais em alguns módulos. | Concluído em simulação e planos revisáveis de modo de energia/processos; consentimento específico persistido. | Bloqueada por padrão; simulação não escreve. | **Parcial** como motor geral; catálogo real pequeno e recuperação automática permanece indisponível. |
| 3 — Primeiro ajuste reversível | Leitura/captura de VIDEOIDLE e modo configurado de energia foram validadas localmente. | Parcial: consequência, snapshot, aplicação/reversão condicional codificados. | Bloqueada; nenhuma alteração de energia executada. | **Parcial**; teste de escrita real e recuperação prática pendentes. |
| 4 — Energia, Registro, políticas, processos e rede | Energia/rede já têm diagnóstico; processos agora têm leitura integrada, mas não foram consultados ao vivo nesta entrega; Registro/políticas não têm diagnóstico equivalente. | Processos têm plano individual/captura no motor; energia/modo ainda bloqueados; Registro/políticas sem plano implementado. | Processos e energia bloqueados por padrão; Registro/políticas/rede não têm execução. | **Parcial**; o módulo de processos está validado somente por fake; candidatos de Registro/política seguem indisponíveis. |
| 5 — GPU compatível | WMI reporta controlador; Configurações nativas encaminham preferência/uso; integração programática de preferência não disponível. | Não há configuração pelo Calixto. | Nenhuma. | **Parcial como diagnóstico/encaminhamento**, sem integração de preferência ou telemetria de uso. |
| 6 — Pós-formatação, fonte e instalação assistida de drivers/programas | Identificação de drivers localmente verificada. | Seleção de pacotes oficiais/planos de instalação não iniciada. | Nenhum driver/programa instalado. | **Parcial**; instalação assistida, seleção de programas e validação em outro PC pendentes. |
| 7 — ZIP, instalador e recuperação | Hashes de pacotes anteriores registrados. | Pacotes 0.5/0.6 existem; pacote atual não foi gerado. | Instalar/atualizar/desinstalar versões anteriores testado neste PC; build atual não testado pelo instalador. | **Parcial**; recuperação prática ampla e outro PC pendentes. |
| 8 — Controles específicos por hardware | Não iniciado. | Sem integração fabricante/firmware validada. | Não autorizada e indisponível. | **Não iniciado**; nenhum firmware/tensão/frequência escrito. |

### Pendências preservadas e próximo passo

- Validar visualmente Rede, atalhos nativos de GPU e agora Processos com checklist manual; não houve inspeção visual nova.
- Fazer um teste real futuro com o probe é uma etapa separada. O próximo passo é abrir o probe, capturar a identidade e ler a prioridade atual somente para revisão; depois será solicitada aprovação específica para uma alteração e uma restauração condicionais. Até essa aprovação, a política bloqueada permanece.
- Continuam pendentes: mutações reais de energia/VIDEOIDLE; Registro; políticas; ajuste de rede; leitura programática de preferência/telemetria GPU; drivers/programas assistidos; benchmark de desempenho; instalações de pacotes atuais; recuperação em outro PC.
- Próximo marco funcional sugerido após revisão visual: testar o fluxo com o probe apenas em leitura e então submeter a solicitação de aprovação do primeiro teste real de prioridade. Não iniciar outro ajuste de desempenho nem gerar pacote antes de revisar este marco.
