# Ajustes de energia implementados

## Modo de energia configurado pelo usuário (Windows 11)

Este é o primeiro recurso de desempenho do catálogo. Não é um plano de energia e não é apresentado como ajuste de FPS: usa as APIs de modo configurado do usuário em `PowrProf.dll`. A documentação da Microsoft define os valores aceitos como Melhor eficiência, Equilibrado e Melhor desempenho; descreve o valor como uma preferência/sinal do usuário que pode ser sobreposto por outros sinais do sistema. O valor configurado, portanto, não comprova o modo efetivo nem um ganho de desempenho.

O suporte documentado começa no Windows 11 desktop. O adaptador verifica a build mínima 22000 e a presença das quatro exportações AC/DC de leitura/escrita antes de habilitar revisão. A tela lê a preferência real somente por clique, em AC e DC separadamente. No PC de desenvolvimento (Windows build 26300), as duas consultas retornaram sucesso e `Equilibrado`; nenhum setter foi chamado. Windows 10, API ausente, GUID desconhecido e falha individual de leitura não permitem alterar o lado correspondente.

- **Objetivo/mecanismo:** definir a preferência configurada pelo usuário para o estado ligado à tomada (AC), na bateria (DC), ou ambos. Não altera o plano ativo e não fixa diretamente clocks, tensão, frequência ou desempenho de hardware.
- **Consequências:** Melhor desempenho favorece desempenho, com possibilidade de maior consumo, temperatura e ruído; em bateria pode reduzir autonomia. Melhor eficiência pode reduzir resposta/desempenho em algumas cargas. Equilibrado é um sinal de equilíbrio. OEM, firmware, políticas, carga e outros sinais podem sobrepor ou alterar o resultado efetivo.
- **Privilégios:** o aplicativo permanece `asInvoker`, sem worker elevado. O código não presume que elevação seja necessária nem suficiente; um erro Win32 é reportado. Escrita real ainda não foi validada neste ou em outro PC.
- **Captura e consentimento:** captura persistida separada em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealPowerMode` armazena somente os lados selecionados, valores anteriores, alvos e fingerprint. As consequências são apresentadas separadamente para AC/DC. O serviço exige consentimento da consequência de cada lado selecionado, frase de confirmação vinculada à operação e política de escrita habilitada. A captura não é backup do Windows.
- **Aplicação/verificação:** o serviço persiste intenção antes de cada setter, compara o estado lido antes de escrever e faz leitura de retorno após cada lado e ao final. Conflito interrompe etapas restantes; falha ou estado ambíguo fica não verificado, nunca é apresentado como sucesso.
- **Restauração:** captura valores anteriores e reverte apenas lados selecionados que ainda correspondam ao valor-alvo ou ao valor capturado. Uma preferência que esteja num terceiro estado causa conflito e recusa a escrita. A leitura posterior confirma somente a preferência configurada, não a efetividade.
- **Limitações de concorrência:** APIs separadas não oferecem transação compare-and-swap; uma alteração externa entre leitura e setter ainda é possível. Falha/interrupção pode deixar uma operação parcialmente concluída e requer revisão manual.
- **Avaliação:** comparar preferências configuradas e, quando houver uma aprovação futura de execução, medir uma carga repetível no mesmo equipamento, observando energia/temperatura/desempenho. Nenhuma melhoria está prometida ou foi medida. Não foram feitas alterações reais; aplicação e restauração só foram testadas com adaptadores falsos.
- **Estado da UI:** leitura e captura real estão disponíveis sob demanda. Revisão de consequência é visível. Aplicação/reversão permanecem bloqueadas por padrão e não há ação de escrita exposta na interface.

Fontes oficiais: [PowerSetUserConfiguredACPowerMode](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfiguredacpowermode), [PowerGetUserConfiguredACPowerMode](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetuserconfiguredacpowermode), [PowerSetUserConfiguredDCPowerMode](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfigureddcpowermode) e [PowerGetUserConfiguredDCPowerMode](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetuserconfigureddcpowermode). `PowerSetActiveScheme` é a API separada para mudar o plano ativo.

## VIDEOIDLE: teste técnico do motor real

## Estado

O primeiro adaptador real está implementado para o valor `VIDEOIDLE` no lado AC do plano ativo, com alvo proposto de **900 segundos**. O app permite, somente após ação explícita, ler o valor real e salvar uma captura técnica separada no histórico `RealVideoIdle`. Não faz essa leitura automaticamente na inicialização.

O app não oferece caminho de escrita: aplicar/reverter estão bloqueados pela política `BlockedVideoIdleMutationPolicy` (nega sempre), e não existe comando/botão habilitado para isso. O serviço Core implementa o fluxo de aplicação, leitura posterior, detecção de conflito e reversão condicional para permitir testes com adaptador falso e futura validação. Nenhum setter VIDEOIDLE foi chamado nesta máquina.

A consulta do GUID do plano e do índice AC usa APIs PowrProf com GUIDs fixos, sem interpretar saída de `powercfg` localizada. A escrita só toca `PowerWriteACValueIndex` no lado AC; o lado DC e outros valores não são enviados ao adaptador. A API documenta que a escrita de um plano ativo precisa de `PowerSetActiveScheme` para entrar em vigor. Fontes e compatibilidade estão em [Microsoft: VIDEOIDLE](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/display-settings-display-idle-timeout), [PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex), [PowerWriteACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwriteacvalueindex) e [PowerSetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme).

## Fluxo implementado

1. **Prévia real:** leitura sob demanda do GUID de plano ativo e índice AC atual, sem escrita.
2. **Captura persistida:** segunda leitura do estado, gravada atomicamente em `LocalAppData\CalixtoOptimizer\History\RealVideoIdle`. O registro guarda esquema, valor anterior, alvo, data, estado e fingerprint dos valores da captura. É uma captura mínima de configuração, não backup do Windows.
3. **Aplicação codificada, bloqueada na aplicação:** requer política habilitada e aprovação ligada ao ID, ação, fingerprint, consequência aceita e frase `APPLY VIDEOIDLE XXXXXXXX`. Compara o estado antes da escrita, persiste a intenção antes de chamar a API, confere novamente antes de escrever, altera somente o índice AC para 900, reativa o mesmo GUID do plano e lê o valor de volta. Divergência/falha fica não verificada, nunca é sucesso.
4. **Conflito:** plano ativo ou valor AC diferentes da captura recusam a escrita. Uma nova leitura após persistir a intenção estreita a janela de corrida. As APIs não oferecem compare-and-swap: uma mudança concorrente entre a última leitura e a chamada nativa ainda é possível; resultado ambíguo exige revisão manual.
5. **Reversão codificada, bloqueada na aplicação:** requer política habilitada e aprovação separada ligada à ação e frase `REVERT VIDEOIDLE XXXXXXXX`. Só prossegue se o plano capturado continuar ativo e o índice atual ainda for 900. Grava exatamente o índice AC capturado, reativa o mesmo plano e confirma em nova leitura. Se o plano/valor mudou, recusa a reversão.
6. **Falha/interrupção:** o diário persiste `MutationMayHaveStarted`/`ReversionMayHaveStarted` antes da escrita. Exceção depois disso vira estado não verificado, com orientação de revisão manual; nunca é marcada como revertida.

## Consentimento e política

Consentimento da demonstração sintética não autoriza esse ajuste. A política usada pelo app para mutações reais é `BlockedVideoIdleMutationPolicy`, que sempre nega. O serviço de aplicação não está exposto na UI. Os testes ativam uma política isolada em memória apenas para exercitar o fluxo no adaptador falso. Não existe aprovação real da usuária/usuário registrada e não ocorreu escrita no Windows.

## Limites

- APIs disponíveis desde Windows Vista desktop; pacote atual é Windows 10+ x64/.NET 10. Compatibilidade de OEM, políticas, Modern Standby, overlays, permissões e efeito real do display ainda não foi testada em outro PC.
- Não inferimos que leitura bem-sucedida prove que o fabricante implementa o comportamento físico esperado.
- O fingerprint detecta divergência acidental do conteúdo capturado; não é assinatura nem proteção contra edição maliciosa do arquivo local.
- Uma captura local pode ser editada/apagada pelo mesmo usuário. Não existe worker elevado e o diário não é fonte confiável para atravessar fronteira de privilégio.
- Uma reversão simulada pelo fake verifica o algoritmo, não comprova reversão no Windows.

## Catálogo restante

Veja [Catálogo inicial de candidatos](INITIAL-CATALOG.md). Processos, Registro, políticas e rede permanecem indisponíveis, sem escrita nem alegação de ganho universal.

## Modo de energia configurado do usuário (0.6.0)

Este é um teste técnico da API do modo de energia, não uma promessa de desempenho ou FPS. A Microsoft documenta `PowerGet/SetUserConfiguredACPowerMode` e `PowerGet/SetUserConfiguredDCPowerMode` para Windows 11 desktop. Os identificadores conhecidos representam Melhor eficiência, Equilibrado e Melhor desempenho. A API lê uma preferência configurada; sinais do sistema podem sobrepô-la. O plano ativo é outro estado, consultado por `PowerGetActiveScheme`.

Fontes oficiais: [PowerGet AC](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetuserconfiguredacpowermode), [PowerSet AC](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfiguredacpowermode), [PowerGet DC](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetuserconfigureddcpowermode), [PowerSet DC](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfigureddcpowermode) e [PowerGetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme).

### Revisão e captura

- `WindowsPowerModeAdapter.ReadConfiguredModes` consulta AC, DC e o GUID do plano ativo. Não consulta `powercfg` nem interpreta saída localizada.
- A compatibilidade para execução exige que os dois lados e o plano ativo sejam legíveis e que os dois valores estejam entre os GUIDs reconhecidos. Se qualquer um for desconhecido ou indisponível, o app não permite captura executável.
- A captura versão 2 registra AC, DC e plano ativo, mesmo quando o alvo escolhido só se aplica a um lado. Assim a execução pode detectar mudanças no lado não selecionado e comprovar que ele permaneceu igual. Capturas versão 1 continuam legíveis como histórico, mas não são executáveis nem restauráveis neste fluxo.
- A captura é uma linha de histórico local em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealPowerMode`. Ela não é backup do Windows e não pode reverter valores fora do escopo.
- A UI exibe consequência separada para AC/tomada e DC/bateria. “Revisei” é apenas revisão visual. Para qualquer escrita futura há controles separados de aceite da consequência, autorização da ação e frase exata vinculada à operação. Reversão exige outro aceite e a frase `REVERT POWER MODE XXXXXXXX`.

### Pré-condição, aplicação e restauração

1. **Bloqueio padrão:** o serviço Core cria `BlockedPowerModeMutationPolicy`, que nega sempre. A tela também inicia sem autorização nem frase; leitura, navegação e captura não chamam setters. Somente o clique explícito de aplicação após os controles acima cria uma política confirmada temporária para aquela operação. Nenhuma aplicação ou reversão real foi autorizada/executada nesta etapa.
2. **Prévia antes da ação:** ao solicitar aplicação, a UI relê AC, DC e plano ativo. Se diferirem da captura, exige nova leitura, captura e revisão; a captura antiga não é reutilizada. O serviço faz outra leitura interna imediatamente antes de armar o diário e repete a leitura antes de cada setter.
3. **Intenção e aplicação futura:** somente os lados selecionados podem ser enviados ao setter correspondente. A intenção é persistida antes da primeira chamada. Após cada setter e no final, novas leituras devem confirmar o alvo selecionado, o lado não selecionado preservado e o plano ativo inalterado. Qualquer divergência fica como conflito ou resultado não verificado, nunca como sucesso.
4. **Restauração futura:** requer confirmação própria. A leitura inicial deve mostrar o mesmo plano ativo e, em cada lado, apenas os valores capturados ou propostos. Antes/depois de cada setter o serviço compara ambos os lados ao estado esperado da etapa. Um terceiro valor, alteração externa ou mudança do plano recusa escrita adicional e pede revisão manual.
5. **Recuperação limitada:** o diário grava intenção antes de escrever e usa leitura posterior. Uma interrupção pode deixar parte da operação aplicada; nesse caso o estado é não verificado, e a restauração condicional depende de nova leitura e de autorização separada. As APIs não oferecem transação ou compare-and-swap: a pequena janela entre última leitura e setter não pode ser eliminada.

### Consequências, privilégios e validação

- Melhor desempenho pode aumentar consumo, calor e ruído; na bateria pode reduzir autonomia. Melhor eficiência pode afetar resposta em algumas cargas. O resultado depende de Windows e OEM; não há benefício numérico alegado.
- A compatibilidade documentada é Windows 11 desktop e exports/API disponíveis. Leitura de estado não precisa de elevação; a aplicação usa o contexto do usuário e pode ser recusada por plataforma/política/OEM. Hardware e permissões de outra máquina continuam pendentes.
- Testes com adaptadores falsos verificam consentimento, comparação de AC/DC/plano, estado conflitante, interrupção, captura corrompida e política bloqueada. São simulações e não comprovam setter, operação nem reversão reais.
- Nesta etapa os getters foram executados localmente e uma captura real foi gravada/reaberta em processos separados; nenhum setter foi chamado. O fluxo visual do painel de modo de energia não foi percorrido até o fim, então a apresentação completa de consequências/autorização/histórico ainda precisa de validação de interface com o app em primeiro plano.

O conteúdo do diário é editável pelo usuário local; o fingerprint só detecta corrupção/divergência acidental, não é assinatura. Nenhum componente elevado pode confiar nesse arquivo como canal de comando.
# Registro e políticas — primeiro candidato documentado

## Candidato: exibir extensões de nome de arquivo

**Categoria:** preferência de interface do Explorador de Arquivos. Não é ajuste de desempenho e não há alegação de ganho de FPS.

- **Finalidade:** mostrar `.txt`, `.exe`, `.pdf` e demais extensões conhecidas nos nomes exibidos pelo Explorador. A documentação de suporte da Microsoft descreve essa preferência no Windows 10 e Windows 11. A extensão ajuda a identificar o tipo do arquivo; mudar o texto da extensão não converte o formato.
- **Mapeamento técnico usado pelo protótipo:** hive `HKEY_CURRENT_USER`; caminho `Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`; valor `HideFileExt`; tipo alvo `REG_DWORD`; `0` significa exibir e `1` significa ocultar. A implementação `WindowsExplorer` do repositório Microsoft `winget-dsc` usa esse caminho/valor e interpreta a ausência do valor como “exibir”. A página de suporte da Microsoft confirma a preferência da interface, mas não publica um contrato Win32 estável para esse valor; o mapeamento é evidência de uma implementação Microsoft mantida, não garantia de API para terceiros.
- **Visão:** `Registry64` em Windows x64, alinhada ao Explorador de Arquivos x64. A implementação recusa outros sistemas/visões; não usa `Wow6432Node` nem redireciona a escrita para a visão de 32 bits.
- **Compatibilidade:** a preferência existe na interface documentada para Windows 10 e 11. O Calixto suporta para revisão somente Windows x64 e não identifica uma edição mínima específica além do Windows cliente. A fonte não lista um build mínimo. O suporte de Windows 10 terminou em 14/10/2025; a existência da preferência não significa que essa versão continue recebendo correções de segurança.
- **Consequências:** nomes de arquivos passam a mostrar extensões. Isso pode alterar a apresentação e reduzir ambiguidade visual; não renomeia arquivos. Não solicita elevação: o escopo é o perfil do usuário atual. ACLs podem negar acesso, caso em que a operação deve parar. Nenhum reinício do Windows é esperado; o motor verifica somente o valor do Registro. A atualização visual do Explorador após escrita direta não foi validada pelo aplicativo; não reinicia `explorer.exe` automaticamente.
- **Captura:** registrar se a chave existe, se o valor existe e, se existir, seu tipo e dado exatos. Ausência não equivale a `REG_DWORD 0` no snapshot, ainda que a implementação Microsoft citada modele a ausência como a preferência “exibir”.
- **Aplicação:** reler imediatamente antes; recusar estado diferente, chave ausente, gerenciamento organizacional não verificável, acesso negado, consentimento incompleto ou política bloqueada. Escrever apenas `REG_DWORD 0` no valor nomeado. A intenção é persistida no diário compartilhado antes da chamada de escrita; a leitura posterior precisa confirmar tipo e dado.
- **Reversão:** reler e só prosseguir se o valor ainda for exatamente o alvo aplicado. Se o snapshot tinha um valor, restaurar o tipo e dado capturados. Se o snapshot dizia que o valor não existia, excluir somente `HideFileExt`, sem apagar a chave nem outros valores. O protótipo não cria a chave pai; se ela não existir, recusa a escrita. Uma alteração externa gera conflito e não é sobrescrita.
- **Avaliação:** comparar a leitura do valor antes/depois e confirmar a exibição no Explorador manualmente. Isso avalia a preferência, não velocidade nem FPS.

### Gestão organizacional e políticas

O candidato fica em `HKCU\Software`, não em uma árvore `Policies`. Isso, sozinho, não prova que o valor seja livre de gerenciamento: scripts, Group Policy Preferences, MDM ou ferramentas corporativas podem voltar a definir preferências do usuário. A documentação ADMX da Microsoft explica que políticas compatíveis são mapeadas para valores do Registro e que Group Policy/MDM podem aplicar essas configurações. Uma chave sob `Policies` também não é prova isolada de que uma política suportada esteja ativa.

O protótipo não tenta editar política nem promete detectar toda forma de gerenciamento. O adaptador Windows reporta gerenciamento como desconhecido; com esse estado, aplicação e reversão são recusadas. Uma futura execução requer revisão explícita de que o dispositivo/perfil não é administrado e, ainda assim, consulta nova, consentimento específico e confirmação da operação. Em dispositivo gerenciado, a preferência deve ser alterada pelo administrador no mecanismo de gestão correspondente.

Fontes técnicas:

- [Microsoft Support — extensões de nome de arquivo no Windows 10/11](https://support.microsoft.com/pt-br/windows/experience/storage-filemanagement/common-file-name-extensions-in-windows).
- [Repositório Microsoft `winget-dsc` — recurso WindowsExplorer e mapeamento `HideFileExt`](https://github.com/microsoft/winget-dsc/blob/main/resources/Microsoft.Windows.Developer/Microsoft.Windows.Developer.psm1).
- [Microsoft Learn — políticas baseadas em ADMX e associação a valores do Registro](https://learn.microsoft.com/windows/client-management/understanding-admx-backed-policies).
- [Microsoft Learn — abrir o Registro numa visão especificada](https://learn.microsoft.com/dotnet/api/microsoft.win32.registrykey.openbasekey?view=net-10.0).

**Estado desta etapa:** código e cenários simulados podem ser implementados com esse contrato restrito. Nenhuma chave de usuário ou chave de teste pode ser criada/alterada sem aprovação específica. Os testes automáticos desta etapa devem usar somente um adaptador falso em memória.

### Procedimento proposto para um teste de integração isolado

O teste futuro deve usar um runner de teste, não o valor funcional `HideFileExt`, e uma chave única gerada para cada execução: `HKCU\Software\CalixtoOptimizer\IntegrationTests\<GUID>`, valor `ProbeValue`, tipo `REG_DWORD`, visão `Registry64`. O runner deve confirmar que a chave GUID ainda não existe, criar a chave e o valor `0`, ler e comparar hive/visão/tipo/dado, mudar o mesmo valor para `1`, reler, excluir somente `ProbeValue`, confirmar que a chave GUID ficou vazia, removê-la e confirmar a ausência. Os pais `Software\CalixtoOptimizer\IntegrationTests` ficam intactos. Qualquer conteúdo inesperado interrompe o teste sem exclusão.

Isso verifica as chamadas locais de Registro, a ACL do perfil e a visão 64 bits; não valida que o Explorador atualize sua apresentação. O runner/adapter de teste ainda precisa ser criado. A operação não será executada sem aprovação explícita para criar, alterar e remover somente essa chave temporária.
