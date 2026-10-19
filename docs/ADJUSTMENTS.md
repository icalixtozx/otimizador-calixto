# Catálogo e motor de ajustes

## Estado atual

O motor e os modelos de catálogo estão implementados em Optimizer.Core/Adjustments. **O catálogo de produto continua vazio**: não há ajuste real habilitado nem adaptador que altere o Windows. Fixtures sintéticas existem somente no harness de testes.

O simulador padrão é um adaptador em memória sem acesso a APIs do Windows ou armazenamento persistente. O resultado chama cada passo de “simulado” e informa explicitamente que nenhuma alteração ocorreu. Falha ou cancelamento encerra o fluxo, identifica o passo afetado e marca os passos seguintes como não executados.

## Metadados e compatibilidade

Cada definição contém ID e versão, descrição, área, nível de risco, consequência, exigência de administrador, arquiteturas e build mínimo declarados e disponibilidade. A disponibilidade só pode ser SimulationOnly ou BlockedExperimental; não existe estado que habilite execução real.

A compatibilidade compara o Windows, a arquitetura e, quando informado, o build mínimo. Um resultado compatível permite apenas simulação. Arquitetura/build ausente é desconhecido; fora dos limites é incompatível; itens experimentais explicitamente bloqueados não entram na simulação.

Essas verificações descrevem metadados fornecidos pelo catálogo; não provam compatibilidade com uma GPU, driver, firmware ou configuração específica.

## Planos

O gerador de planos preserva a consequência e o risco, distingue itens prontos para simulação, incompatíveis, bloqueados e IDs desconhecidos, e calcula uma impressão digital SHA-256 do conteúdo selecionado e da plataforma. Um plano pode ser simulado somente quando todos os itens são conhecidos e compatíveis.

CanApply permanece sempre falso nesta versão. A impressão digital vincula plataforma, IDs e versões, estado, risco, consequência e metadados apresentados. A interface oferece seleção do perfil, revisão do plano e consentimento individual somente para a demonstração sintética. Nenhum recibo autoriza aplicação real.

## Exclusões deliberadas

Não há alterações genéricas de Registro ou políticas, comandos PowerShell, desativação de proteções ou serviços, prioridade Tempo real, limpeza de RAM, ajustes em massa de rede, instalação de drivers, mudanças de GPU, tensão, frequência, BIOS ou firmware. Não se afirmam ganhos de desempenho.

O Core contém diário JSON, transições que exigem consentimento e snapshots para cada item antes de armar uma operação e avaliação de recuperação manual. A interface lê o histórico local e identifica a origem dos registros. Demonstrações sintéticas não aceitam snapshots. Os snapshots dos testes não são backups do Windows, e recuperação automática não existe.

## Proposta única para validação futura — tempo de desligamento da tela na tomada

**Estado: proposta documentada, sem código de aplicação real e sem execução.** A interface não lista este ajuste como disponível. Exige aprovação específica antes de qualquer implementação real ou execução.

- **Configuração exata:** mudar somente `Display > Turn off display after` para o lado AC do plano ativo, valor exato `900` segundos (15 minutos). O índice DC/bateria e as demais configurações do plano permanecem intocados. Se o valor atual já for 900 segundos, a operação seria no-op. A Microsoft identifica a configuração pelo alias `VIDEOIDLE`, GUID `3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e`, com valores em segundos e suporte desde Windows Vista.
- **Fonte oficial e comandos:** a captura/consulta pode usar `powercfg /getactivescheme` e `powercfg /query <GUID-do-plano>`. A aplicação equivalente documentada usa `powercfg /setacvalueindex <GUID-do-plano> <7516b95f-f776-4464-8c53-06167f40cc99> <3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e> 900`; a Microsoft informa que uma escrita no plano ativo precisa ser seguida por `PowerSetActiveScheme`. O `powercfg /change monitor-timeout-ac 15` é outro comando documentado, com argumento em minutos, mas para reversão precisa-se preservar o índice exato em segundos. [Display idle timeout](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/display-settings-display-idle-timeout), [Powercfg command-line options](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options), [PowerWriteACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwriteacvalueindex), [PowerSetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme).
- **Compatibilidade:** configuração/API documentada para Windows Vista desktop ou posterior; o app distribuído aqui é Windows 10+ x64 por depender do .NET 10. O ajuste depende de um plano ativo e da exposição da configuração. Compatibilidade com Modern Standby, overlays, política corporativa, OEM, permissões e comportamento em cada edição ainda precisa ser validada por ambiente; nada disso será inferido do modelo de hardware.
- **Consequência:** a tela pode permanecer ligada por mais tempo quando o computador está na tomada, consumindo mais energia durante períodos ociosos. Não altera bateria/DC, CPU, GPU, rede, serviços, proteções ou firmware. Não há ganho de desempenho alegado.
- **Captura anterior:** obter o GUID do plano ativo e consultar o plano por `PowerGetActiveScheme`/`PowerReadACValueIndex` (ou pelos comandos de leitura acima). Registrar GUID do esquema, subgrupo Display, GUID da configuração e o índice AC original exato. Isto é um dado de reversão de uma configuração, não um backup do Windows.
- **Aplicação futura:** depois de verificar que plano e índice ainda correspondem à captura, chamar `PowerWriteACValueIndex` para escrever 900; chamar `PowerSetActiveScheme` com o mesmo GUID para efetivar; conferir com `PowerReadACValueIndex` que o índice AC é 900. A implementação deverá verificar códigos de retorno e recusar divergências. A exigência de elevação ainda precisa ser provada em ambiente de teste; não será presumida.
- **Reversão futura:** somente se o plano ativo continuar sendo o capturado e o índice AC ainda for 900, restaurar o índice AC original exato e chamar `PowerSetActiveScheme` para aplicar. Verificar o valor restaurado. Se um usuário ou outro agente tiver mudado o plano/valor, o app deve recusar reversão automática e pedir revisão manual. Fonte da leitura: [PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex).
- **Conflitos e recuperação:** plano removido, alternado ou administrado por política; OEM que reescreve o tempo; mudança manual concorrente; sleep/Modern Standby ou interface que represente o ajuste em overlay podem impedir uma restauração segura. Não haverá recuperação automática se a captura estiver ausente, incompleta ou conflitar. Um journal sintético não representa captura real.

Esta proposta não foi implementada como operação real. Os testes atuais usam storage e adaptadores injetáveis; não chamam `powercfg`, `PowerWriteACValueIndex` ou `PowerSetActiveScheme`.
