# Progresso

Última atualização: 2026-10-19
Base preservada: c150a51430e603f4bc2ca9f0536383c337efab37.
Versão do aplicativo: 0.5.0-experimental. Não validada em outro computador.

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
