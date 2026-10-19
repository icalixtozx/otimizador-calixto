# Progresso

Última atualização: 2026-10-19
Base preservada: `c231eab1d73391e969c5c7a5606c4195c316b1b5` (`feat: integrate simulated planning and history`). Alterações desta continuação ainda não foram commitadas nem enviadas.

## Resultado desta continuação

- O inventário permanece somente leitura. App continua sem ação automática de energia e sem elevação.
- Implementado um adaptador técnico real para `VIDEOIDLE` por `PowrProf.dll`: leitura do esquema ativo, leitura do índice AC, escrita AC, ativação do esquema e nova leitura. Os GUIDs são constantes documentadas; não interpretamos texto localizado de `powercfg`.
- A UI apresenta demo sintética em seção distinta de prévia real. Prévia e captura real só são iniciadas por botões explícitos; a inicialização do app não consulta VIDEOIDLE.
- A captura real persiste em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealVideoIdle`, separada do histórico sintético. Guarda somente o GUID do plano, valor AC anterior, alvo, estado, timestamp e fingerprint. Não inclui DC e não é backup do Windows.
- O serviço Core implementa protocolo gated para aplicar 900 s, ler/confirmar, detectar conflito, guardar estado antes da escrita e reverter para o valor anterior somente se esquema/valor não mudaram. Estado ambíguo fica não verificado; sem rollback automático.
- A política injetada pelo app é `BlockedVideoIdleMutationPolicy`, que sempre nega. A UI não tem comando de aplicação ou reversão. Testes usam policy habilitada apenas com fake in-memory.
- Processo, Registro, políticas e rede têm candidatos e lacunas documentados em `docs/INITIAL-CATALOG.md`; permanecem indisponíveis.
- Fonte do Inno Setup recebeu procedimento de instalação e verificação do compilador. `ISCC.exe` não existe no PATH nem nos caminhos comuns; ação do usuário será necessária para disponibilizá-lo.

## Arquivos alterados nesta etapa

- Core VIDEOIDLE: `src/Optimizer.Core/Adjustments/VideoIdleOperationModels.cs`, `JsonVideoIdleOperationStore.cs`, `VideoIdleOperationService.cs`.
- API Windows: `src/Optimizer.Windows/WindowsVideoIdlePowerAdapter.cs`.
- Interface: `src/Optimizer.App/ViewModels/MainWindowViewModel.cs`, `ProfilesPageViewModel.cs`, `src/Optimizer.App/Views/ProfilesPage.xaml`.
- Robustez: `tests/Optimizer.Core.Tests/Program.cs`.
- Documentação: `README.md`, `docs/ADJUSTMENTS.md`, `docs/INITIAL-CATALOG.md`, `docs/ARCHITECTURE.md`, `docs/RECOVERY.md`, `docs/PROGRESS.md`, `packaging/README-INSTALADOR.md`, `packaging/README-PACOTE.txt`.
- Distribuição: `src/Optimizer.App/Optimizer.App.csproj`, `src/Optimizer.App/MainWindow.xaml`, `packaging/CalixtoOptimizer.iss` (versão `0.4.0-experimental`).
- Artefatos ficam sob `artifacts/`, ignorados pelo Git.

## Build e testes executados

- Após modelos/serviço Core: `dotnet build .\CalixtoOptimizer.sln --configuration Release` — sucesso, 0 avisos, 0 erros.
- Após adaptador PowrProf: mesmo build — sucesso, 0 avisos, 0 erros.
- Após integração inicial e robustez: build Release — sucesso, 0 avisos, 0 erros; harness 34/34.
- Após guards de corrida, proteção de estado AC/DC e falha no journal: build Release — sucesso, 0 avisos, 0 erros; harness **37/37**.
- Testes incluem: captura persistida e bloqueio default; leitura-preview apenas com fake; apply/verify fake; reversão exata fake; conflito anterior e tardio; mudança do plano não reativa o plano antigo; falha ao persistir intenção evita escrita; interrupção após escrita falsa produz estado não verificado; falha na captura e fingerprint adulterado são recusados.
- Não foi chamado `PowerGetActiveScheme`, `PowerReadACValueIndex`, `PowerWriteACValueIndex` nem `PowerSetActiveScheme` nesta máquina. A UI de prévia/captura real não foi clicada. Portanto, o adaptador nativo está compilado, mas sua operação real ainda não foi verificada.
- Build do script Inno Setup: não executado, pois o compilador está ausente. Instalação/desinstalação também não testadas.

## Pacote portátil

- `artifacts/CalixtoOptimizer-0.4.0-experimental-win-x64.zip`
- Tamanho: 65.433.198 bytes; 406 entradas.
- SHA-256: `441DCACBABC2772874852003CE8679FB6B1B085F4E97714EB54F88CE3F49CB89`.
- Publicação self-contained em `artifacts/release-v0.4.0-experimental/CalixtoOptimizer`.
- ZIP verificado para conter `Optimizer.App.exe`, `Optimizer.App.runtimeconfig.json` e `README-PACOTE.txt`. Build/publish e checagem de conteúdo não são teste de instalação nem compatibilidade em outro PC.
- Setup EXE **não produzido**. Script `.iss` ainda não compilado.

## Catálogo, suporte e limites

- Energia: VIDEOIDLE é teste técnico, não otimização de desempenho. O valor AC e alvo exatos estão em `docs/ADJUSTMENTS.md`; DC permanece fora da interface de escrita.
- Processos: sem candidato universal implementado; encerramento forçado indisponível.
- Registro: nenhum caminho de tweak ou escrita.
- Políticas: nenhuma mudança de GPO/MDM/Registro de política.
- Rede: nenhum `netsh`, ajuste TCP ou mudança de interface.
- Ganhos de FPS/latência não são alegados. Nenhum driver, registro, política, serviço, rede, energia ou firmware foi alterado para este trabalho.
- Fingerprint da captura é verificação de consistência acidental, não assinatura contra alteração maliciosa da pasta local. Não pode autorizar worker privilegiado.

## Pendências e próximo passo

1. Usuário disponibiliza o Inno Setup oficial; conferir assinatura Authenticode/publisher, localizar `ISCC.exe`, compilar `.iss`, conferir o EXE/hash e depois testar instalar/desinstalar em VM/PC Windows limpa.
2. Revisar as limitações do protocolo nativo de energia em equipamento Windows de teste controlado; ainda falta observar API/permissões, Modern Standby/overlays/OEM e conflitos. Obter aprovação específica antes de ligar qualquer interface de escrita; não executar no computador de desenvolvimento.
3. Melhorar evento de operação real/auditoria autenticada antes de confiar em recuperação entre fronteiras de privilégio; o JSON local não tem autenticidade criptográfica.
4. Implementar um módulo de diagnóstico somente leitura para energia (enumerar planos/valores) ou escolher, com fonte e teste reproduzível, um candidato de rede/processo. Não adicionar writes enquanto não houver item específico, mecanismo documentado e reversão validável.
5. Testar instalação, UI e adaptador em outro PC quando houver disponibilidade. A versão continua experimental e não validada.
