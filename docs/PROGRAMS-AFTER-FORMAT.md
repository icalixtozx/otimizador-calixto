# Programas pós-formatação — primeira fatia

## Estado desta entrega — 2026-10-20

O catálogo, seleção múltipla, revisão por pacote, detecção local conservadora e o serviço sequencial WinGet estão implementados. A execução real permanece bloqueada pela composição padrão do aplicativo. Nenhuma instalação de programa, atualização, desinstalação, aceitação de acordo, instalação automática do WinGet ou alteração de configuração foi executada nesta etapa.

## Catálogo inicial e evidência de origem

O catálogo contém somente IDs exatos do repositório público de manifests `microsoft/winget-pkgs`, disponibilizados pela origem configurada `winget`. O repositório é mantido no GitHub pela Microsoft, mas é um catálogo comunitário: a presença do manifesto não significa que a Microsoft seja a fabricante ou que tenha auditado o software. A página oficial da Microsoft documenta o nome da origem WinGet padrão e o comportamento de `winget source list`.

| ID exato | Nome/publicador | Manifesto conferido em 2026-10-20 | Licença no manifesto | Escopo/elevação declarados no manifesto revisado |
| --- | --- | --- | --- | --- |
| `7zip.7zip` | 7-Zip / Igor Pavlov | `26.04` — [manifesto](https://github.com/microsoft/winget-pkgs/tree/master/manifests/7/7zip/7zip/26.04) | LGPL-2.1 — [licença do projeto](https://7-zip.org/license.txt) | `machine`, `elevatesSelf`; sem `--scope` forçado pelo app. |
| `VideoLAN.VLC` | VLC media player / VideoLAN | `3.0.24` — [manifesto](https://github.com/microsoft/winget-pkgs/tree/master/manifests/v/VideoLAN/VLC/3.0.24) | GPL-2.0 — [licença do projeto](https://code.videolan.org/videolan/vlc/-/blob/HEAD/COPYING) | Instalador x64 revisado declara `machine` e `elevatesSelf`; sem `--scope` forçado pelo app. |

Os manifests x64 revisados registram hashes para os seus instaladores: 7-Zip `D54BF805F9F3704D1E8DB2FA3498AE7EF2DF0312B40B558E7C71C734430A665D`; VLC `D711E1E1FE52052748C39080C7DCE63F6B7E4C315EFEDF1774A3F1957B782FF3`. Esses são valores publicados no manifesto, não hashes calculados de downloads locais: nenhum instalador foi baixado nesta etapa. A integridade da instalação futura depende dos checks de segurança do WinGet; o código não inclui uma opção para ignorar divergência de hash.

Versões são snapshots datados para auditoria do catálogo, não promessa de serem as versões atuais. A operação futura terá de resolver novamente o metadata pelo ID exato e interromper se a origem não puder confirmar o pacote/instalador aplicável. O resultado de um comando `show` sem sucesso não deve ser substituído por nome parecido.

## Detecção de instalados

A ação de leitura consulta as entradas locais de desinstalação em HKLM/HKCU e visões de 32/64 bits, somente leitura. O comparador exige nome de exibição e publicador exatos da lista curta. Uma única correspondência é descrita como “instalação identificada”; não prova origem WinGet, integridade ou funcionamento. Nenhuma correspondência é “não identificado localmente”, nunca “não instalado”. Correspondências múltiplas ou falhas parciais de leitura são ambíguas/incompletas. O programa pode existir sem uma entrada que o catálogo reconheça.

Essa consulta local não exige rede nem inicia `winget.exe`. O escopo e a versão vistos no registro são somente metadados da entrada registrada.

## Revisão e limites de execução

Para cada seleção, a interface mostra ID/nome, origem, ação, data/versão do manifesto usado como evidência, escopo declarado, licença do pacote, necessidade possível de UAC, chance de reinício e cautela sobre metadata desatualizado. O acordo da origem e o acordo/EULA do pacote são conceitos separados na documentação WinGet. O texto atual do acordo da origem não foi obtido na prévia local; portanto a interface marca ambos como não revisados/aceitos. Uma autorização futura precisa apresentar os textos correntes e obter decisão separada por pacote.

O serviço de execução recebe uma solicitação por pacote e roda uma de cada vez. O `ProgramInstallCommandFactory` aceita somente IDs do catálogo e produz uma lista de argumentos fixa para `winget install --id <ID> --exact --source winget --interactive`. O escopo é deixado para o manifesto/instalador. Não há campo para caminho, script, comando, argumento livre ou origem informada pelo usuário. A aceitação dos acordos só é gerada quando consequências, acordo da origem e acordo do pacote foram todos registrados para aquele ID. Não são usados `--force`, `--silent`, `--override`, `--ignore-security-hash`, `--scope` ou `--allow-reboot`.

A composição do app injeta `BlockedProgramInstallExecutionPolicy`; até um resultado de bloqueio por seleção retorna sem iniciar `winget.exe`. O runner Windows não foi executado. Em uma execução futura autorizada, UAC/elevação dependerão do pacote e da política do sistema. Um pacote MSI/EXE pode deixar alterações parciais; WinGet não oferece rollback genérico garantido. Se uma execução for cancelada depois de iniciar, o estado do pacote atual é “resultado desconhecido/parcial possível”, os seguintes não iniciam, e o app não tenta desinstalar automaticamente. Um código WinGet que exige reinício pausa a sequência; o aplicativo não inicia reinício. A instalação reportada como sucesso não equivale a verificação posterior do produto.

## Resultados por pacote

Estados distintos incluem WinGet indisponível, ID não encontrado, falha de conectividade reportada pelo WinGet, falha genérica, cancelamento antes de iniciar, cancelamento com estado parcial desconhecido, sucesso reportado mas não verificado, reinício pendente/iniciado e itens não executados. Códigos conhecidos são classificados por HRESULT documentado pelo projeto WinGet, sem interpretar mensagens localizadas. `APPINSTALLER_CLI_ERROR_INSTALL_NO_NETWORK` descreve o requisito de conectividade da operação; o aplicativo não o generaliza para afirmar que a internet inteira caiu.

## Verificações realizadas

- SDK .NET explícito: `%LOCALAPPDATA%\dotnet-sdk-10.0.402\dotnet.exe`, versão `10.0.402`.
- O catálogo foi consultado diretamente na API/repositório público `microsoft/winget-pkgs`; IDs, versões snapshot, licenças, escopo, elevação, mínimo de Windows e SHA-256 publicado foram lidos dos manifests/locale manifests exatos. Os hashes não foram recalculados localmente porque não houve download.
- O WinGet local havia reportado versão `v1.29.380` e a origem configurada `winget`; uma consulta local de metadata `show` falhou ao atualizar o índice com HTTP 404 (`0x80190194`). Isso confirma que o catálogo online não pôde ser validado por WinGet neste PC nessa tentativa. Não instalei/reparei WinGet nem alterei a origem.
- O harness Core cobre catálogo restrito, correspondência e incerteza da detecção, comando fixo, pacote arbitrário recusado, política de produção bloqueada sem chamada ao runner, consentimento por pacote, gerenciador indisponível, ID não encontrado, código de conectividade, falha parcial, cancelamento e reinício pendente. São verificações simuladas; nenhum instalador foi executado.
- A leitura do Registro e a nova tela WPF ainda precisam de validação visual. A divergência da ferramenta visual continua bloqueada e o checklist manual não foi substituído.

## Fontes técnicas

- Microsoft Learn: [`winget install`](https://learn.microsoft.com/en-us/windows/package-manager/winget/install), incluindo ID exato, origem, modo interativo e acordos de pacote.
- Microsoft Learn: [`winget list`](https://learn.microsoft.com/en-us/windows/package-manager/winget/list), incluindo listagem de aplicações WinGet e outras instalações.
- Microsoft Learn: [`winget source`](https://learn.microsoft.com/en-us/windows/package-manager/winget/source), nomes/fontes e aceitação separada do acordo da origem.
- Microsoft Learn: [usar WinGet e disponibilidade do App Installer](https://learn.microsoft.com/en-us/windows/package-manager/winget/).
- Projeto oficial: [HRESULTs do WinGet](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md).
