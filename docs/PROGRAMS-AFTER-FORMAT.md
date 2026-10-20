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
- O WinGet local reportou `v1.29.380`. `winget source list --name winget` mostra a configuração padrão `Microsoft.PreIndexed.Package`, confiável, `https://cdn.winget.microsoft.com/cache`, atualização registrada em `2026-09-30 22:58:55`; `winget source export winget` corresponde à mesma origem. Portanto, o nome/URL da origem não está ausente nem foi trocado.
- Em logs WinGet de `2026-10-20 16:57` e `16:58`, a atualização falhou em `GetUpstreamFile` para essa origem com `0x80190194` (HTTP 404). Em outra tentativa às `17:19`, o log registrou falha distinta `0x87E80034` em `MsixvcStagingSession::GetManifestReader`. `winget show --id 7zip.7zip --exact --source winget --disable-interactivity` ainda retornou código de saída zero e mostrou `26.03`, mas também emitiu “Failed in attempting to update the source”. Como o índice local foi atualizado em setembro e o manifesto público atual é `26.04`, esse resultado é metadata em cache, não confirmação do estado atual do CDN.
- A API pública do GitHub respondeu; o diretório atual do manifesto `7zip.7zip/26.04` existe. Isso prova conectividade com GitHub e presença do manifesto comunitário, mas não prova que o CDN WinGet está atualizado ou servindo o pacote neste momento. Não removi/recriei fontes, aceitei acordos, instalei/reparei componentes nem alterei configurações.
- A leitura local de Registro desta sessão consultou 89 entradas sem avisos. Foi encontrada a entrada `7-Zip 26.04 (x64)`, `Igor Pavlov`, versão `26.04`, em HKLM. O VLC não teve correspondência em HKLM/HKCU 32/64; “não identificado” não equivale a prova de ausência.
- O harness Core cobre catálogo restrito, correspondência e incerteza da detecção, comando fixo, pacote arbitrário recusado, política de produção bloqueada sem chamada ao runner, consentimento por pacote, gerenciador indisponível, ID não encontrado, código de conectividade, falha parcial, cancelamento e reinício pendente. São verificações simuladas; nenhum instalador foi executado.
- A página WPF foi aberta a partir da DLL Release deste repositório; PID, comando e caminho foram conferidos. Visualmente foram testados navegação, seleção de 2 pacotes, leitura local, revisão, consequências e resultados individuais bloqueados. O botão **Verificar bloqueio por pacote** mostrou `BLOQUEADA` para ambos. `winget.exe` não estava em execução antes nem depois, e o resultado de bloqueio não iniciou o runner.
- Na primeira observação do build anterior (commit `7994bf6`), a UI indicou 7-Zip como “Não identificado localmente” apesar da entrada real `7-Zip 26.04 (x64)`. Foi adicionada essa variante exata à lista reconhecida e uma asserção ao harness. Após o usuário fechar a janela que bloqueava os arquivos, o build WPF completo Release e a validação visual da correção foram concluídos; nenhum processo foi encerrado automaticamente.
- A execução da DLL Release recompilada mostrou **Instalação identificada · versão 26.04** para o 7-Zip; a explicação mantém o limite de que a entrada exata DisplayName/Publisher não comprova origem WinGet ou integridade. VLC ficou como “Não identificado localmente”, sem concluir ausência. A leitura local confirmou 89 entradas.
- Visualmente, dois pacotes puderam ser selecionados ao mesmo tempo; a revisão mostrou origem, ação pretendida, escopo, licença/acordos ainda não aceitos, permissões/elevação, possibilidade de reinício e aviso de que o manifesto datado não garante metadata atual. Ao clicar em **Verificar bloqueio por pacote**, ambos retornaram `BLOQUEADA` e a interface informou que a política padrão impediu qualquer chamada ao `winget.exe`. Não havia `winget.exe` ativo depois do resultado; nenhum instalador foi executado.
- O build completo da solução Release com SDK explícito `10.0.402` terminou com 0 avisos e 0 erros; o harness atualizado passou **121/121 checks simulados**. A tela ainda exibe a etiqueta `v0.6.0 · Experimental`; ela não foi alterada nesta etapa. Nenhum pacote de distribuição foi gerado.

## Revalidação de origem e procedimento candidato — 2026-10-20

### Manifesto público atual do 7-Zip

Consulta direta somente leitura à API do repositório `microsoft/winget-pkgs`: ID `7zip.7zip`, versão `26.04`, atual no branch `master` observado em `2026-10-20`. O instalador x64 declarado no manifesto é EXE, URL `https://www.7-zip.org/a/7z2604-x64.exe`, SHA-256 publicado `D54BF805F9F3704D1E8DB2FA3498AE7EF2DF0312B40B558E7C71C734430A665D`, data `2026-10-06`, `Scope: machine` e `ElevationRequirement: elevatesSelf`. O hash foi lido do manifesto e não recalculado; o arquivo não foi baixado. A máquina é x64 e tem `7-Zip 26.04 (x64)` registrado em HKLM. O registro não prova como foi instalado nem que está funcional. Após a correção e o build Release, a página reconheceu essa entrada pelo nome exato.

O comando WinGet local ainda apresenta metadata em cache `26.03`; não foi possível confirmar a versão que o CDN entregaria hoje. O pacote tem manifesto público, mas a disponibilidade atual na origem WinGet continua **não confirmada**.

### Correção de origem proposta, ainda não executada

A próxima ação diagnóstica proposta é `winget source update --name winget --disable-interactivity --verbose-logs`. Ela tenta atualizar o cache local da origem existente, sem remover/recriar fontes, aceitar acordos nem reinstalar App Installer. Pode falhar novamente, e não deve ser seguida por `source reset`, remoção/recriação ou aceitação automática de acordos. Se falhar, revisar o novo log antes de propor reparo de componente. A ação não foi executada nesta etapa; aguarda aprovação específica.

### Menor teste real útil para aprovação posterior

7-Zip não é candidato a instalação de teste neste PC: a versão atual `26.04` já aparece em HKLM, portanto instalar de novo não valida uma instalação limpa. O candidato disponível no catálogo e sem correspondência local é VLC `VideoLAN.VLC`, versão atual do manifesto comunitário `3.0.24` (2026-09-21). O manifesto x64 oferece EXE e MSI, ambos `machine` e `elevatesSelf`; o WinGet pode escolher o instalador aplicável. O hash publicado para EXE é `D711E1E1FE52052748C39080C7DCE63F6B7E4C315EFEDF1774A3F1957B782FF3`; para MSI, `22172095CE4C2D5F5B600BB4C142D78105DB29A77FA59A931941876F3B20F6D0`. Esses hashes não foram recalculados de arquivos baixados.

**Pré-condições:** primeiro concluir o build/inspeção da correção de reconhecimento e, com aprovação separada, tentar a atualização da origem; não instalar se o WinGet não confirmar metadata atual, URL, hash e arquitetura. Antes do teste, revisar o acordo vigente da origem e o EULA do pacote sem autoaceitação, confirmar novamente que VLC não está instalado e mostrar qual instalador/scope será usado. Instalação em escopo `machine` altera arquivos e registros de produto para todos os usuários e pode abrir UAC; o instalador interativo pode oferecer associações/opções próprias, que devem ser revistas antes de avançar. Nenhum reinício será iniciado automaticamente.

**Verificação:** registrar código/resultado WinGet por pacote, depois consultar a entrada de desinstalação com nome/publicador/versão, confirmar o executável e a versão instalada e comparar novamente os inventários. “Sucesso” do WinGet sozinho não prova funcionamento. Não instalar outro pacote na mesma rodada.

**Limite de desinstalação:** o Calixto Optimizer ainda não implementa desinstalação nem rollback. Se aprovado separadamente, a remoção seria feita pelo item VLC de Configurações > Aplicativos > Aplicativos instalados, conferindo que o publicador/nome corresponde ao teste. A desinstalação pode deixar preferências, cache, associações escolhidas pelo usuário ou arquivos criados durante o uso; esses resíduos teriam de ser verificados manualmente. Instalar VLC não está aprovado nem foi executado.

## Fontes técnicas

- Microsoft Learn: [`winget install`](https://learn.microsoft.com/en-us/windows/package-manager/winget/install), incluindo ID exato, origem, modo interativo e acordos de pacote.
- Microsoft Learn: [`winget list`](https://learn.microsoft.com/en-us/windows/package-manager/winget/list), incluindo listagem de aplicações WinGet e outras instalações.
- Microsoft Learn: [`winget source`](https://learn.microsoft.com/en-us/windows/package-manager/winget/source), nomes/fontes e aceitação separada do acordo da origem.
- Microsoft Learn: [usar WinGet e disponibilidade do App Installer](https://learn.microsoft.com/en-us/windows/package-manager/winget/).
- Projeto oficial: [HRESULTs do WinGet](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md).
