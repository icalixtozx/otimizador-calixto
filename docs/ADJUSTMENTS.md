# VIDEOIDLE: teste técnico do motor real

## Estado

O primeiro adaptador real está implementado para o valor `VIDEOIDLE` no lado AC do plano ativo, com alvo proposto de **900 segundos**. O app permite, somente após ação explícita, ler o valor real e salvar uma captura técnica separada no histórico `RealVideoIdle`. Não faz essa leitura automaticamente na inicialização.

O app não oferece caminho de escrita: aplicar/reverter estão bloqueados pela política `BlockedVideoIdleMutationPolicy` (nega sempre), e não existe comando/botão habilitado para isso. O serviço Core implementa o fluxo de aplicação, leitura posterior, detecção de conflito e reversão condicional para permitir testes com adaptador falso e futura validação. A implementação não foi executada nesta máquina.

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
