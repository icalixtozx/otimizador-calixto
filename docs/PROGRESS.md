# Progresso

Última atualização: 2026-10-19

## Estado real

- Repositório preservado em Downloads, branch `main`; alterações desta rodada permanecem locais até revisão final e commit.
- Solução: Optimizer.Core, Optimizer.Windows, Optimizer.App (WPF) e harness executável Optimizer.Core.Tests.
- SDK usado: .NET 10.0.402 portátil já existente na sessão de trabalho; não instalado no Windows. O `global.json` pede 10.0.100 com roll-forward de feature.
- Validação final local: `dotnet build CalixtoOptimizer.sln -c Release` passou com 0 avisos e 0 erros; o harness passou 26/26; `git diff --check` passou.
- O app continua iniciando em Visão geral/diagnóstico. A coleta de hardware e drivers é somente leitura.
- Não há outro computador disponível; suporte cruzado continua pendente. Nenhum dado pessoal ou detalhe de hardware da sessão é registrado neste documento.

## Arquivos principais desta continuação

- Motor, consentimento e robustez: `src/Optimizer.Core/Adjustments/`, `src/Optimizer.Core/Consent/`, `src/Optimizer.Core/Profiles/` e `src/Optimizer.Core/Recovery/`.
- UI: `src/Optimizer.App/ViewModels/MainWindowViewModel.cs`, `src/Optimizer.App/ViewModels/ProfilesPageViewModel.cs`, `src/Optimizer.App/Views/ProfilesPage.xaml`, `src/Optimizer.App/Views/ProfilesPage.xaml.cs`, `src/Optimizer.App/MainWindow.xaml`.
- Testes simulados: `tests/Optimizer.Core.Tests/Program.cs`.
- Proposta e distribuição: `docs/ADJUSTMENTS.md`, `packaging/CalixtoOptimizer.iss`, `packaging/README-INSTALADOR.md`, `packaging/README-PACOTE.txt`.
- Estado/uso/recuperação: `README.md`, `docs/ARCHITECTURE.md`, `docs/RECOVERY.md`, `docs/PROGRESS.md`, `.gitignore`.

## Fase 1 — inventário somente leitura

- [x] Modelos de computador e drivers e adaptadores WMI de leitura.
- [x] Busca, filtro de assinatura e CSV sanitizado.
- [x] Estados de carregamento, falha, vazio e dados parciais na interface.
- [x] Saída real do inventário observada em validação anterior; detalhes específicos foram omitidos.
- [ ] Confirmar independentemente a divergência de metadados do Windows observada antes.
- [ ] Validar em outro computador.

## Fase 2 — catálogo, compatibilidade, plano e simulação

- [x] Metadados tipados, validação de IDs, consequência, risco e arquitetura/build.
- [x] Compatibilidade distingue simulável, bloqueado, incompatível, desconhecido e ID ausente.
- [x] SHA-256 do plano vincula plataforma e metadados apresentados.
- [x] Adaptador simulado em memória não chama APIs do Windows e informa `WindowsChanged=false`.
- [x] Falhas e cancelamentos não são sucesso; etapas seguintes ficam não executadas.
- [x] UI informa que o catálogo de produto está vazio; a fixture sintética está separada e explicitamente identificada.
- **Limite:** não existe ajuste de produto habilitado nem executor real.

## Fase 3 — perfis e interface de planos

- [x] Leve, Equilibrado, Extremo e Personalizado compõem propostas sujeitas a revisão.
- [x] Leve sugere somente risco baixo; Equilibrado até moderado.
- [x] Extremo e Personalizado usam somente escolha explícita; Extremo não seleciona tudo automaticamente.
- [x] UI permite escolher perfil, incluir opcionalmente fixture sintética, gerar e revisar o plano.
- [x] Aplicação real indisponível em todos os perfis.
- **Limite:** catálogo de produto sem ajustes; a plataforma x64 exibida na demonstração é sintética e não comprova compatibilidade com o computador.

## Fase 4 — consequência e consentimento

- [x] UI mostra consequência e exige checkbox por item antes de simular.
- [x] Recibo liga decisão ao ID, versão, fingerprint e consequência.
- [x] Plano antigo, recibo adulterado ou recusa não valida.
- [x] Consentimento autoriza somente a demonstração em memória; não pode autorizar escrita no Windows.

## Fase 5 — diário, histórico e retomada

- [x] JSON por operação com escrita temporária, flush e substituição atômica.
- [x] Interface consulta o histórico em `%LOCALAPPDATA%\CalixtoOptimizer\History` e mostra origem/estado.
- [x] Origem sintética é marcada; a demonstração não cria snapshots.
- [x] A UI informa que snapshots não são backup restaurável do Windows.
- [x] Avaliação distingue estado ainda preparatório, conflito de fingerprint, término e revisão manual.
- [x] Não existe reaplicação, restauração ou rollback automático.
- **Limite:** sem provedor de snapshot real e sem executor real; registros demonstrativos só são gravados após ação explícita.

## Fase 6 — testes simulados de robustez

- [x] Sucesso, falha, cancelamento, etapas não executadas e ausência de mutação no simulador.
- [x] Diário corrompido rejeitado.
- [x] Falha de gravação e erro de disco cheio injetados; entrada previamente confirmada preservada.
- [x] Interrupção de gravação injetada; entrada previamente confirmada preservada.
- [x] Conflito de plano, consentimento duplicado e avaliação conservadora de retomada.
- [x] Demonstração sintética não pode criar snapshots; o avaliador informa que nenhum backup do Windows foi feito.
- Resultado atual: **26/26** verificações do harness, sem chamadas de configuração do Windows.
- **Limite:** falha física de disco e encerramento abrupto real não foram provocados; são representados por injeção.

## Fase 7 — candidato a ajuste real

- [x] Uma proposta documentada em `docs/ADJUSTMENTS.md`: tela desligada após 900 segundos quando ligado à tomada.
- [x] Fonte Microsoft, compatibilidade, consequência, estado a capturar, aplicação, verificação, reversão e conflitos documentados.
- [ ] Implementar adaptador real: bloqueado até aprovação específica e validação por ambiente.
- [ ] Executar alteração neste computador: não autorizado pela solicitação atual.

## Fases 8–10 — outros módulos

- [ ] Energia, Registro, políticas, processos e rede: indisponíveis; sem operações implementadas.
- [ ] Instalação assistida de drivers: indisponível; permanece separada do inventário.
- [ ] Integração de GPU: indisponível; nenhum suporte comprovado foi incluído.
- Não há escrita em firmware, BIOS, tensão ou frequência; proteções não são desativadas.

## Fases 11–12 — distribuição

- [x] Publicação autocontida Windows x64 v0.3.0 experimental gerada em `artifacts/release-v0.3.0-experimental/CalixtoOptimizer`.
- [x] ZIP `artifacts/CalixtoOptimizer-0.3.0-experimental-win-x64.zip`, 65,414,901 bytes, SHA-256 `758ADEF8FE855A28153FC00D7A3FEE81F0B7089635A07B13FF2BE4C3D0FA11A3`; contém 406 entradas, executável, runtime e LEIA-ME.
- [x] Executável publicado abriu localmente com título “Calixto Optimizer” e encerrou com código 0 após o smoke check; isso não é teste de instalação.
- [x] Fonte Inno Setup por usuário em `packaging/CalixtoOptimizer.iss`; usa `PrivilegesRequired=lowest`, sem worker privilegiado.
- [x] Licença, disponibilidade e comando `ISCC.exe <arquivo.iss>` documentados em `packaging/README-INSTALADOR.md` com referências oficiais.
- [ ] ISCC não está instalado no ambiente; EXE do instalador ainda não foi produzido.
- [ ] Sintaxe Inno Setup ainda não passou pelo compilador; instalação/desinstalação não foram testadas.
- [ ] Teste do instalador e pacote em outro computador.
- **Segurança futura:** o caminho de instalação é gravável pelo usuário; nenhum worker elevado pode ser colocado ali. Um worker futuro exige desenho protegido e validação próprios.

## Segurança e estado honesto

- Nenhuma otimização, driver, Registro, política, serviço, energia, rede ou firmware foi alterado para este trabalho.
- O app permanece em modo de diagnóstico por padrão; consentimento da demonstração não habilita ações reais.
- A versão de distribuição será experimental e não validada em outro computador.
- Não chamar o app de completo enquanto os módulos pendentes acima permanecerem indisponíveis.
