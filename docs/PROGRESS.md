# Progresso

Última atualização: 2026-10-20
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
| 4 — Energia, Registro, políticas, processos e rede | Energia/rede têm diagnóstico; leitura real de identidade/prioridade e captura do probe foram verificadas pelo adaptador; UI de processos ainda sem validação visual. Registro/políticas sem diagnóstico equivalente. | Processos têm plano individual e captura persistida; energia/modo permanecem bloqueados; Registro/políticas sem plano. | Ciclo real de prioridade validado somente no probe descartável aprovado; política padrão do app segue bloqueada. Energia sem escrita; Registro/políticas/rede sem execução. | **Parcial**; o teste de processo comprova somente este caminho e esta instância; UI, outros processos, energia, Registro e políticas seguem pendentes. |
| 5 — GPU compatível | WMI reporta controlador; Configurações nativas encaminham preferência/uso; integração programática de preferência não disponível. | Não há configuração pelo Calixto. | Nenhuma. | **Parcial como diagnóstico/encaminhamento**, sem integração de preferência ou telemetria de uso. |
| 6 — Pós-formatação, fonte e instalação assistida de drivers/programas | Identificação de drivers localmente verificada. | Seleção de pacotes oficiais/planos de instalação não iniciada. | Nenhum driver/programa instalado. | **Parcial**; instalação assistida, seleção de programas e validação em outro PC pendentes. |
| 7 — ZIP, instalador e recuperação | Hashes de pacotes anteriores registrados. | Pacotes 0.5/0.6 existem; pacote atual não foi gerado. | Instalar/atualizar/desinstalar versões anteriores testado neste PC; build atual não testado pelo instalador. | **Parcial**; recuperação prática ampla e outro PC pendentes. |
| 8 — Controles específicos por hardware | Não iniciado. | Sem integração fabricante/firmware validada. | Não autorizada e indisponível. | **Não iniciado**; nenhum firmware/tensão/frequência escrito. |

### Pendências preservadas e próximo passo

- Validar visualmente Rede, atalhos nativos de GPU e agora Processos com checklist manual; não houve inspeção visual nova.
- Fazer um teste real futuro com o probe é uma etapa separada. O próximo passo é abrir o probe, capturar a identidade e ler a prioridade atual somente para revisão; depois será solicitada aprovação específica para uma alteração e uma restauração condicionais. Até essa aprovação, a política bloqueada permanece.
- Continuam pendentes: mutações reais de energia/VIDEOIDLE; Registro; políticas; ajuste de rede; leitura programática de preferência/telemetria GPU; drivers/programas assistidos; benchmark de desempenho; instalações de pacotes atuais; recuperação em outro PC.
- Próximo marco funcional sugerido após revisão visual: testar o fluxo com o probe apenas em leitura e então submeter a solicitação de aprovação do primeiro teste real de prioridade. Não iniciar outro ajuste de desempenho nem gerar pacote antes de revisar este marco.

## 19. Probe de processos: leitura real e preparação do teste (2026-10-19)

- O probe descartável `tests/Optimizer.ProcessProbe/bin/Release/net10.0/Optimizer.ProcessProbe.exe` foi iniciado sem carga intensiva e ficou aguardando Enter. O primeiro início pelo host padrão falhou porque o caminho global oferecia apenas .NET 8; o início autorizado funcionou ao apontar `DOTNET_ROOT` somente para o runtime .NET 10.0.13 já instalado no perfil do usuário. Nenhuma ferramenta ou configuração do sistema foi alterada.
- A consulta local `Get-Process` confirmou PID, instante UTC de criação, caminho do executável e `PriorityClass=Normal`; `Responding=True`. Os valores de PID, instante e caminho com perfil de usuário foram omitidos deste arquivo versionado. O instante e o caminho conferiram com a saída do próprio probe. A consulta foi feita por PowerShell, não pela tela do Calixto Optimizer.
- O probe encerrou com código 0 ao receber Enter; uma consulta posterior confirmou que aquele PID não continuava ativo. Nenhum setter de prioridade foi chamado. Não houve alteração de prioridade ou outra configuração do Windows.
- Plano técnico pretendido para uma nova instância, condicionado a leitura nova: somente o probe, `Normal → BelowNormal → Normal`. A captura de aplicação deve gravar `PreviousPriority=Normal`, `TargetPriority=BelowNormal`, identidade completa e fingerprint antes de qualquer escrita. Imediatamente antes da escrita, a identidade e prioridade precisam continuar iguais à revisão. Depois, verificar `BelowNormal`. Restaurar para `Normal` somente se PID, criação UTC e caminho ainda coincidirem e o estado continuar `BelowNormal`; se o processo terminar, PID for reutilizado, identidade ficar inacessível ou a prioridade mudar externamente, registrar conflito e não escrever.
- `BelowNormal` pode reduzir a parcela de escalonamento do probe durante disputa de CPU; não promete FPS nem benefício de desempenho e pode atrasar apenas o processo-alvo. A restauração também seria uma escrita real e precisa de aprovação específica. O código atual mantém `BlockedProcessPriorityMutationPolicy` e a página não expõe comandos de aplicação/restauração.
- **Captura persistida do motor não realizada:** o diário de processo grava consentimento de consequência junto com a captura; não se registrou esse consentimento por inferência. O probe consultado já estava encerrado quando essa distinção foi confirmada. Uma nova execução exige probe novo, leitura nova e aceite explícito para registrar a captura; isso, por si só, não autoriza alteração de prioridade.
- **Validação visual pendente:** o inventário disponível do CUA retornou sem aplicações abertas. Não foi aberta ou focada janela do produto e nenhum clique/tecla foi enviado. A página e seus estados visuais continuam pendentes; checklist manual em `docs/TESTING.md`.
- Os 72/72 testes anteriormente registrados continuam sendo exclusivamente verificações simuladas do harness. Esta consulta real comprova somente que o probe iniciou, que o Windows expôs os campos lidos pelo `Get-Process` e que o processo encerrou; não comprova integração visual, captura persistida, setter ou reversão no Windows. Nenhuma nova compilação ou execução do harness foi feita neste registro, que altera apenas documentação.

## 20. Captura real de processo persistida (2026-10-19)

- Após autorização explícita apenas para captura, uma nova instância do probe foi iniciada e relida. `Get-Process` e `WindowsProcessPriorityAdapter.Inspect` confirmaram a mesma identidade por PID, criação UTC e caminho, prioridade `Normal` e processo respondendo. Os identificadores concretos permanecem no diário local do usuário e não neste arquivo versionado.
- `ProcessPriorityOperationService.CreateReview` criou a revisão real `Normal → BelowNormal`. `CaptureAsync(..., consequencesAccepted: true)` gravou um novo registro em `%LOCALAPPDATA%\CalixtoOptimizer\History`; o JSON foi reaberto pelo `JsonOperationJournalStore.LoadAsync`. Estado persistido `SnapshotsCaptured`, consentimento individual aceito, `PreviousPriority=Normal`, `TargetPriority=BelowNormal` e fingerprint presente.
- A leitura feita pelo adaptador após salvar confirmou a mesma identidade e prioridade ainda `Normal`. A captura persistida é um snapshot para o plano e não um backup do Windows. O recibo de consentimento não é autorização para escrita (`CanAuthorizeRealApplication=false`); a política padrão segue bloqueando mutações. No momento da captura ainda não havia autorização de escrita; aplicação e restauração só foram chamadas depois da aprovação específica registrada abaixo.
- O runner temporário compilou contra os projetos Core/Windows com o SDK local 10.0.402, 0 avisos e 0 erros. Essa compilação valida a integração do runner com as bibliotecas, não a janela WPF. Os 72/72 anteriores seguem simulados; este evento valida leitura/captura real e persistência, não setter ou reversão.
- Após autorização específica para o ciclo completo, o runner de teste injetou `ConfirmedProcessPriorityMutationPolicy(enabled: true)` somente no processo de teste; a política padrão do produto não foi alterada.
- A releitura imediatamente antes da aplicação confirmou a identidade capturada e `Normal`. `ApplyAsync` chamou o setter real somente para o probe e terminou `MutationVerified`; nova leitura confirmou a mesma identidade em `BelowNormal`. `RestoreAsync` confirmou o mesmo alvo e prioridade proposta, restaurou `Normal` e terminou `RestoredVerified`; a leitura final confirmou identidade igual e prioridade `Normal`.
- O diário local foi reaberto após o ciclo. O estado final é `RestoredVerified`, com eventos `Planned`, `ConsentRecorded`, `SnapshotCaptured`, `MutationArmed`, `MutationVerified`, `RestorationArmed` e `RestoredVerified`. O probe foi encerrado por Enter, código 0, e consulta posterior confirmou que não estava ativo.
- Nenhum outro processo ou configuração do Windows foi alterado. O exercício prova a escrita/reversão deste adaptador e serviço no Windows para o probe identificado; não valida a interface visual nem amplia a política padrão bloqueada. Os 72/72 do harness continuam sendo exclusivamente simulados.

## 21. Primeiro protótipo de Registro e investigação de políticas (2026-10-19)

Continuação de `65a0ad439fb1675d8761df43b2958957298ab08c`. A pendência de inspeção visual da página **Processos** foi mantida; não foi aberta janela nem usado mouse/teclado.

### Ajuste escolhido e base técnica

- Escolhido `registry.explorer.file-extensions`, uma preferência de interface do Explorador, não um ajuste de desempenho. O alvo inicial recomendado é exibir extensões (`HKCU`, `Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`, `HideFileExt`, `REG_DWORD 0`, visão 64 bits); ocultá-las (`REG_DWORD 1`) é opção do mesmo item, com risco e consequência mais altos. Nenhum FPS ou ganho de velocidade é alegado.
- A página de suporte Microsoft descreve a preferência em Windows 10 e 11. O repositório mantido pela Microsoft `winget-dsc` implementa o mapeamento do valor e modela ausência como exibir. A fonte de suporte não publica uma API/contrato de Registro estável para terceiros; essa diferença e a ausência de mínimo de build documentado estão anotadas em `docs/ADJUSTMENTS.md`. O produto limita a revisão a Windows x64. Windows 10 teve suporte encerrado pela Microsoft em 14/10/2025.
- A consequência é visual: exibir extensões ajuda a distinguir nomes/tipos; ocultá-las pode tornar nomes visualmente ambíguos. Não renomeia arquivos e não altera desempenho. O local é o perfil atual, sem elevação automática. Não se espera reinício do Windows; atualização visual após escrita direta não foi testada e o app não reinicia Explorer.
- O snapshot representa separadamente presença da chave, presença do valor, tipo e dado. A aplicação não cria a chave pai. Reversão compara primeiro o alvo atual; restaura o tipo/dado anterior ou exclui somente o valor que estava ausente, preservando chave e demais valores. Leituras repetidas em torno da gravação do diário detectam alterações observadas; ainda existe uma janela concorrente entre última leitura e setter, pois o Registro não oferece compare-and-swap nesta API.

### Motor compartilhado e gestão organizacional

- Implementados `RegistryPreferenceOperationService`, `WindowsRegistryPreferenceAdapter`, plano/consequência `AdjustmentPlanService`, consentimento `PlanConsentService` e histórico `OperationJournalCoordinator`/`JsonOperationJournalStore` existentes. Não foi criada estrutura paralela de diário. Consentimento persistido não autoriza escrita; há política separada de mutação, bloqueada por padrão, e confirmação vinculada à captura.
- O adaptador Windows é estreito: somente a localização descrita acima em `HKCU`/Registry64; não acessa a árvore `Policies` e não cria chave pai. Valores `REG_SZ`, `REG_EXPAND_SZ`, DWORD, QWORD, MULTI_SZ, BINARY e NONE podem ser capturados/restaurados preservando tipo/dado. Valor desconhecido/não preservável resulta em leitura recusada.
- Microsoft documenta que políticas ADMX/MDM fazem mapeamentos para o Registro. Uma árvore `Policies` não basta para concluir suporte, e `HKCU\Software` não prova ausência de gestão: GPP, MDM, scripts de logon e agentes podem redefinir a preferência. Não há detecção universal implementada. `WindowsRegistryPreferenceAdapter.Assess` retorna `Unknown`; estado desconhecido ou administrado recusa aplicação e restauração. Nenhuma configuração de política é implementada.
- O serviço ainda não foi ligado à UI; a tela não apresenta nem oferece este ajuste. O adaptador real foi compilado, porém não foi chamado para ler ou escrever no Registro. A política padrão não foi habilitada.

### Testes simulados, build e estado real

- Os 13 novos casos usam exclusivamente `FakeRegistryPreferenceAdapter` e journal temporário de teste. Incluem ausência versus DWORD 0; tipos REG_SZ, REG_EXPAND_SZ, DWORD, QWORD, MULTI_SZ, BINARY e NONE; recusa de prévia obsoleta; restauração e exclusão apenas do valor; acesso negado; conflito antes da aplicação/restauração e durante a gravação da intenção; gestão desconhecida; política padrão bloqueada; falha de persistência por espaço cheio; interrupção depois do setter falso, sem rollback automático; e recusa da chave pai ausente.
- SDK escolhido explicitamente: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402. `global.json` permanece em 10.0.100 com `latestFeature`.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release`: êxito; WPF, Core, Windows, harness e probe compilados; 0 avisos, 0 erros.
- `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: **85/85** verificações passaram. Os 13 casos de Registro são simulados e não chamam APIs do Registro.
- Nenhuma chave ou valor real, inclusive de teste, foi criado, lido, alterado ou removido neste marco. Nenhum ajuste, política ou preferência funcional do Windows mudou. Não houve teste de integração real nem validação da apresentação no Explorador.

### Teste de integração preparado, sem aprovação

`docs/ADJUSTMENTS.md` e `docs/TESTING.md` descrevem um runner isolado que criará uma chave GUID única em `HKCU\Software\CalixtoOptimizer\IntegrationTests\<GUID>`, validará `ProbeValue` DWORD `0 → 1`, excluirá apenas esse valor e removerá a chave GUID somente se vazia. O teste para sem apagar conteúdo inesperado e deixa pais comuns intactos. Valida acesso e leitura/escrita no Registro de 64 bits, não a mudança visual do Explorador. O runner ainda não foi criado nem executado. É necessária aprovação específica antes de criar ou modificar essa chave.

### Matriz completa atualizada do plano oficial

| Fase oficial | Diagnóstico | Configuração / geração de plano | Execução real | Estado global e evidência |
| --- | --- | --- | --- | --- |
| 0 — Ambiente e decisões | Windows x64, SDK e builds locais identificados. | Regras, versões e limitações documentadas; segundo PC pendente. | Build e testes locais verificados. | **Parcial** — ambiente local conhecido; validação em outro PC e proveniência integral dos pacotes antigos pendentes. |
| 1 — Inventário de hardware, dispositivos e drivers | WMI, planos de energia, PnP e inventário de drivers consultados localmente; GPU reportada separada de uso. | Inventário não configura hardware. | Consultas locais somente leitura verificadas. | **Concluído localmente no escopo consultado**; outros OEMs/builds e comparação externa de versão pendentes. |
| 2 — Catálogo, compatibilidade, perfis, consentimento, diário e simulação | Estado real consumido por alguns módulos; cobertura do catálogo segue pequena. | Perfis, consequências e consentimento compartilhado implementados; planos de energia/processos/Registro são revisáveis em seus serviços. | Simulação separada; política Registry padrão negada. | **Parcial** — motor comum existe; não representa todos os módulos nem oferece recuperação automática. |
| 3 — Primeiro ajuste reversível e recuperação | VIDEOIDLE/modo de energia lidos/capturados; ciclo real de prioridade comprovado somente no probe. | Serviços condicionais de energia/prioridade e fluxo tipado Registry codificados; Registry ainda fora da UI. | Energia permanece sem escrita; prioridade mudou e foi restaurada somente no probe aprovado; Registry não foi chamado. | **Parcial** — Registro tem testes simulados, sem integração real ou visual. |
| 4 — Energia, Registro, políticas, processos e rede | Energia/rede e processos têm diagnóstico em graus diferentes; Registry: adaptador de leitura compilado, não executado; estado de gestão sempre `Unknown`; políticas sem inventário próprio. | Energia, processo e Registry têm serviços/plano/captura; rede tem teste explícito; políticas sem configuração. | Energia bloqueada; ciclo de prioridade limitado ao probe; Registry bloqueado/nunca chamado; nenhuma política ou rede alterada. | **Parcial** — Registro só simulado; política indisponível. Validação visual de Processos pendente. |
| 5 — Integrações gráficas compatíveis | WMI reporta controlador e encaminha às telas nativas; preferência por app e GPU corrente são estados distintos. | Sem configuração programática pelo Calixto. | Nenhuma. | **Parcial como diagnóstico/encaminhamento**; preferência programática e telemetria continuam indisponíveis. |
| 6 — Pós-formatação, fontes oficiais e instalação assistida de drivers/programas | Identificação de drivers/PnP disponível localmente. | Catálogo de pacotes oficiais e plano de instalação não iniciados. | Nenhum driver/programa instalado. | **Parcial** — instalação assistida e validação em outro PC pendentes. |
| 7 — ZIP, instalador e recuperação | Hashes de pacotes anteriores registrados. | Pacotes 0.5/0.6 existem; não regenerados neste marco. | Setup/upgrade/desinstalação anteriores testados neste PC; build atual não testado pelo instalador. | **Parcial** — recuperação ampla, instalação do build atual e outro PC pendentes. |
| 8 — Controles extremos por equipamento | Matriz CPU/GPU/placa/OEM não iniciada. | Sem integração oficial de fabricante/firmware. | Não implementada nem autorizada. | **Não iniciado** — nenhuma escrita em BIOS, firmware, tensão ou frequência. |

### Próximo passo

- Próxima entrega menor: validar visualmente a nova página Registro e manter a validação visual manual de **Processos**. O runner isolado foi executado com aprovação e não substitui leitura/compatibilidade da preferência real nem teste do Explorador em outro PC.
- Pendências preservadas: validação visual manual de **Processos**, energia/VIDEOIDLE, políticas/gestão, configuração de rede, GPU programática, drivers assistidos, benchmark, instalador do build atual, recuperação prática e segundo PC.

## 22. Teste isolado real de Registro e integração da página (2026-10-19)

Continuação de `29de21a928626cba596cf74e4f0319a60a852cb2`. Não foi incorporado o patch de modificações futuras. A pendência de validação visual de **Processos** foi preservada.

### Teste real do mecanismo em chave isolada

- Após autorização exclusiva para `HKCU\Software\CalixtoOptimizer\IntegrationTests\<GUID>`, o runner opt-in do projeto de testes gerou o GUID `6e55acf9-2b7e-4581-95fb-3bd3b1f84684`. Usou a visão `Registry64` e somente o valor `ProbeValue` (`REG_DWORD`). O runner recusou GUID preexistente e usa a disposição de criação do Registro para detectar colisão.
- O adaptador existe apenas no projeto de testes e aceita somente o endereço daquele GUID/valor, com escrita limitada a DWORD 0 ou 1. A aplicação e o `WindowsRegistryPreferenceAdapter` não aceitam caminho de teste nem caminho arbitrário. O teste não passa pelo serviço de `HideFileExt` nem pelo inspetor de gerenciamento.
- Resultado real: criou uma chave GUID inicialmente vazia; adaptador leu valor ausente; criou e leu DWORD `0`; mudou e leu DWORD `1`; excluiu somente `ProbeValue`; confirmou a chave vazia e a removeu.
- `Software\CalixtoOptimizer` e `IntegrationTests` não existiam antes. Foram criadas apenas como pais do alvo isolado e removidas por estarem vazias. Consulta somente leitura após o runner confirmou que os dois pais e o GUID não existiam. O runner terminou com código 0 e não reportou falha ou resíduo.
- Escopo: prova as operações Registry64 e permissão do perfil para aquele endereço, nesta conta e máquina. Não leu nem alterou `HideFileExt`, não alterou `Policies`, não reiniciou Explorer e não comprova compatibilidade ou autorização da preferência real.

### Integração visual implementada, ainda sem validação visual

- Adicionada página **Registro e políticas**. Ela consulta `HideFileExt` somente quando a pessoa aciona “Ler estado e preparar prévia”, mostra chave/valor/tipo, alvo, compatibilidade e consequência, e mostra gerenciamento `Unknown` com bloqueio. A captura requer checkbox de consequência e é persistida pelo serviço/diário compartilhado existentes.
- A página apresenta histórico filtrado por `registry-file-extensions`; não oferece botões de aplicação ou restauração. `BlockedRegistryMutationPolicy` continua como padrão do serviço, e a avaliação `Unknown` recusa mutações também no serviço.
- A tela foi incluída na navegação, mas não foi aberta, clicada ou observada em screenshot. Portanto, integração compilada não significa validação visual; a página Processos também continua pendente de inspeção visual.
- A compatibilidade documentada de `HideFileExt` permanece limitada à preferência que a Microsoft expõe na interface Windows 10/11 e ao mapeamento observado no recurso Microsoft `winget-dsc`; não há build mínimo publicado nem contrato Win32 estável para terceiros. A aplicação ainda não leu o valor neste computador, não determinou a edição/build compatível, e a detecção abrangente de gerenciamento organizacional permanece indisponível. A política de escrita permanece fechada.

### Matriz completa atualizada do plano oficial

| Fase oficial | Diagnóstico | Configuração / geração de plano | Execução real | Estado e evidência |
| --- | --- | --- | --- | --- |
| 0 — Ambiente e decisões | SDK 10.0.402 e Windows x64 locais identificados. | Escopo e limites documentados; outro PC pendente. | Build completo e harness local executados. | **Parcial** — evidência local; sem validação em outro computador. |
| 1 — Inventário de hardware, dispositivos e drivers | WMI, PnP, energia e rede consultados em marcos anteriores. | Inventário descritivo, sem configuração. | Consultas somente leitura validadas no escopo documentado. | **Concluído localmente no escopo consultado**; cobertura de OEMs/edições ainda pendente. |
| 2 — Catálogo, compatibilidade, perfis, consentimento, diário e simulação | Prévia real em alguns serviços; Registro ainda não lido pela UI. | Catálogo pequeno; motor, consentimento e diário reutilizados; captura Registro integrada. | Simulações, falhas injetadas e persistência do diário testadas; escrita default negada. | **Parcial** — não é catálogo completo nem recuperação automática. |
| 3 — Primeiro ajuste reversível | VIDEOIDLE, energia e prioridade do probe têm leituras reais em seus escopos; `HideFileExt` ainda sem leitura real nesta entrega. | Registro: prévia/consequência/captura na UI; processo individual e energia codificados com bloqueios. | Ciclo de prioridade real só no probe aprovado; ciclo de Registro real só em chave teste; energia e preferência funcional não alteradas. | **Parcial** — ajuste Registro de preferência não aplicado ou validado visualmente. |
| 4 — Energia, Registro, políticas, processos e rede | Diagnósticos existentes em graus diferentes; Registro da preferência disponível sob demanda mas não acionado; gerenciamento desconhecido; política sem inventário próprio. | Energia, processos e Registro com revisão/captura; políticas não configuráveis; rede só diagnóstica. | Energia, `HideFileExt`, políticas e rede sem mutação; apenas probe de prioridade e GUID Registro autorizados foram escritos/revertidos/limpos. | **Parcial** — política indisponível e validações visuais de Processos/Registro pendentes. |
| 5 — Integrações GPU compatíveis | Controlador reportado; preferência e uso efetivo separados; encaminhamento nativo. | Preferência programática indisponível. | Nenhuma configuração. | **Parcial como diagnóstico/atalho**; telemetria e preferência ainda não implementadas. |
| 6 — Pós-formatação e instalação assistida | Identificação de drivers disponível. | Catálogo de pacotes oficiais/planos de instalação não concluído. | Nenhum driver instalado. | **Parcial** — assistente não implementado. |
| 7 — ZIP, instalador e recuperação | Artefatos antigos e hashes registrados. | Sem pacote novo nesta etapa. | Testes de versões anteriores registrados; instalação do build atual e recuperação ampla pendentes. | **Parcial** — empacotamento não conclui produto. |
| 8 — Ajustes específicos por hardware | Matriz CPU/GPU/OEM ausente. | Integração oficial de fabricante/firmware ausente. | Não implementada nem autorizada. | **Não iniciado** — BIOS, firmware, tensão e frequência não são escritos. |

### Validação e pendências

- Build completo Release com SDK local explicitamente escolhido `10.0.402`: êxito, 0 avisos e 0 erros. Harness Core: **85/85** verificações simuladas passaram. O runner de integração real é separado e opt-in.
- Não houve consulta da preferência funcional, escrita em `HideFileExt`, alteração de política, reinício do Explorer ou validação visual da tela. O teste do GUID não serve como evidência para nenhuma dessas verificações.
- Pendências preservadas: inspeção visual manual de **Processos** e **Registro**; validar leitura/compatibilidade real do candidato sem escrita; gerenciamento corporativo; energia/VIDEOIDLE; rede/GPU; drivers assistidos; instalador/build atual; recuperação e segundo PC.
- Próxima entrega menor: percorrer visualmente a página Registro em modo de revisão/captura sem habilitar aplicação, enquanto mantém o histórico visível; conservar a pendência visual separada para Processos.

## 23. Revisão de integração visual e matriz por capacidade — 2026-10-19

Continuação do commit `ad910c31afbbfcdd42a49baaad93fab8818c4167`; árvore limpa no início. O foco desta etapa foi conferir integração e precisão dos estados na interface, sem criar ajustes. A janela não foi aberta nem focada: esta sessão não permite inspecionar a UI nativa sem tomar o primeiro plano do usuário. Por isso, nenhuma validação visual é declarada aprovada.

### Auditoria estática e correção de texto

- Navegação está ligada às páginas de Rede, Processos, Registro e Planos/Energia. Ao entrar, os view models disparam as leituras pertinentes; consultas longas usam operações assíncronas ou `Task.Run`, e falhas são convertidas em mensagens de estado. Seleção de processos e alvos de Registro atualiza os comandos/prévia; mudança de alvo Registro invalida a revisão anterior.
- Processos identifica a leitura como real, informa acesso negado e processo encerrado, apresenta revisão/consequência e captura no diário. A página declara que não chama setters, não altera em massa e não oferece `Tempo real`. Nenhuma mudança de prioridade foi executada nesta etapa.
- Registro oferece leitura real somente sob demanda, revisão de compatibilidade/gestão e consequência, captura e histórico. O texto informa `Unknown` para gestão e aplicação/restauração bloqueadas; os controles de escrita não existem nessa página. O teste aprovado em GUID isolado permanece evidência somente do mecanismo de Registro, não de `HideFileExt` ou política.
- Rede separa a leitura local de adaptadores do teste ativo. Destino, finalidade, amostras, timeout, RTT, perda e duração ficam explícitos; o teste exige clique e permite cancelamento. O resultado ICMP não pretende provar acesso geral à Internet. A página não registra snapshots nem possui histórico de alterações, porque não configura a rede.
- Energia distingue leitura real AC/DC/plano ativo, captura real persistida e demonstração sintética. A aplicação/restauração do modo de energia não é uma ação de diagnóstico: o serviço padrão bloqueia setters, e a UI contém fluxo real separado com consentimentos, frase e captura específicos. Nenhuma aplicação/restauração foi acionada nesta etapa. VIDEOIDLE continua bloqueado.
- A auditoria encontrou uma contradição no texto geral de Planos: dizia que nenhuma tela podia aplicar mudanças, embora o painel de modo de energia tenha uma ação real separada, condicionada a confirmação explícita. O texto foi corrigido em `src/Optimizer.App/Views/ProfilesPage.xaml` para distinguir demonstração, leitura, captura e fluxo de execução condicionado, sem alterar comportamento ou configurações.
- As etiquetas existentes distinguem `CAPTURA REAL PERSISTIDA · NÃO APLICADA`, estados de falha/conflito e resultados verificados pelo serviço após setter. Os históricos documentam que snapshot não é backup do Windows. Nenhuma captura/consentimento foi criada nesta etapa.

### Matriz completa do plano oficial por capacidade

Esta matriz substitui as classificações anteriores resumidas por fase. “Visual integrada” indica presença de rota/tela no código, não inspeção por screenshot. Os 85 testes são do harness Core e simulados; não os atribua como teste real de cada integração.

| Fase/capacidade | Implementação | Integração visual | Teste simulado | Teste real executado | Bloqueios e pendências |
| --- | --- | --- | --- | --- | --- |
| 0 — Ambiente, build e portabilidade | Solução .NET/WPF e SDK local documentados; alvo x64. | Navegação e páginas compiladas; inspeção visual não feita nesta etapa. | Harness Core completo disponível. | Build e harness repetidos nesta etapa com SDK explicitamente escolhido; veja resultados abaixo. | Outro PC, Windows/SDK alternativos e execução a partir de cópia limpa ainda precisam de validação própria. |
| 1 — Inventário de hardware, dispositivos e drivers | Consultas locais de sistema, planos, PnP e drivers; link para fontes nativas GPU. | Páginas Overview/Drivers integradas; validação visual atual pendente. | Cobertura de compatibilidade e resultados sintéticos no harness, sem comprovar cada fornecedor. | Consultas somente leitura de energia, dispositivos/drivers e rede comparadas parcialmente com ferramentas nativas em etapas anteriores. | Mais OEMs/builds, divergências de inventário e fonte comparável de versão de driver; nenhuma classificação de driver desatualizado sem fonte. |
| 2 — Catálogo, compatibilidade e perfis | Catálogo pequeno, perfis Leve/Equilibrado/Extremo/Personalizado e plano revisável. | Página Planos integrada; demo sinalizada sintética; validação visual integral pendente. | Planos, risco, consequência, consentimento e falhas exercitados em adaptadores falsos. | Sem execução do catálogo de demonstração como otimização; nenhum ganho de desempenho foi medido. | Cobertura do catálogo ainda pequena; não promover a demonstração sintética a ajuste real. |
| 2 — Consentimento e diário/histórico | Diário persistente comum; snapshots tipados e retomada avaliada; captura não é backup Windows. | Histórico compartilhado nas telas e histórico de energia; visual pendente. | Falha de persistência, interrupção, conflito, retomada e estados não verificados cobertos pelo harness. | Captura de modo de energia e de processo foram persistidas e reabertas em etapas anteriores; teste isolado Registry também foi limpo. | Recuperação prática por módulo e UI ainda pendentes; a existência de journal não comprova restauração no Windows. |
| 3/4 — Energia: modo de energia AC/DC | Leitura via PowrProf, captura com AC/DC/plano/fingerprint, verificação e reversão condicional codificadas. | Painel em Planos com consequência por lado, captura, autorização e histórico; inspeção visual completa pendente. | Aplicação/reversão/conflito/interrupção são simulados no harness. | Getters e captura persistida foram validados localmente; nenhum setter foi chamado nesta etapa nem ciclo real aplicado/restaurado. | A UI tem caminho real que exige aprovação específica. Qualquer aplicação/reversão real segue dependente de aprovação do usuário e validação específica; outro PC pendente. |
| 3/4 — Energia: VIDEOIDLE | Adaptador e fluxo de leitura/captura; botão de aplicação desabilitado. | Painel técnico separado de perfis e histórico; visual pendente. | Casos simulados do serviço e bloqueio de escrita no harness. | Leitura/captura local documentada em etapas anteriores; sem aplicação ou restauração real. | Teste real de setter/reversão não autorizado; permanece bloqueado e não é otimização comprovada. |
| 4 — Registro: preferência `HideFileExt` | Catálogo/previsão, adaptador tipado, captura que distingue ausência, serviço e política de mutação negada. | Página Registro com leitura sob demanda, consequência, captura/histórico; visual pendente. | 13 casos de Registro dentro do harness, usando adaptadores falsos. | Teste Registry64 de criar/ler/alterar/remover `ProbeValue` em GUID isolado, autorizado e limpo. Nenhuma leitura/escrita de `HideFileExt` ocorreu. | Compatibilidade funcional por build/edição não validada; aplicação/restauração bloqueadas; gerenciamento permanece `Unknown`. O teste isolado não conclui esta capacidade. |
| 4 — Políticas e gestão organizacional | Sem configuração de política; avaliador do candidato funcional retorna `Unknown` e recusa mutação. | A página explica o bloqueio; não há editor de políticas. Inspeção visual pendente. | Estado `Unknown`/recusa coberto por simulados do Registro. | Nenhum inventário ou teste real de GPO/MDM/GPP/scripts/agentes. | Uma chave `Policies` não comprova suporte. Detecção universal e tratamento de dispositivo administrado não implementados; capacidade não concluída. |
| 4 — Processos e prioridade | Identidade PID/criação/caminho, leitura de classe, plano individual e recuperação condicional; sem massa/Tempo real. | Página Processos com lista, seleção, consequências, captura e histórico; inspeção visual pendente. | Conflito, acesso negado, saída, PID reutilizado e persistência falha simulados. | Ciclo real `Normal → BelowNormal → Normal` validado somente no probe descartável em etapa aprovada anterior; nenhum processo foi alterado nesta etapa. | A tela de produção segue sem setter; validação visual e outros tipos de processo pendentes. Resultado do probe não generaliza. |
| 4 — Rede | Inventário local e diagnóstico explícito de DNS/ICMP com cancelamento, duração, RTT e perda. Nenhuma configuração de rede. | Página Rede com adaptadores, teste e resultado; inspeção visual pendente. | Sucesso, DNS, timeout, ICMP bloqueado, parcial e cancelamento cobertos no harness. | Até quatro sondagens para `1.1.1.1` responderam em etapa aprovada anterior; inventário agregado comparado parcialmente a `Get-NetAdapter`/`Get-NetIPConfiguration`. Sem alteração de rede. | A tela ainda não foi validada visualmente; um resultado ICMP não prova acesso geral/velocidade. Falhas ativas e cancelamento de UI pendentes. |
| 5 — GPU compatível | Inventário WMI e atalhos para configurações nativas; sem leitura programática da preferência por app nem telemetria de uso. | Overview integrada; inspeção dos atalhos/estados visuais pendente. | Não existe teste que comprove preferência configurada ou GPU efetivamente usada. | Controlador foi reportado no inventário local; não foi demonstrado que uma preferência específica estava configurada nem qual GPU estava em uso. | Manter GPU presente, preferência por aplicativo e GPU efetivamente usada como estados distintos; capacidade de preferência/uso permanece indisponível. |
| 6 — Pós-formatação e instalação assistida | Inventário de drivers existe; catálogo de software/fontes oficiais e fluxo assistido incompletos. | Página Drivers apresenta inventário; fluxo de seleção/instalação não existe para validar. | Não há simulação equivalente a instalação segura de OEM. | Nenhum driver ou programa instalado. | Seleção de pacote, assinatura/fonte, compatibilidade, ponto de recuperação e instalação assistida pendentes. |
| 7 — ZIP/instalador/recuperação e publicação | Scripts e artefatos experimentais 0.5/0.6 documentados, com hashes anteriores. | Não há UI de distribuição; nenhum pacote novo nesta etapa. | Empacotamento/build local testado em versões anteriores. | Instalação/upgrade/desinstalação de versões anteriores foram executados localmente conforme histórico. | Build atual não empacotado nem instalado; publicação/recovery em outro PC pendente. Artefatos não equivalem a produto concluído. |
| 8 — Controles específicos por hardware | Não implementado. | Nenhuma UI de controle existe. | Não há testes específicos de fabricante/firmware. | Nenhuma escrita em BIOS, firmware, tensão ou frequência. | Exige integração oficial, hardware identificado e validação por modelo; fase não iniciada. |

### Validação desta etapa e próxima menor entrega

- Validação visual: **pendente** para Processos, Registro, Rede e painel Energia. Nenhuma página foi aberta/focada, screenshot obtido ou interação feita; não há estado visual aprovado. Um checklist manual único, sem comandos de aplicação/restauração, foi consolidado em `docs/TESTING.md`.
- Revisão estática: encontrou e corrigiu a mensagem contraditória no cabeçalho de Planos. A correção foi apenas texto XAML; não altera comandos ou permissões.
- Build completo Release com `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402, no diretório `%USERPROFILE%\Downloads\CalixtoOptimizer` (Windows 10.0.26300, RID `win-x64`): sucesso, 0 avisos e 0 erros. Harness `Optimizer.Core.Tests` Release: **85/85** verificações passaram. Esses resultados não substituem inspeção visual nem tornam testes simulados em validações reais.
- Não houve captura nova, sondagem ativa, leitura de configuração nesta etapa, chamada de setter, reinício de Explorer, instalação de driver ou mudança de energia, Registro, política, processo ou rede.
- Próxima menor entrega prevista: executar **uma sessão manual de validação visual das quatro páginas**, seguindo o checklist consolidado em `docs/TESTING.md`. É menor e fecha a integração já existente sem abrir outro módulo. Critérios: navegar e voltar sem tela vazia; observar carregamento e mensagem de erro/estado; conferir seleção, consequência e diferença entre leitura/captura; reabrir histórico para confirmar persistência; não aplicar/reverter energia, prioridade ou Registro; classificar cada módulo como validado somente com evidência visual. Esta entrega não foi iniciada.

## 24. Publicação autocontida e inicialização pelo atalho — 2026-10-19

Continuação do commit `a5d2db890fc5a4f99373fecf16a5f31b4899c6d0`, na raiz oficial `%USERPROFILE%\Documents\meu otimizador`. O projeto não foi movido nesta etapa.

### Ambiente e publicação atual

- Raiz Git confirmada: `%USERPROFILE%\Documents\meu otimizador`; branch `main`; commit de origem `a5d2db890fc5a4f99373fecf16a5f31b4899c6d0`; árvore limpa antes do trabalho.
- SDK disponível e chamado explicitamente: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, SDK `10.0.402`. Inno Setup `7.1.0` disponível em `%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe`.
- Publicação atual produzida de `src/Optimizer.App/Optimizer.App.csproj`, Release, `win-x64`, `--self-contained true`, em `artifacts/preview-v0.6.0-experimental-selfcontained-20261019/CalixtoOptimizer`. Contém 405 arquivos, inclusive `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`, e declara `Microsoft.NETCore.App` e `Microsoft.WindowsDesktop.App` versão `10.0.13` em `includedFrameworks`.
- Executável autocontido: `artifacts/preview-v0.6.0-experimental-selfcontained-20261019/CalixtoOptimizer/Optimizer.App.exe`; SHA-256 `B0C3A45341764118569C08475AE91687E13946EE6DCA250D7EDC7A32AA633E9D`.
- Foi iniciado o executável dessa publicação (não o executável framework-dependent em `bin`). O processo mostrou janela principal com título `Calixto Optimizer`, handle de janela não nulo e `Responding=True`. O erro de runtime ausente não apareceu. A primeira janela de preview foi fechada normalmente antes do teste do instalador.

### Instalador único de validação e teste do atalho

- Adicionado `packaging/CalixtoOptimizer-Preview-20261019.iss`. Usa identidade Inno, pasta de instalação, nome de atalho e caminho de artefato exclusivos; inclui recursivamente a mesma publicação autocontida. Não sobrescreve nem substitui os instaladores anteriores e não cria ação de lançamento automático durante o setup.
- `ISCC.exe` 7.1.0 compilou o script com sucesso; saída do compilador “Successful compile”, código 0. Instalador: `artifacts/installer-preview-20261019/CalixtoOptimizer-0.6.0-experimental-preview-20261019-win-x64-setup.exe`, 46.782.097 bytes; SHA-256 `B559EFD19FE4C84928D938E9BC39B9F211F2E9FF7896A8035647A09BDAA31263`.
- Instalação autorizada executada sem interface do setup (código 0), isolada em `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019`. Foram encontrados 407 arquivos instalados, payload/runtimes presentes e o hash do `Optimizer.App.exe` instalado coincide com o da publicação. O setup cria o registro comum de desinstalação do próprio app e o atalho do Menu Iniciar; isso não altera preferências de otimização.
- O `.lnk` foi lido e seu `TargetPath` e `WorkingDirectory` coincidiram exatamente com a pasta exclusiva da instalação. O aplicativo foi iniciado pelo próprio atalho; o processo apontou para o executável instalado, exibiu janela principal `Calixto Optimizer` e permaneceu respondendo. Essa janela instalada está **deixada aberta** para a inspeção manual do usuário.
- Resultado específico: o erro de runtime foi resolvido para a publicação autocontida e para o atalho do pacote recém-instalado. Isso confirma inicialização; não garante ausência de outros defeitos nas páginas. Nenhuma página foi operada ou inspecionada visualmente; não houve controle de mouse/teclado. Desinstalação não foi autorizada nem executada.
- Não houve aplicação de otimizações, setters de energia, escrita em preferências do Registro, tráfego de rede, mudança de prioridade ou instalação de drivers. Nenhuma Release foi publicada. O artefato é um preview experimental, separado e não assinado; não é uma nova versão de distribuição estável.

## 25. Identificação do Windows e evidência de drivers — 2026-10-19

Continuação do commit `14aab7cbe28aa41f3b052d10933ab281a094d5db`, na raiz oficial `<raiz do repositório>`. Nenhum pacote Preview novo foi gerado.

### Correção da identificação do Windows

- Causa reproduzida por leitura somente leitura: `WmiComputerInventoryService.ReadWindowsVersion` usava apenas `ProductName` em `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`; `OverviewPageViewModel.WindowsLabel` compõe `ComputerInventory.WindowsName`, edição, versão, build e arquitetura. Neste computador, o Registro ainda retorna `Windows 10 Pro` após atualização, portanto o app mostrava a família incorreta.
- O serviço agora consulta `Win32_OperatingSystem.Caption`, `Version`, `BuildNumber`, `ProductType` e `OperatingSystemSKU`, além de `EditionID`, `DisplayVersion`, `CurrentBuildNumber` e `UBR` no Registro. A versão `10.0` sozinha nunca decide entre Windows 10 e 11. Para cliente Windows `10.0`, build `22000+` indica a família Windows 11 conforme a documentação Microsoft; caption e build são cruzados. Se divergirem, o app mostra `Windows (identificação divergente)` e aviso com fontes. A edição é cruzada entre Caption, SKU CIM documentado e EditionID; se fontes reconhecidas divergem, a edição fica sem confirmação. `ProductName` é comparação, não autoridade; se for a única fonte disponível, o app mostra identificação não confirmada. O CIM é preferido para a build em execução e não recebe UBR se a build no Registro não coincidir.
- Resultado real do PC via `Win32_OperatingSystem`, Registro e serviço atualizado: Caption `Microsoft Windows 11 Pro`; versão do kernel `10.0.26300`; build `26300.9711`; `ProductType=1` (workstation); `OperatingSystemSKU=48` (Professional); `EditionID=Professional`; `DisplayVersion=26H2`; `CurrentBuildNumber=26300`; `ProductName=Windows 10 Pro`. A nova coleta de `WmiComputerInventoryService` retornou `Windows 11`, `Professional`, `26H2`, `26300.9711`; Caption/SKU/EditionID concordam na edição, e o aviso identifica `ProductName` como divergente e desconsiderado para o nome.
- A página Microsoft de release health lista build `26300.9711` como Windows 11 26H2, General Availability Channel, publicado em 13/10/2026. A família base 26300 também aparece no Flight Hub de previews. A edição/build observada do host coincide com a linha GA documentada; o inventário não consulta inscrição/canal do Windows Insider e mantém esse estado como não verificado em vez de deduzi-lo da build. O valor adicional `BuildLabEx` lido foi `26100.1...ge_release`; não foi tratado como build do sistema em execução nem como prova de canal, pois CIM e `CurrentBuildNumber` concordam em 26300 e a referência de release confirma a build completa.
- Fontes: [Win32_OperatingSystem (Caption, Version, BuildNumber, ProductType e SKU)](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-operatingsystem); [limiar documentado 10.0/build 22000 para Windows 11](https://learn.microsoft.com/en-us/windows-hardware/drivers/gettingstarted/platforms-and-driver-versions); [versão 10.0 compartilhada por Windows 10/11](https://learn.microsoft.com/en-us/windows/win32/sysinfo/operating-system-version); [build 26300.9711 / Windows 11 26H2 GA](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information); [Flight Hub, builds preview](https://learn.microsoft.com/en-us/windows-insider/flight-hub).
- Testes adicionados: Windows 10 coerente; Windows 11 com `ProductName` antigo; caption/build conflitantes; inferência com caption ausente; servidor não convertido em Windows cliente; divergência entre build CIM e build do Registro.

### Investigação de instalação assistida de drivers (sem implementação de instalação)

- Leitura somente leitura: placa-mãe `ASUSTeK COMPUTER INC. PRIME B550M-A AC`; o modelo de sistema `System Product Name` é genérico. Ethernet Realtek PCIe GbE, hardware ID de modelo `PCI\VEN_10EC&DEV_8168&SUBSYS_86771043`, status `OK`, driver `1.0.0.14`/`rtcx21x64.inf` (provider Microsoft). Wi-Fi Realtek 8821CE, hardware ID de modelo `PCI\VEN_10EC&DEV_C821&SUBSYS_304A1A3B`, status `OK`, driver `2024.0.8.108`/`netrtwlane.inf` (provider Microsoft). Sufixos de instância PnP não foram registrados.
- Manual oficial ASUS confirma no modelo Ethernet RTL8111H e módulo Wi-Fi RTL8821CE. A página de suporte ASUS do modelo, com Windows 11 64-bit selecionado, lista RTL8111H LAN `1168.27.50.919` (31/12/2025, SHA-256 `3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D`) e RTL8821CE Wi-Fi `2024.10.138.3` (04/10/2022). São candidatos por modelo/chip, não instalados. Ainda falta verificar assinatura e se os INF contêm IDs aplicáveis. Os formatos de versão do provider Microsoft não foram comparados como equivalentes; nenhum driver foi chamado de desatualizado.
- A investigação, fontes, estratégia de correspondência por INF/OS/arquitetura, fluxo offline, alternativa de link OEM e limites para instaladores opacos estão em `docs/DRIVER-ASSISTED-INSTALLATION.md`. Não se recomenda instalar todos os pacotes da placa-mãe. Apenas Ethernet e Wi-Fi físicos foram candidatos nesta revisão; utilitários, RAID, áudio, chipset, VGA e Bluetooth precisam de identificação e necessidade próprias.

### Validação desta etapa

- Raiz Git preservada; SDK explicitamente usado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402 (único SDK em `--list-sdks`); `dotnet --info` reportou MSBuild 18.9.15, host/runtime 10.0.13 e RID `win-x64`. Diretório de build `<raiz do repositório>`.
- Repetição final do build Release completo da solução: sucesso, 0 avisos e 0 erros. Harness Core: **91/91** verificações simuladas passaram. Uma tentativa anterior desta etapa falhou por inferência de tipo nullable; a correção foi aplicada e as execuções finais de build, harness e probe real passaram.
- Validação real do serviço `WmiComputerInventoryService` executada por um probe temporário de console fora do repositório e sem escrita no Windows: `WindowsName=Windows 11`, `Edition=Professional`, `Version=26H2`, `Build=26300.9711`, placa `ASUSTeK COMPUTER INC. PRIME B550M-A AC`. O serviço retornou os avisos previstos para `ProductName` divergente e canal Insider não consultado. Nenhum dado de série, nome do computador, usuário, IP ou endereço MAC foi coletado para a validação/documentação.
- Nenhum pacote de driver foi baixado/instalado/removido; nenhuma configuração de sistema foi alterada. O app instalado em `%LOCALAPPDATA%` permanece na versão anterior; não foi gerado instalador/Preview nesta etapa.

## 26. Candidatos ASUS para rede e integração na página Drivers — 2026-10-20

Continuação do commit `4f29d11187bdd1c38e8fe3d1dc379228a2ea2e9e`, na raiz oficial `%USERPROFILE%\Documents\meu otimizador`. A revisão anterior de drivers registrava que nenhum pacote havia sido baixado; este marco atualiza aquele estado. Nenhum instalador foi executado e nenhum driver ou configuração do Windows foi alterado.

### Evidência dos pacotes oficiais

- A página oficial ASUS da PRIME B550M-A AC lista Realtek RTL8111H LAN `1168.27.50.919`, Windows 11 x64, publicado em 31/12/2025; o SHA-256 publicado é `3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D`. O pacote oficial RTL8821CE Wi-Fi `2024.10.138.3` é listado para Windows 10/11 x64, publicado em 04/10/2022; a página não publica hash para seu ZIP.
- Downloads guardados localmente em `artifacts/driver-review-20261020/` (ignorado pelo Git): `ASUS-RTL8111H-LAN-1168.27.50.919.zip` (15.387.705 bytes) e `ASUS-RTL8821CE-WiFi-2024.10.138.3.zip` (18.051.928 bytes). LAN: hash local coincide com ASUS. Wi-Fi: SHA-256 calculado localmente `E061EBD3E77F54DA07ECED9876DB54DCAA46E871DB4015FEE6EC1F2987535647`; serve como fingerprint da cópia revisada, não prova origem independente.
- Integridade ZIP foi verificada por CRC32 de cada entrada contra o diretório central, além de comprimento descompactado: 722 entradas/34.802.917 bytes para LAN e 37 entradas/31.111.699 bytes para Wi-Fi, todos conferidos.
- `Get-AuthenticodeSignature` marcou todos os catálogos `.cat` dos dois pacotes como `Valid`, com signatário `Microsoft Windows Hardware Compatibility Publisher`. A associação do INF ao `.cat` correspondente foi verificada com `WinVerifyTrust`/`DRIVER_ACTION_VERIFY`: `rt68cx21x64.inf` em `rt68cx21x64.cat` e `netrtwlane.inf` em `netrtwlane.cat`, retorno `0x00000000` para ambos. A assinatura válida não comprova por si só o efeito funcional do pacote.
- A Ethernet tem correspondência exata no INF pelo Hardware ID ASUS `PCI\VEN_10EC&DEV_8168&SUBSYS_86771043&REV_15`, sob seção x64 `NTamd64.10.0...22000`; o host Windows 11 x64 build `26300.9711` satisfaz o alvo declarado. O Wi-Fi tem correspondência exata `PCI\VEN_10EC&DEV_C821&SUBSYS_304A1A3B`, em seção `NTamd64.10.0`; a página ASUS declara Windows 10/11 64-bit.
- Probe temporário somente leitura chamou o `WmiDriverInventoryService` real: inventário `Available`, 183 linhas de dispositivo e zero avisos. O catálogo da aplicação encontrou um dispositivo para cada pacote por Hardware ID exato. Os pacotes que o Windows reporta instalados são Microsoft Ethernet `1.0.0.14`/`rtcx21x64.inf` e Microsoft Wi-Fi `2024.0.8.108`/`netrtwlane.inf`. Nenhuma comparação entre esses esquemas de versão e as versões ASUS foi usada para declarar atualização, necessidade ou ganho de desempenho.

### Implementação e integração

- `WmiDriverInventoryService` agora mantém Hardware IDs e Compatible IDs em memória para comparar pacotes. O CSV e o relatório diagnóstico continuam omitindo os IDs completos.
- A página Drivers mantém o inventário local e ganhou a área **Candidatos oficiais**, com pacote instalado reportado pelo Windows, candidato ASUS, origem, versão/data publicada, hash, resultado de assinatura/CRC/INF/arquitetura e pendências. Correspondência exata, somente Compatible ID, ausente e ambígua têm estados separados; ambiguidade não escolhe dispositivo.
- **Conferir ZIP local (somente leitura)** calcula SHA-256 de um arquivo selecionado e reconhece somente os dois hashes revisados. O app não extrai nem executa esse arquivo. Hash desconhecido fica não validado; arquivos arbitrários não recebem parser genérico de INF nesta entrega.
- Inventário e correspondência usam WMI local e funcionam sem Internet; link ASUS e download são necessários apenas para obter pacotes novos. A instrução para pacote trazido por USB/outro PC está em `docs/DRIVER-ASSISTED-INSTALLATION.md`.

### Matriz desta etapa do plano oficial — fase 6

| Capacidade | Estado atual | Evidência/limite |
| --- | --- | --- |
| Inventário instalado e IDs locais | Implementado e validado neste host. | Probe WMI real: 183 linhas, 0 avisos; Ethernet/Wi-Fi presentes e identificados. Continua consulta somente leitura. |
| Encontrar candidato ASUS por Hardware ID | Implementado e validado para estes dois pacotes/dispositivos. | Correspondência exata nos INFs e um dispositivo local por candidato; em outros PCs precisa ocorrer nova leitura dos IDs e do SO. |
| Proveniência/integridade dos arquivos revisados | Validada para os dois ZIPs desta revisão. | LAN tem hash oficial correspondente; Wi-Fi sem hash ASUS; ZIPs fora do Git em pasta ignorada. Assinaturas de catálogo e associação dos INFs verificadas no Windows. |
| Conferência offline de pacote local | Integrada somente para ler hash e reconhecer byte a byte os arquivos já revisados. | Não extrai, verifica conteúdo desconhecido, persiste importação nem aprova origem/compatibilidade de outro pacote. |
| Instalação assistida de driver | Não implementada; bloqueada. | Não há comando de instalar/remover/substituir nem worker elevado seguro. Nenhum driver foi instalado nesta etapa. Exige desenho, aprovação específica e validação funcional em equipamento aplicável. |
| Validação visual da página Drivers | Pendente. | XAML integrado e compilado; janela não foi aberta nem capturada. |

### Build, testes e próximos bloqueios

- SDK explicitamente chamado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão `10.0.402`; diretório `%USERPROFILE%\Documents\meu otimizador`; configuração Release.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore`: compilação WPF e solução completa com sucesso, 0 avisos e 0 erros.
- `dotnet run --project .\tests\Optimizer.Core.Tests\Optimizer.Core.Tests.csproj --configuration Release --no-build`: **94/94** checks do harness aprovados. Os novos checks simulam correspondência exata, bloqueio de ambiguidade/Compatible ID genérico e reconhecimento por SHA. São verificações de lógica Core, não testes de assinatura ou instalação real.
- Validações reais dos ZIPs e WMI são reportadas separadamente acima; não houve teste funcional dos drivers nem validação visual da janela.
- Pendências: candidatos adicionais/OEMs e versões de outros pacotes; parser offline para arquivos desconhecidos; política completa de versão/compatibilidade; fluxo de instalação/reversão; testes funcionais em equipamento e Windows compatíveis; inspeção visual desta tela e pendências visuais históricas preservadas. Nenhuma instalação é proposta nesta etapa.

## 27. Preview autocontido R2, validação visual e estado integral do plano — 2026-10-20

### Correção encontrada na inspeção e origem

- A primeira Preview de 20/10 foi instalada sobre a Preview existente e abriu. Na navegação para **Registro**, o processo encerrou com `System.InvalidOperationException`: uma associação WPF bidirecional tentava atualizar `CurrentStateText`, propriedade somente leitura. O evento .NET foi `0xe0434352`. Nenhuma leitura/escrita de `HideFileExt` ou outra preferência ocorreu antes da falha.
- Corrigi as três associações `Run.Text` de `CurrentStateText`, `TargetText` e `ConsequenceText` com `Mode=OneWay` em `src/Optimizer.App/Views/RegistryPage.xaml`. A compilação seguinte abriu Registro normalmente; a janela permaneceu em execução ao percorrer as páginas.
- Commit de origem da publicação final: `bb3c5eb60c310cbaaa18d20f370b67c7da10d81a`. A primeira Preview e seus artefatos permanecem preservados em seus caminhos anteriores; não são os arquivos recomendados abaixo.

### Build e artefatos R2

- SDK chamado explicitamente: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão `10.0.402`; `global.json` manteve `10.0.100` com `latestFeature`. Release compilou com 0 avisos/0 erros. Harness Core: **94/94**, todos checks do motor em adaptadores simulados; não são 94 validações reais do Windows.
- `dotnet publish` produziu publicação Windows x64 `--self-contained true` com 406 arquivos; `coreclr.dll` e `hostfxr.dll` foram confirmados na publicação e na instalação. O mesmo diretório publicado originou ZIP e Setup.
- Inno Setup 7.1.0 compilou `packaging/CalixtoOptimizer-Preview-20261020-r2.iss`; compilação bem-sucedida. Instalador sem assinatura digital própria.

| Artefato | Caminho | Tamanho | SHA-256 |
| --- | --- | ---: | --- |
| ZIP portátil | `artifacts/preview-v0.6.0-experimental-selfcontained-20261020-r2/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-portable.zip` | 65.599.195 bytes | `20FAD64165F5166F5A7FDFB46BF30724ACBF2EE9BE649EEF07F10DA63031EF0D` |
| Instalador EXE | `artifacts/installer-preview-20261020-r2/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-setup.exe` | 46.799.063 bytes | `ADE4D74A0C7E75E86BE70C9E722FCE758D0C01EABC73CE99911577DCF95966DD` |

### Upgrade e inicialização

- Atualizei a instalação Preview existente, usando o mesmo AppId e diretório por usuário, com código de saída do Setup `0`. Não havia processo do app aberto durante a instalação e o script define `CloseApplications=no`; nenhum processo foi encerrado pelo instalador.
- Comparei os 406 arquivos do payload com a publicação por SHA-256: **406 iguais, 0 divergentes**. O diário `%LOCALAPPDATA%\CalixtoOptimizer\History` tinha dois arquivos; nome relativo, tamanho e hash de ambos permaneceram idênticos ao estado anterior. Não foi aberto nem regravado pelo teste de upgrade.
- O atalho `Calixto Optimizer Preview 20261020.lnk` aponta para `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019\Optimizer.App.exe`, que é o destino instalado. O atalho antigo 20261019 foi removido. O ProductVersion do executável instalado inclui o commit `bb3c5eb...`.
- Iniciei o executável instalado após o upgrade. A janela abriu e permaneceu em execução. Instalação limpa e desinstalação do R2 não foram testadas; outro PC segue pendente.

### Inspeção visual das páginas R2

A janela foi capturada diretamente com `PrintWindow`, sem trazê-la para a frente ou usar o mouse/teclado físico. A automação navegou pelos itens da barra e acionou apenas leitura local, importação de hash e rolagem. As capturas não foram versionadas; as de Processo e Rede foram descartadas por conterem dados locais.

- **Drivers:** inventário WMI real, 183 linhas. A página de candidatos mostrou os dois pacotes ASUS — Ethernet RTL8111H e Wi-Fi RTL8821CE —, pacote instalado reportado pelo Windows, origem, versão/data, sistema-alvo, correspondência de Hardware ID, validações do ZIP/catálogo/INF e pendências. A UI exercitou o reconhecimento por SHA-256 dos dois ZIPs locais conhecidos (fluxo de importação somente leitura). O R2 visual usa o mesmo código de Drivers do build anterior; não extraiu, abriu instaladores nem instalou driver. Hash reconhecido não valida ZIP genérico nem comprova a origem do Wi-Fi, cujo fornecedor não publicou hash.
- **Processos:** leitura local exibida, com processos inacessíveis/encerrados durante a enumeração e prioridade indisponível nesses itens. Uma leitura desta sessão reportou 280 listados, 108 com acesso negado e 1 encerrado durante a leitura. Isso descreve a consulta do Windows; nenhum processo foi encerrado por nós e nenhum setter de prioridade foi chamado. Não selecionei um alvo, nem gravei captura.
- **Registro e políticas:** página abriu sem exceção e mostrou escrita bloqueada, compatibilidade/gerenciamento organizacional e histórico sem captura nesta sessão. O texto indicou que nenhuma leitura de Registro foi solicitada. Não consultei `HideFileExt`, não capturei valor e não alterei Registro ou política.
- **Rede:** adaptadores/configuração local carregaram; a UI apresentou a finalidade e o limite de até quatro sondagens ICMP de 1.000 ms. Não iniciei teste, DNS ativo ou ping nesta etapa; a configuração de rede não foi alterada.
- **Planos/Energia:** telas de VIDEOIDLE e modo de energia mostraram explicitamente “sem leitura real”, AC/DC/plano não consultados e aplicação/restauração bloqueadas. Não chamei getters, setters, captura ou restauração durante esta inspeção.
- O aplicativo ficou aberto na página Drivers para o checklist do usuário. A inspeção não alterou energia, Registro, política, rede, prioridade, processos, driver ou firmware.

### Matriz completa atual do plano oficial

| Fase/capacidade | Diagnóstico/leitura | Plano/configuração | Execução real | Estado atual, evidência e pendências |
| --- | --- | --- | --- | --- |
| 0 — Ambiente e decisões | Windows x64, versão/build, SDK e ferramentas identificados localmente. | Regras e limites estão documentados. | Build, teste local e upgrade R2 executados. | **Parcial** — nenhum segundo PC; ambiente e hardware externos pendentes. |
| 1 — Inventário do sistema, dispositivos, drivers e energia | WMI/PnP, identidade Windows, planos e adaptadores consultados localmente. | Inventário não altera esses estados. | Leituras locais; não há mutação automática. | **Concluído neste host no escopo implementado**; limites/permissões e outros PCs pendentes. |
| 2 — Catálogo, compatibilidade, perfis, consequências, consentimento, diário, recuperação e simulador | Prévia baseada em leitura real onde o módulo oferece getter. | Perfis e fluxo simulado existem; catálogo real de ajustes permanece pequeno e sem itens aprovados na tela Planos. | **Simulado** para o motor; snapshots sintéticos não restauram Windows. | **Parcial** — comportamento de simulação testado; plano real útil ainda incompleto. |
| 3 — Primeiro ajuste reversível | VIDEOIDLE e modo de energia têm leitura/captura locais; processo prioritário tem identidade/estado capturados. Preferência HideFileExt permanece sem leitura atual nesta etapa. | Plano, consentimento e diário são integrados, com conflitos/recuperação condicional. | Setters de energia e HideFileExt permanecem bloqueados. Um ciclo real do serviço de prioridade foi testado antes somente no probe descartável aprovado e restaurado. | **Parcial** — nenhum ajuste de energia ou preferência de usuário foi aplicado neste ciclo; teste do probe não autoriza novas alterações. |
| 4 — Energia, Registro, políticas, processos e Rede | Energia/planos, leitura de processo, adaptadores e diagnóstico de rede existem; Registro tem tela e chave de teste isolada anterior. | Energia, prioridade e HideFileExt têm fluxos de revisão/captura com escopo limitado; política organizacional bloqueia quando desconhecida. | Energia/HideFileExt/políticas continuam sem execução habilitada. Ciclos aprovados anteriores de probe e chave isolada não são configuração do usuário. Rede ativa foi testada em etapa anterior; não foi repetida agora. | **Parcial** — módulos reais e simulados não equivalem a capacidade ampla de aplicar/restaurar; política e vários ajustes indisponíveis. |
| 5 — Integrações GPU | WMI informa controladores; acesso às configurações nativas existe. | Preferência por app não é lida pelo Calixto. | Nenhuma configuração nem telemetria programática de GPU. | **Parcial** — GPU presente, preferência configurada e GPU efetivamente usada continuam distintas. |
| 6 — Instalação assistida de drivers | Inventário, IDs e dois candidatos ASUS revisados para este host; ZIP local reconhecido apenas por hashes conhecidos. | Não há seleção genérica de pacote offline nem plano completo de recuperação. | **Não implementada/bloqueada** — nenhum driver instalado, atualizado ou removido. | **Parcial** — correspondência e revisão existem; parser genérico offline, elevação segura, instalação e recuperação ainda ausentes. |
| 7 — ZIP, Setup e recuperação/distribuição | Conteúdo/hash dos artefatos registrados. | Publicação autocontida e instalador per-user R2 compilados. | Upgrade R1→R2 no host passou; arquivos, histórico, atalho e abertura conferidos. | **Parcial** — instalação limpa, desinstalação R2, assinatura, segundo PC e recuperação em falha pendentes; empacotamento não conclui o produto. |
| 8 — Controles específicos de hardware/firmware | Nenhuma matriz por modelo/firmware validada. | Integração por fabricante não iniciada. | BIOS, firmware, tensão e frequência não são escritos. | **Não iniciado** e bloqueado pelos requisitos de equipamento/integrador e validação específicos. |

### Próxima etapa

A próxima entrega pequena prevista é ampliar a conferência offline de pacotes locais desconhecidos para ler INF/catálogo, validar assinatura e corresponder Hardware IDs, arquitetura e Windows, ainda sem executar ou instalar. Depois, permanece necessário desenhar um fluxo de instalação segura por dispositivo, com recuperação; os candidatos atuais não autorizam instalação.

## 28. Análise genérica de pacote local e preparação assistida de driver — 2026-10-20

Continuação a partir do estado do Preview R2, preservado. Não gerei outro instalador/ZIP nem alterei os artefatos R2. O aplicativo Preview já instalado/aberto não foi substituído. Nenhum instalador OEM foi executado e nenhum driver foi instalado, staged, associado, removido ou substituído.

### Implementação

- `LocalDriverPackageAnalyzer` analisa `.inf` e `.zip` localmente, sem executar conteúdo. ZIP limitado a 128 MiB compactado, 512 MiB expandido, 128 MiB por arquivo e 4096 entradas; rejeita traversal/absolutos/duplicados/symlinks, confere CRC32 e comprimentos e extrai somente para uma pasta GUID própria em `%TEMP%\CalixtoOptimizer\DriverAnalysis`. Arquivos são removidos ao descartar a análise. INF direto é lido sem extrair ou modificar a pasta original; links/reparse points são ignorados e há limites equivalentes.
- O parser cruza Hardware IDs/Compatible IDs PnP, arquitetura, decoração de OS/build e ProductType/SuiteMask quando compreendidos. INF auxiliar (`Autorun.inf`) não aparece como driver PnP; no seletor direto, o resultado explica que o arquivo é incompleto. Correspondência só por Compatible ID, múltiplos devnodes e sintaxe não entendida não geram prévia instalável.
- Para arquivo aceito como candidato, `SetupVerifyInfFileW` verifica associação/assinatura INF-catálogo. Arquivos declarados por `SourceDisksFiles` são localizados sem ambiguidade, verificados como membros do catálogo por WinTrust e recebem hashes SHA-256. Arquivos necessários ausentes ou diretivas que o parser não resolva deixam resultado como **Análise incompleta**. A assinatura não é usada como prova de adequação funcional.
- Resultados discriminam **Compatível segundo verificações**, **Incompatível**, **Ambíguo** e **Análise incompleta**, com razões por INF. A tela Drivers apresenta os candidatos, IDs resumidos, assinatura/catálogo, integridade, origem do arquivo, detalhes do driver atual e prévia de efeitos/permissões/recuperação.
- `DriverPackageInstallationService` reutiliza catálogo/plano, consentimento e diário existentes; captura leitura atual, hash do arquivo de origem (ZIP completo ou INF escolhido) e hashes separados dos arquivos necessários antes de qualquer etapa. A política de produção é `BlockedDriverPackageMutationPolicy`. Consentir e capturar não autoriza a execução.
- `PnpUtilDriverPackageInstaller` implementa separadamente `/add-driver <inf>` (staging no Driver Store) e `/add-driver <inf> /install` (instalação em dispositivos correspondentes). Não executa Setup OEM, não deleta pacotes, não usa `/force`/`/reboot` e não força downgrade. Exige processo já elevado, sem worker/UAC. A produção permanece bloqueada antes da chamada. O serviço relê o INF ativo no mesmo devnode após a etapa de associação, mas associação reportada não comprova que a função esteja operacional.
- Limite conhecido: o comando documentado `/install` age sobre todo dispositivo correspondente, não aceita instance ID de um único devnode. A verificação exige um alvo único e relê o estado antes do comando, porém uma mudança concorrente/hot-plug continua possível. Não liberar essa associação real sem restringir o escopo por devnode ou obter aprovação explícita para o alcance demonstrado e testar em ambiente recuperável.

### Validação real dos dois ZIPs ASUS nesta máquina

Probe temporário usou a leitura WMI real de dispositivos e APIs nativas do Windows apenas para verificação; não chamou PnPUtil. Inventário disponível: 193 linhas, Windows `10.0.26300.0`, x64/`amd64`.

| Pacote revisado localmente | Estado do analisador nesta compilação | Evidência e limite |
| --- | --- | --- |
| ASUS LAN RTL8111H `1168.27.50.919` (`3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D`) | `rt68cx21x64.inf`: compatível segundo verificações; um Hardware ID exato; catálogo/INF e payload confirmados. | 722 entradas/34.802.917 bytes. Assinante Microsoft Windows Hardware Compatibility Publisher. A origem ASUS é sustentada pelo hash oficial publicado coincidente. Não é veredito de superioridade/necessidade nem teste de instalação/funcionamento. |
| ASUS Wi-Fi RTL8821CE `2024.10.138.3` (`E061EBD3E77F54DA07ECED9876DB54DCAA46E871DB4015FEE6EC1F2987535647`) | `netrtwlane.inf`: **incompleto**. `Autorun.inf` foi ignorado como auxiliar; um Hardware ID exato e catálogo/INF assinado foram detectados. | 37 entradas/31.111.699 bytes. `rtlCoInst.dll` e `rtlCoInst.dat` declarados em `SourceDisksFiles` não estão no ZIP; staging/prévia não elegíveis. ASUS não publica hash do ZIP Wi-Fi; hash é apenas fingerprint da cópia local. |

Os resultados completos e os candidatos ASUS estão em `docs/DRIVER-ASSISTED-INSTALLATION.md`. Pacotes e extrações continuam ignorados pelo Git. Nenhuma configuração do Windows foi alterada.

### Build, testes e validação visual

- Raiz Git: `<raiz do repositório>`; branch `main`, Preview R2 preservado. SDK explícito `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402.
- `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore`: **sucesso**, 0 avisos, 0 erros.
- Harness `Optimizer.Core.Tests` Release: **100/100** verificações. ZIP traversal/limites, raiz do ZIP, INF auxiliar, IDs exatos/genéricos, incompatibilidade, ambiguidade, payload ausente, hash, preview/consentimento/diário e separação staging/associação são checks de modelo com verificadores/instaladores simulados. Eles não comprovam instalação real de drivers.
- Validação real separada: leitura WMI atual (193 linhas), assinatura/membership via SetupAPI/WinTrust e conteúdo dos dois ZIPs locais. LAN passou nos checks implementados; Wi-Fi permaneceu incompleto pelos arquivos ausentes. Nenhuma chamada PnPUtil, Setup ou API de instalação ocorreu.
- A solução WPF compilou, mas a janela desta alteração **não foi aberta nem inspecionada**; integração visual compilada não é validação visual. A antiga instalação R2 permanece preservada e não contém este código mais novo.

### Matriz atual do plano oficial

| Fase/capacidade | Leitura/diagnóstico | Plano/revisão | Operação real | Estado e pendências |
| --- | --- | --- | --- | --- |
| 0 — Ambiente | Host Windows x64, SDK e hardware locais identificados. | Limites de segurança documentados. | Builds/testes locais. | **Parcial**: nenhum outro PC. |
| 1 — Inventário | WMI/PnP, energia, rede e identidade Windows consultados nos módulos implementados. | Inventário é leitura. | Nenhuma otimização aplicada nesta entrega. | **Concluído neste host no escopo consultado**; permissões e outros PCs pendentes. |
| 2 — Catálogo, perfis, consentimento, histórico/recuperação | Estado real disponível onde existe getter. | Perfis, consequências, consentimento e diário integrados em parte. | Harness simulado; snapshots não são backups do Windows. | **Parcial**: catálogo real útil e recuperação operacional limitados. |
| 3 — Ajustes reversíveis iniciais | VIDEOIDLE e energia têm leitura; prioridade/processo e preferência de Registro têm fluxo preparado. | Planos/capturas específicos existentes. | Energia/Registro seguem bloqueados; probe de prioridade aprovado anteriormente é um teste isolado, não autorização permanente. | **Parcial**; não foi aplicado ajuste nesta etapa. |
| 4 — Energia, Registro, políticas, processos e rede | Leituras e telas variam por módulo; rede ativa foi validada em etapa anterior. | Alguns módulos têm revisão/consentimento/histórico; políticas continuam limitadas. | Alterações reais de energia/Registro/rede/prioridade não autorizadas aqui e/ou bloqueadas por padrão. | **Parcial**: amplitude e validação visual pendentes; Processo ainda tem checklist visual histórico. |
| 5 — GPU | Inventário de GPU presente; configurações nativas acessíveis. | Preferência por app não comprovada pelo app. | Nenhuma escrita/telemetria da GPU. | **Parcial**; seleção configurada e uso efetivo não são equivalentes nem consultados. |
| 6 — Drivers assistidos | Inventário atual e análise real dos dois ZIPs ASUS. A análise agora resolve seção Models/DDInstall decorada e payload alcançável por CopyFiles; LAN e Wi-Fi compatíveis segundo os checks atuais. | UI mostra motivos, seção aplicável, arquivos necessários, dependências do Windows, dispositivos correspondentes e alcance amplo do PnPUtil; prévia/consentimento/captura permanecem separados das operações. | Staging/associação do backend PnPUtil continuam bloqueados por política padrão; nenhuma chamada real. `DiInstallDevice` é documentada para devnode específico, mas ainda não integrada. | **Parcial, não concluído**: inspeção visual deste build pendente por erro de vínculo do CUA; staging, associação direcionada, validação funcional, rollback/desinstalação e outro PC pendentes. |
| 7 — Publicação/Setup | Artefatos R2 previamente registrados. | R2 autocontido e upgrade prévio testado. | Nenhum novo pacote gerado nesta fatia. | **Parcial**: instalação limpa, desinstalação R2 e outra máquina pendentes; R2 não contém estas mudanças. |
| 8 — Firmware/controles específicos | Sem matriz por equipamento. | Integração não iniciada. | Não há escrita em BIOS/firmware/tensão/frequência. | **Não iniciado**; requisitos específicos seguem bloqueando. |

### Próximo teste candidato e pendências preservadas

Nenhum teste real de driver está autorizado por esta entrega. Para solicitar um teste futuro, apresentar novamente os dados atuais do pacote LAN, seu estado de driver e alvo, resolver/mitigar o alcance global de `/install`, e submeter um plano para VM ou máquina sobressalente com rede alternativa, exportação do driver atual e console local. Executar primeiro staging, verificar Driver Store, obter segunda aprovação para associação, reler o mesmo devnode e testar conectividade sem reinício automático; sem rollback automático. Se não houver ambiente recuperável, não pedir alteração neste PC.

Permanecem pendentes: inspeção visual da interface deste build; instalação limpa e desinstalação do Preview/Setup R2; teste de outro PC; validação funcional real de associação de driver, rede após troca e recuperação. A instalação limpa/desinstalação de R2 não pode ser inferida do upgrade anterior.

## 29. Correção de dependências INF e alcance PnP — 2026-10-20

Continuação a partir do commit `e817913a2b4dc6783f525c7c4de770493d6e4829`; Preview R2 e seus artefatos foram preservados. Nenhum pacote novo foi gerado. Nenhum `setup.exe`, PnPUtil, staging, associação, instalação, remoção ou alteração do Windows foi executado.

### Implementação

- `LocalDriverPackageAnalyzer` agora interpreta o primeiro campo da linha Models como seção DDInstall; só campos seguintes são IDs. Seleciona a variante aplicável à arquitetura/Windows/build e deriva arquivos necessários somente dos `CopyFiles` das seções selecionadas e relacionadas.
- Escolhe `[SourceDisksFiles.<arch>]` antes do mapa genérico e confirma que cada arquivo copiado tem entrada correspondente. Lista definida mas nunca referenciada, arquivo de outra arquitetura ou DDInstall para build futuro deixa de bloquear o alvo atual. Diretivas/caminhos parametrizados ou não resolvidos continuam bloqueando.
- `WindowsSystemInfDependencyResolver` verifica em modo leitura se cada `Include` aplicável existe sob `%WINDIR%\INF` e se cada seção `Needs` ocorre em exatamente um dos arquivos incluídos. Essa conferência não valida assinatura/autenticidade desses INF do sistema e não percorre recursivamente as diretivas internas das dependências incluídas; detalhes são expostos no resultado da análise.
- A página Drivers mostra seção DDInstall e dependências avaliadas, todos os dispositivos atuais correspondentes, ID do pacote, limitações de alcance do PnPUtil e a separação entre análise, staging no Driver Store e associação a devnode. A associação de produção continua bloqueada. Consentimento/captura não autorizam staging ou associação.
- Escopo: PnPUtil `/install` não recebe instance ID e pode afetar todo dispositivo correspondente, incluindo um conectado após a enumeração. A análise lista o conjunto encontrado naquele inventário, mas isso não restringe o comando. A documentação oficial `DiInstallDevice` instala um driver já presente no Driver Store para `SP_DEVINFO_DATA` específico e aceita `SP_DRVINFO_DATA` para escolher pacote; essa alternativa ainda não foi implementada. Não alegar alvo único até usar API direcionada ou aprovar o alcance completo.

### Revisão do Wi-Fi e análise real dos pacotes

- A linha Models aplicável ao ID local aponta para `ASUS8821ce_FCC_REG.ndi`; neste Windows x64 selecionou `[ASUS8821ce_FCC_REG.ndi.NT]`. O `CopyFiles=RTWlanE.CopyFiles` alcança `rtwlane.sys` e `rtldata.txt`, ambos presentes e verificados como membros do catálogo.
- As seções selecionadas referenciam `netvwifibus.inf` e `machine.inf` por Include/Needs. O Windows deste host contém esses INF e as quatro seções Needs selecionadas. O resolver valida arquivo/seção existente; não afirma validação recursiva ou de assinatura dessas dependências.
- `rtlCoInst.dll` e `rtlCoInst.dat` aparecem em `[SourceDisksFiles]` e na definição `[CoInstaller.CopyFiles]`, mas nenhum `CopyFiles=CoInstaller.CopyFiles` ou `AddReg=CoInstaller.AddReg` é alcançável pelas seções de instalação aplicáveis ao modelo/arquitetura/build atual. O analisador não exige nem baixa essas DLLs avulsas.
- Probe WMI real: inventário disponível, 196 linhas, zero avisos; Windows 11 Professional, build 26300.9711, amd64.
- LAN `rt68cx21x64.inf`: uma correspondência Hardware ID exata; `RTL8168H.ndi.NT`; payload obrigatório `rt68cx21x64.sys`; SetupAPI/WinTrust confirmou INF/catálogo/payload. Resultado **compatível segundo as verificações realizadas**.
- Wi-Fi `netrtwlane.inf`: uma correspondência Hardware ID exata; `ASUS8821ce_FCC_REG.ndi.NT`; payloads `rtwlane.sys` e `rtldata.txt`; SetupAPI/WinTrust confirmou INF/catálogo e membership dos dois payloads. Include/Needs do Windows encontrados. Resultado **compatível segundo as verificações realizadas**. O SHA-256 do ZIP Wi-Fi é apenas local (ASUS não publica digest para ele).
- Esses estados são análise de adequação declarada e integridade conforme checks; não provam que o Windows escolherá o pacote, superioridade, instalação, conectividade ou funcionamento. Nenhum pacote foi instalado ou staged.

### Interface visual

- Autorização específica para abrir/navegar apenas a página Drivers foi recebida (“pode”). Iniciei o executável Release de `src/Optimizer.App/bin/Release/net10.0-windows/Optimizer.App.exe` separadamente do Preview instalado. O processo do build atual continua aberto.
- A enumeração visual retornou o HWND da janela do build com o AppID do Preview instalado. `sky.get_window` recusou tanto a captura inicial quanto uma nova tentativa após atualizar `list_windows`: “window id 279447742 no longer belongs to ...Optimizer-Preview-20261019\\Optimizer.App.exe; current owner is ...”. Segui a regra do helper e parei, sem reutilizar coordenadas/índices ou disputar controles. **Análise, prévia, consentimento e histórico não foram aprovados visualmente** nesta execução.
- Nenhum consentimento foi clicado e nenhum registro de histórico foi criado pela inspeção.

### Build e testes

- SDK chamado explicitamente: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402; `global.json` não foi alterado.
- Build Release final: **sucesso**, 0 avisos e 0 erros. Antes do build foi necessário fechar apenas o processo Release iniciado nesta etapa (PID 9280), pois ele mantinha o apphost bloqueado. Harness Core final: **103/103** testes de modelo aprovados.
- Probe real final, somente leitura: inventário WMI `Available`, 196 dispositivos, zero avisos; Windows 11 Professional build 26300.9711, x64. LAN `rt68cx21x64.inf` compatível segundo os checks, ID exato único, payload `rt68cx21x64.sys`; Wi-Fi `netrtwlane.inf` compatível segundo os checks, ID exato único, payload `rtwlane.sys` e `rtldata.txt`; Include/Needs do Windows presentes. Catálogos/INF/payloads passaram pelas verificações SetupAPI/WinTrust aplicáveis.
- Dependência adicional LAN: `[RTL8168H.ndi.NT.HW]` inclui `pci.inf` e Needs `PciIoSpaceNotRequired.HW`; o INF e seção também foram encontrados em `%WINDIR%\INF`. Assim como nos INF Wi-Fi, a checagem só valida disponibilidade e nome da seção, não assinatura nem cadeia recursiva.
- O probe não chamou ferramentas de instalação e não escreveu estado do Windows. Não comprova instalação, rank escolhido, função real, conectividade após troca, rollback ou desempenho.
- Inspeção visual: falhou a associação segura do HWND pelo CUA; a validação ficou pendente, registrada com o erro literal acima. Nenhum consentimento/captura foi clicado/criado. A lista curta para concluir manualmente é: importar ZIP Wi-Fi ou selecionar INF; conferir `Compatible`, seção DDInstall e os dois payloads; conferir aviso de alcance amplo do PnPUtil; abrir prévia e ler consentimento/limites; verificar que staging/associação continuam bloqueados; visitar Histórico sem criar nova captura. Não marcar visualmente aprovado antes de conferir.

### Próxima etapa e limites mantidos

Continuam pendentes: inspeção manual/visual da tela atual; implementação direcionada de associação por `DiInstallDevice` após staging; instalação de driver somente após aprovação específica; validação funcional, recuperação/desinstalação e outro PC. O resultado de leitura de pacote não é autorização para instalar.

## 30. Associação direcionada preparada e dependências INF recursivas — 2026-10-20

Continuação a partir do commit `516184eaa7f95e5f1e67a65833b6aa92d89c3c6b`. Nenhum instalador foi gerado. Nenhum staging, associação, instalação, remoção, reinício ou outra mutação do Windows foi executado. As declarações de dependência direta e backend PnPUtil nas seções históricas anteriores foram substituídas por esta etapa.

### Implementação

- Backend de staging/associação usa APIs documentadas do Windows: `SetupCopyOEMInfW` para staging separado e `DiInstallDevice` com `SP_DEVINFO_DATA` e `SP_DRVINFO_DATA` explícitos para a associação de um devnode e candidato.
- A enumeração lê ranks de drivers compatíveis do alvo; o pacote staged só pode ser selecionado se aparecer exatamente uma vez e for o único candidato de melhor rank. Ausência, empate, rank inferior ou recusa do Windows bloqueiam. Nenhuma opção de força, compatibilidade ignorada ou downgrade é usada.
- Antes da operação, o serviço relê inventário/Hardware ID/INF atual. O adaptador nativo repete a validação de instance ID, Hardware ID e INF ativo imediatamente antes de chamar a API e chama `CM_Locate_DevNodeW` com `CM_LOCATE_DEVNODE_NORMAL`; dispositivo removido, devnode trocado ou identidade divergente são recusados. A validação não é atômica com a chamada; há uma janela de corrida residual.
- Estados permanecem distintos no serviço/diário: staging concluído (não associado), associação pendente de reinício (sem verificação do driver ativo), associação não verificada e associação verificada por nova leitura WMI do mesmo devnode/INF. O app nunca reinicia o PC; mesmo uma leitura do INF ativo não prova que o hardware funciona.
- Staging altera o Driver Store do computador e pode disponibilizar o pacote a futuras instâncias correspondentes. Diretivas e seções de instalação como `CopyINF`, serviços, filtros e componentes podem trazer efeitos adicionais além do devnode alvo. O app não promete isolamento total nem remove automaticamente pacotes/efeitos parciais.
- `Include`/`Needs` das seções aplicáveis são resolvidos recursivamente no INF de sistema. O resolver mantém o nome literal (inclusive decoração) que a diretiva `Needs` refere, requer uma única seção proprietária, detecta ciclo/ambiguidade e limita a 64 INF, 32 níveis, 256 seções alcançadas, 16.384 cabeçalhos, 8 MiB/arquivo e 32 MiB totais. Referência necessária ausente, parametrizada, fora de escopo, inacessível ou além dos limites dá análise incompleta e impede elegibilidade; não é presumida compatível.
- `Include`/`Needs` vazios ou com campos vazios não são descartados silenciosamente; `CopyINF` encontrado em dependência do sistema, `AddSoftware` aplicável e `AddReg` alcançável em `.CoInstallers` bloqueiam elegibilidade enquanto seus efeitos/dependências não forem verificados. Isso fecha falsos positivos por diretivas de instalação que poderiam adicionar pacotes, instalar software à parte ou registrar DLL de co-installer.
- Limitações: assinatura dos INF incluídos de `%WINDIR%\INF` não é revalidada; parser não representa todos os efeitos/gramáticas do SetupAPI. Construções que não são verificadas integralmente continuam bloqueadas. O verificador não interpreta nem autoriza efeitos sistêmicos adicionais declarados pelo INF.
- Produção continua composta com `BlockedDriverPackageMutationPolicy`; staging e associação reais param antes do backend. A UI e a captura/consentimento não habilitam a execução.

### Testes e validação

- SDK explicitamente usado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão 10.0.402; `global.json` não foi alterado.
- Harness Core: **109/109** checks simulados aprovados. Cobre dependência recursiva/ciclo, seção decorada explícita, dependência ausente, `CopyINF` alcançável, entradas `Include`/`Needs` vazias, `AddSoftware`, registro de co-installer, limites, melhor rank único versus rank inferior/empate/ausente, dispositivo removido, Hardware ID alterado, staging com falha, associação recusada e reinício pendente. O teste de reinício confirma que nenhum resultado de driver ativo é declarado antes de nova leitura.
- A build Release normal encontrou bloqueio de arquivo: o executável aberto para revisão visual (PID 24540) segurava `Optimizer.App.exe`. Sem encerrá-lo, `dotnet build ... -p:UseAppHost=false` compilou toda a solução com 0 erros e 1 aviso MSB3061 pelo apphost em uso. Os 109 checks foram executados por `dotnet.exe` sobre a DLL do harness. A janela foi preservada.
- Os testes usam dispositivos, INF e adaptadores simulados/descartáveis. Não chamam `SetupCopyOEMInfW`, `DiInstallDevice` nem qualquer setter/ação real de driver.
- `DiInstallDevice`/P/Invoke passou por compilação; a compilação não valida chamada nativa, assinatura binária, staging ou associação no Windows. Nenhuma operação real foi executada.
- Probe real somente leitura após os bloqueios conservadores: inventário WMI `Available`, 189 linhas, zero avisos; Windows 11 Professional 26H2 build 26300.9711, x64. LAN `rt68cx21x64.inf` e Wi-Fi `netrtwlane.inf` retornaram **Compatível segundo as verificações realizadas**, cada um com uma correspondência exata e prévia elegível. LAN resolveu `pci.inf`/`PciIoSpaceNotRequired.HW` recursivamente (1 INF, 1 seção); Wi-Fi resolveu 3 INF e 5 seções. Hashes locais dos ZIPs e resultados de assinatura/INF/payload foram rechecados. Isso não executou seleção de rank nem staging/associação.
- Para a lista PnP, o adaptador configura `DI_FLAGSEX_ALLOWEXCLUDEDDRVS`, requerido por `SetupDiBuildDriverInfoList` para enumerar arquivos de driver PnP segundo a documentação Microsoft. Isso inclui `ExcludeFromSelect` na lista; não usa flag de força em `DiInstallDevice`, e o candidato continua obrigado a ser compatível e único melhor rank. A documentação também observa que PnP costuma estar excluído de seleção manual; a operação permanece bloqueada por padrão e sem validação nativa.

### Interface visual e pendências

- Iniciei o executável Release desta raiz. CIM confirmou PID **24540** em `<raiz do repositório>\src\Optimizer.App\bin\Release\net10.0-windows\Optimizer.App.exe`. `sky.list_apps` retornou uma janela `Optimizer.App.exe`, mas o AppID/path owner era `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019\Optimizer.App.exe`, diferente do PID/caminho confirmado. Parei antes de chamar `get_window`, capturar ou clicar; não contornei o bloqueio de identidade. A interface desta compilação não foi validada visualmente. O executável Release permanece aberto para checklist manual.
- Checklist manual consolidado permanece válido: Drivers (candidatos, pendências, prévia, escopo e bloqueio), Processos (seleção e consequência sem encerrar/aplicar), Registro (prévia e escrita bloqueada), Rede (carregamento sem sondagem) e Energia (diagnóstico sem setters); conferir histórico apenas como captura revisada e não como ajuste aplicado. Nenhum item visual é promovido a aprovado sem observação.
- Staging real em chave/driver de teste, associação real, instalação limpa, rollback/desinstalação, validação de conectividade e teste em outro computador permanecem pendentes e requerem aprovações/plano próprio. A ausência de teste real não é coberta pelos 108 checks simulados.
- Nenhum novo instalador/ZIP foi produzido; Preview R2 permanece preservado e não contém as mudanças desta etapa.

## 31. Catálogo pós-formatação e execução WinGet bloqueada — 2026-10-20

Continuação a partir do estado limpo do commit `be75cc5b08c4211f8a7a995390c5c9b4aa2f46f0`. O código de drivers das etapas anteriores foi preservado. Nenhum instalador foi gerado; nenhum driver ou programa foi instalado; nenhuma configuração do Windows foi alterada.

### Estado de drivers ao fechar a etapa anterior

- **Implementação parcial:** o código de análise INF/dependências, staging e associação direcionada via `DiInstallDevice` existe; a composição ainda mantém `BlockedDriverPackageMutationPolicy`.
- **Validação simulada:** o harness anterior cobre seleção e estados de falha/associação, mas não chama APIs nativas.
- **Validação real pendente:** nenhuma execução nativa validou staging, associação, resultado ativo ou recuperação. Não há instalação de driver autorizada nesta entrega. A linha atual do plano permanece **Parcial**, não concluída.
- **Visual pendente:** a checagem de identidade da janela divergiu entre a ferramenta e o PID/caminho conhecido. Não foi contornada; permanece o checklist manual consolidado em `docs/TESTING.md`.
- Antes deste build, foi pedido que o usuário fechasse a janela que poderia manter `Optimizer.App.exe` bloqueado. O usuário confirmou o fechamento e o build normal Release passou conforme registrado abaixo; nenhum processo foi encerrado e `UseAppHost=false` não foi usado.

### Implementação da primeira fatia de programas

- Catálogo local fechado em dois IDs exatos: `7zip.7zip` e `VideoLAN.VLC`, origem WinGet `winget`. Os manifests x64 e metadados de licença/publicador/escopo/elevação foram verificados diretamente no repositório público `microsoft/winget-pkgs` em 2026-10-20; snapshot e URLs estão em `docs/PROGRAMS-AFTER-FORMAT.md`. O repositório é comunitário. Hash publicado no manifest não foi recalculado localmente porque não houve download.
- A página “Programas pós-formatação” oferece seleção múltipla, consulta local somente leitura de entradas de desinstalação e uma revisão por pacote: ID/origem, ação, escopo declarado, licença/acordos, UAC possível e reinício. Ausência/ambiguidade/permissão incompleta não vira afirmação de “não instalado”. Catálogo datado não afirma “mais recente”; WinGet precisa confirmar metadata atual.
- `ProgramInstallCommandFactory` e `ProgramInstallService` preparam execução individual sequencial por ID exato e resultados por pacote. `ArgumentList` não recebe comando arbitrário. O comando exclui força/downgrade, modo silencioso, escopo forçado, bypass de hash e pedido de reinício. Os acordos de origem e pacote são separados e ligados ao pacote.
- **Bloqueio real:** a composição do app injeta `BlockedProgramInstallExecutionPolicy`. “Verificar bloqueio por pacote” só retorna o bloqueio por pacote e não inicia `winget.exe`. O runner WinGet não foi executado; não instalar WinGet automaticamente.
- Detecção é local e só lê chaves Uninstall em HKLM/HKCU e visões de 32/64 bits; não altera o Registro nem chama WinGet. Ela não prova que um item foi originalmente instalado via WinGet.

### Build e testes desta etapa

- SDK explicitamente chamado: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`; `--version` retornou `10.0.402`. `global.json` não foi alterado.
- `dotnet build tests/Optimizer.Core.Tests/Optimizer.Core.Tests.csproj --configuration Release --no-restore`: sucesso, 0 avisos e 0 erros. O harness Core foi executado diretamente com `dotnet.exe` sobre a DLL. Resultado final após corrigir um assert de capitalização: **121/121 checks simulados aprovados**. Os novos cenários cobrem catálogo restrito, detecção incerta, command factory sem parâmetros arbitrários, bloqueio sem runner, consentimento por pacote, WinGet ausente, pacote não encontrado, conectividade, falha parcial, cancelamento e reinício pendente.
- Após o usuário confirmar que a janela foi fechada, `dotnet build .\CalixtoOptimizer.sln --configuration Release --no-restore` foi executado com o SDK explicitamente chamado `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` (10.0.402): **sucesso, 0 avisos e 0 erros**. A solução completa, incluindo WPF e apphost normal, compilou; não foi usado `UseAppHost=false` e nenhum processo foi encerrado.
- O harness foi executado novamente após o build Release: **121/121 checks simulados aprovados**. A compilação não substitui abrir o aplicativo; não houve execução nem inspeção visual desta compilação. O checklist manual continua pendente.
- **Validação real separada:** manifests exatos consultados via API GitHub. Em consulta local anterior, WinGet era `v1.29.380`, a origem `winget` estava configurada e `winget show` não conseguiu atualizar metadata, retornando HTTP 404 (`0x80190194`). Essa falha deixa descoberta atual da origem não confirmada neste host. Não houve runner/instalador real, downloads, UI visual ou validação pós-instalação.

### Matriz atual do plano oficial

| Fase/capacidade | Implementação | Revisão/integração | Simulação | Operação real | Estado e pendências |
| --- | --- | --- | --- | --- | --- |
| 0 — Ambiente e base | SDK 10.0.402 e build normal da solução Release disponíveis; raiz oficial preservada. | Várias páginas integradas; revisão visual deste diff pendente. | Harness Core 121/121. | Outro PC não testado. | **Parcial** — build local completo aprovado; validação visual e em outro PC ausentes. |
| 1 — Diagnóstico/inventário | Hardware, Windows, drivers, energia/plano, rede e processos têm leitores nos escopos existentes. | Drivers/Rede/Processos têm telas; Windows corrigido em etapa anterior. | Harness cobre parsers/contratos. | Leituras locais anteriores; não repetidas como parte desta fatia. | **Parcial por capacidade** — visual de várias telas segue checklist manual; ausência de dado não é falha global. |
| 2 — Catálogo, plano, consentimento e histórico/recuperação | Catálogo de ajustes/perfis, consentimento e diário implementados em parte. | Capturas e consequências estão em alguns fluxos; novo catálogo de programas só revisa, sem registro persistente de instalação. | Harness Core cobre consentimento/diário. | Snapshots simulados não são backup Windows; recuperação real não provada. | **Parcial** — recuperação operacional e histórico de pacote futuro pendentes. |
| 3 — Ajustes reversíveis iniciais (VIDEOIDLE/energia) | Serviços e adaptadores existem. | Revisão/captura e bloqueio disponíveis. | Serviços testados com adaptadores fake. | Nenhuma alteração autorizada nesta etapa; não executada. | **Parcial/bloqueado** — aplicação/reversão reais dependem de aprovação específica. |
| 4 — Registro, políticas, processos e rede | Leitura e alguns serviços de plano/operação existem. | Páginas integradas; Processo permanece sem validação visual aprovada. | Falhas/conflitos/cancelamentos simulados. | Nenhuma escrita nesta etapa; sondagem de Rede não executada. | **Parcial** — políticas/gerenciamento, validação real de mudanças e checklist visual pendentes. |
| 5 — GPU | Inventário GPU e acesso a configurações nativas parcial. | Preferência por app e GPU em uso não são equivalentes. | Não há prova simulada de leitura suportada. | Sem telemetria/escrita GPU. | **Parcial/não disponível** — leitura programática comprovada não implementada. |
| 6 — Pós-formatação: drivers e programas | Análise INF, staging/associação direcionada e catálogo WinGet de 2 pacotes; instalação de programas codificada por ID exato. | Tela Programs com multi-seleção, detecção local e prévia; UI Drivers mantida com bloqueio. | Drivers têm harness; programas: 121 checks Core com fakes, incluindo parcial/cancelamento/reboot. | Nenhum driver/programa instalado; WinGet runner não executado. | **Parcial** — APIs nativas de drivers não validadas; fonte WinGet falhou HTTP 404 local; instalação assistida e recuperação não verificadas. |
| 7 — Distribuição/instalador | Preview R2 existe de versão anterior. | Nenhum pacote regenerado. | Sem novos testes. | Sem instalação/desinstalação nesta entrega. | **Parcial** — R2 não contém o código novo; Setup atual não testado nesta etapa. |
| 8 — Ajustes específicos de hardware/firmware | Matriz CPU/GPU/OEM ausente. | Integração de fabricante/firmware ausente. | Nenhum. | Não autorizada/implementada. | **Não iniciado** — sem escrita em firmware, tensão ou frequência. |

Permanecem no checklist manual: abrir Drivers e conferir candidatos/análise/prévia/bloqueio; Processo e estado da seleção sem encerrar/aplicar; Registro e bloqueio; Rede sem sondagem; Energia sem setter; e conferir Histórico distinguindo consentimento/captura de operação verificada. A ferramenta de inspeção visual não foi usada contra o executável divergente.

### Candidato de pacote para aprovação futura

Uma primeira instalação pequena para possível teste, ainda não aprovada nem executada: **7-Zip**, ID `7zip.7zip`, origem `winget`, manifest x64 revisado `26.04`, licença LGPL-2.1, escopo declarado `machine` e elevação própria declarada pelo manifest. O teste futuro deve primeiro confirmar metadata/installer/hash atuais com WinGet, apresentar o texto vigente dos acordos de origem e pacote, explicar UAC/escopo e eventual reinício, conferir se já está identificado localmente e solicitar autorização específica para instalar apenas esse pacote. O estado desta etapa é apenas candidato.

## 32. Revalidação visual e de metadata WinGet — 2026-10-20

Continuação a partir do commit `7994bf627852e3300e0165d4cbe110eeed70238e`. Nenhum pacote foi gerado; nenhum programa/driver foi instalado; nenhuma fonte WinGet foi alterada e nenhum acordo foi aceito.

### Inspeção visual e leitura local

- A tela Programas foi aberta da DLL Release do repositório usando o SDK .NET 10 local. A identidade da janela foi verificada por HWND → PID `25128`, `dotnet.exe` do SDK e command line apontando à DLL atual. A lista do helper mostrava outra cópia byte-idêntica do `dotnet.exe`; o SHA-256 das duas cópias era igual e o proprietário real do HWND foi confirmado pelo PID antes do uso visual.
- Navegação, catálogo, seleção múltipla (7-Zip + VLC), revisão por pacote (origem, ação, escopo, licença/acordos, UAC, reinício e aviso de snapshot), leitura e cartões de resultado foram vistos. A consulta local leu 89 entradas sem avisos. O botão de bloqueio mostrou `BLOQUEADA` nos dois itens; nenhuma chamada ao runner ocorreu e não havia processo `winget.exe` antes/depois.
- Consulta real do Registro: `7-Zip 26.04 (x64)`, publicador `Igor Pavlov`, versão `26.04`, HKLM. VLC não foi encontrado nas entradas Uninstall das visões/hives consultadas; isso continua sendo “não identificado”, não prova de ausência.
- A tela da DLL do commit `7994bf6` mostrava erroneamente 7-Zip como não identificado porque não reconhecia o nome exato observado. `ProgramCatalog` agora inclui a variante observada e o teste de correspondência foi acrescentado. A interface dessa correção ainda não foi recompilada/reaberta.

### WinGet: configuração, cache e pacote atual

- `winget source list --name winget`: origem padrão `Microsoft.PreIndexed.Package`, URL `https://cdn.winget.microsoft.com/cache`, confiança `Trusted|StoreOrigin`, `Explicit=false`, última atualização registrada `2026-09-30 22:58:55`. `source export` confirmou a mesma configuração. A configuração existe e coincide com a origem padrão documentada; não há evidência de URL/nome ausente ou alterado.
- Os logs WinGet de 16:57 e 16:58 mostram 404 `0x80190194` ao obter arquivo da origem. Em uma tentativa posterior, às 17:19, o log registrou outro erro (`0x87E80034`, `MsixvcStagingSession::GetManifestReader`). `winget show` alertou que a atualização falhou mas retornou metadata em cache `7zip.7zip 26.03`, código de saída 0. O índice local está desatualizado em relação ao manifest público; disponibilidade atual pelo CDN não está confirmada.
- A API GitHub estava acessível e confirmou o manifesto público comunitário 7-Zip `26.04` (2026-10-06), x64 EXE, escopo `machine`, `elevatesSelf`, URL `https://www.7-zip.org/a/7z2604-x64.exe` e hash publicado `D54BF805F9F3704D1E8DB2FA3498AE7EF2DF0312B40B558E7C71C734430A665D`. Hash não calculado localmente; instalador não foi baixado. GitHub acessível não prova a atualização do CDN WinGet.
- Correção proposta, não executada: `winget source update --name winget --disable-interactivity --verbose-logs`, sem aceite de acordo e sem alterar/remover/recriar a origem. Solicitar aprovação antes de tentar; em caso de falha, preservar o log e reavaliar antes de reparar App Installer.

### Testes e build após a correção de detecção

- Build Core.Tests Release com SDK `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`: sucesso, 0 avisos/erros; **121/121 checks simulados aprovados**. A verificação nova do alias exato está dentro do caso de correspondência existente e passou. Nenhum WinGet ou instalação foi iniciado pelo harness.
- O build completo Release tentou copiar Core/Windows para `Optimizer.App\bin` enquanto o próprio app PID `25128` carregava essas DLLs; falhou após retries (20 avisos, 4 erros). Nenhum processo foi encerrado. Conforme a instrução anterior do usuário, não encerrei a janela para forçar o build. O build normal e a validação visual do código novo ficam pendentes até o usuário fechar a janela.

### Procedimento candidato e estado do plano

7-Zip não é um teste de instalação limpa neste PC: a versão atual `26.04` já aparece no Registro. O único candidato do catálogo não identificado localmente é VLC `VideoLAN.VLC` `3.0.24`; manifesto atual lista x64 EXE/MSI, ambos machine/elevate-self, com hashes publicados. O teste dependerá primeiro da origem WinGet voltar a servir metadata atual. Se aprovado em etapa posterior: mostrar/revisar os acordos atuais, instalar só VLC interativamente, conferir código/versão/publicador/executável e inventário; uma remoção seria feita separadamente em Configurações > Aplicativos. O app não oferece desinstalação nem rollback, e configurações/associações do usuário podem permanecer. Nenhuma instalação foi aprovada ou feita.

Drivers assistidos continuam **parciais**: as APIs de staging/associação e recuperação não foram validadas por execução real. Esta etapa valida visualmente apenas a página Programas do build anterior; após fechamento e build, ainda é necessário validar visualmente a correção de reconhecimento. Nenhum pacote novo foi gerado.

## 33. Conclusão do ciclo WinGet/Programas — 2026-10-20

Fechamento da continuação do commit `7994bf627852e3300e0165d4cbe110eeed70238e`. A janela bloqueadora foi fechada pelo usuário; não encerrei processo algum. Não gerei pacotes, não atualizei fontes WinGet, não aceitei acordos e não instalei programas.

- **Build local:** `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe` reportou SDK `10.0.402`, Windows x64, runtime `10.0.13`. `build .\CalixtoOptimizer.sln -c Release --no-restore` concluiu com êxito, 0 avisos/0 erros.
- **Harness simulado:** 121/121 checks passaram, incluindo o novo nome exato observado `7-Zip 26.04 (x64)`. Isto valida o matcher sob teste, não instalação, WinGet, ou comportamento em outro PC.
- **Inspeção visual do build atual:** iniciei a DLL `src\Optimizer.App\bin\Release\net10.0-windows\Optimizer.App.dll` pelo SDK explícito; caminho, command line e PID da janela foram conferidos antes de usar o helper visual. O alias de executável mostrado pelo helper apontava para uma cópia byte-idêntica do `dotnet.exe` (SHA-256 comparado) e o owner PID do HWND correspondia ao processo validado.
- A página leu 89 entradas locais, reconheceu 7-Zip como versão `26.04` com correspondência exata do DisplayName/Publisher, e manteve VLC como “não identificado localmente”. Foi possível selecionar ambos. A revisão exibiu origem, intenção, escopo, acordos não aceitos, elevação/permissões, reinício possível e limites do manifesto.
- O botão de verificação devolveu `BLOQUEADA` para os dois IDs e informou que a política padrão impediu iniciar `winget.exe`; uma consulta de processos confirmou que não havia WinGet ativo. Não foi iniciada instalação.
- **Consultas reais WinGet/GitHub/Registro** permanecem distintas da inspeção visual e dos testes simulados: a origem configurada e o HTTP 404 inicial, a metadata em cache `26.03`, o manifest público `7zip.7zip` `26.04`, e o registro local de 7-Zip `26.04` estão descritos acima. Naquele estado, nenhuma atualização da origem tinha sido executada; o resultado da tentativa aprovada depois está na seção 34.
- **Próximo teste real candidato:** VLC `VideoLAN.VLC` `3.0.24`, x64, machine/elevates-self segundo o manifest público, ainda sem correspondência local. Isto não comprova que esteja ausente nem que WinGet possa servir o pacote. Primeiro é necessária aprovação para atualizar a metadata da origem existente e revalidar versão, instalador, hash e acordos atuais. A instalação de VLC exige aprovação específica separada e só prossegue se essas pré-condições passarem. A remoção seria posterior, manual e separadamente aprovada; o app não implementa rollback e resíduos de preferências/associações podem permanecer.
- A interface continua exibindo `v0.6.0 · Experimental` embora o código validado seja o do HEAD `7994bf6`; essa etiqueta não foi ajustada neste ciclo. Drivers assistidos continuam **parciais**, sem validação real de staging/associação/recuperação.

## 34. Tentativa autorizada de atualização do cache WinGet — 2026-10-20

O usuário autorizou somente `winget source update --name winget --disable-interactivity --verbose-logs`. Nenhum pacote de software foi instalado e não houve remoção/recriação de origem, aceite de acordo, reset ou reparo do App Installer.

- O refresh anterior tinha HTTP 404 (`0x80190194`) ao obter arquivo. Nesta tentativa, o log mostra que o CDN respondeu para `source2.msix`, indicou índice remoto `2026.1020.1922.22` versus local `2026.1001.228.20`, baixou 3.812.012 bytes e passou a validação de confiança Microsoft.
- A falha ocorreu depois do download, durante `AddPackage`, em `MsixvcStagingSession::GetManifestReader`, `0x87E80034`; houve duas tentativas. O log terminou `Failed to update source: winget`, embora o console tenha mostrado `Cancelled` e exit code 0. Portanto, não considerar atualização concluída.
- Uma leitura estrutural somente leitura com ZIP .NET encontrou 20 entradas e leu o `AppxManifest.xml`: identidade `Microsoft.Winget.Source`, versão `2026.1020.1922.22`, arquitetura neutra, publicador Microsoft. Nos eventos AppXDeploymentServer 402/404/486, o leitor de manifesto falhou com `Unknown error`/`0x87E80034`, a implantação registrou `0x80073CF0` e `0x80070490` (“Could not find package name in path”). Isso localiza a falha no staging/leitura pelo Windows após o download; não prova a causa-raiz nem que o pacote esteja corrompido.
- `winget source list --name winget` confirmou que configuração e URL padrão permaneceram iguais, com `Updated: 2026-09-30 22:58:55`. O cache não foi atualizado. O MSIX temporário permanece em `%LOCALAPPDATA%\Temp\WinGet\Microsoft.Winget.Source_8wekyb3d8bbwe.msix`, tamanho 3.812.012, hash SHA-256 `09C358A284C6DAE61D2C5E3A9C240A4C73C7627EC95643BE0A5D37EECD1D83D`; não removido.
- O erro HTTP 404 não se reproduziu para o MSIX nesta tentativa; o bloqueio observado foi a falha de staging do pacote de origem. A metadata atual do pacote 7-Zip/VLC no cache WinGet permanece não confirmada; a existência do manifesto público no GitHub é independente.
- A documentação oficial do WinGet registra opções de recuperação que removem a origem atual ou instalam manualmente `source2.msix`; ambas excedem a autorização atual e não foram executadas. Não foi encontrada uma explicação oficial específica para `0x87E80034` que prove a causa-raiz deste caso. Qualquer correção deve ser apresentada antes de outra alteração. O candidato VLC permanece sem aprovação de instalação e só pode ser reconsiderado após a origem atualizar e a metadata/termos serem revalidados.
