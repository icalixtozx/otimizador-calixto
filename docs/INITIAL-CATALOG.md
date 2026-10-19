# Catálogo inicial de candidatos

Este catálogo é uma triagem técnica, não uma lista de otimizações habilitadas. Somente o teste VIDEOIDLE tem adaptador técnico. As áreas processos, Registro, políticas e rede permanecem **indisponíveis**: não existe uma alteração universal com benefício comprovado que justifique uma escrita genérica.

## Energia — VIDEOIDLE AC = 900 segundos

- **Objetivo/mecanismo:** testar o ciclo de uma configuração real, identificada pelo GUID documentado `VIDEOIDLE`, alterando somente o índice AC do plano ativo. Não é ajuste de FPS nem promessa de desempenho.
- **Compatibilidade:** API de configuração documentada desde Windows Vista para aplicativos desktop; o app experimental x64 exige Windows 10+/.NET 10. Planos OEM, políticas, overlays e Modern Standby ainda carecem de validação por ambiente.
- **Consequências:** a tela pode ficar ligada por mais tempo em períodos ociosos e consumir mais energia. Índice DC/bateria e demais opções ficam fora do adaptador.
- **Privilégios:** leitura/captura usa APIs de consulta. A permissão necessária para a escrita não é presumida; erro de acesso encerra a operação. A UI atual não oferece escrita.
- **Captura/reversão:** registra GUID do esquema ativo e índice AC original. Reversão só é permitida se o mesmo esquema continuar ativo e o valor atual ainda for 900; grava o valor AC original e confirma por nova leitura. Conflitos exigem revisão manual.
- **Avaliação:** comparar o índice AC lido antes/depois e observar o comportamento de desligamento da tela; não medir nem alegar FPS. A verificação via API confirma o valor configurado, não o comportamento de cada display/OEM.
- **Estado:** leitura e captura real implementadas; escrita/verificação/reversão codificadas atrás de política bloqueada no app. Cobertura de escrita usa somente adaptador falso. Nenhuma execução real foi feita.

Fontes: [Display idle timeout](https://learn.microsoft.com/en-us/windows-hardware/customize/power-settings/display-settings-display-idle-timeout), [PowerGetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme), [PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex), [PowerWriteACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powerwriteacvalueindex), [PowerSetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme).

## Processos — encerramento cooperativo por aplicativo (indisponível)

- **Objetivo/mecanismo:** eventual redução de carga somente para um aplicativo e uma sessão de trabalho identificados. Não há mecanismo universal de encerramento cooperativo; depende de suporte explícito do próprio aplicativo. O app não envia `TerminateProcess`.
- **Compatibilidade:** teria de ser definida por aplicativo, versão e sessão; nada é compatível por inferência do nome do processo.
- **Consequências:** fechamento pode perder dados não salvos, interromper tarefas, perder estado da sessão e alterar medições. Encerrar processos à força não prova melhoria de jogo ou FPS.
- **Privilégios:** nenhuma permissão é solicitada enquanto indisponível. Um mecanismo futuro dependeria do processo alvo e de seus direitos de acesso; não pode solicitar elevação em lote.
- **Captura/reversão:** para uma proposta específica, identificar processo e sessão antes da ação. Reabrir um executável não restaura documentos, memória ou trabalho anterior; portanto, não existe reversão geral segura.
- **Avaliação:** somente com carga de trabalho repetível e métricas de CPU, memória e frametime antes/depois; rejeitar se interromper tarefas ou se a diferença não for repetível.
- **Estado:** indisponível. A documentação do Windows descreve que encerramento forçado finaliza threads e pode comprometer dados globais de DLL; esse risco impede tratá-lo como limpeza universal.

Fonte: [TerminateProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess).

## Registro — sem chave candidata (indisponível)

- **Objetivo/mecanismo:** nenhum ajuste de Registro de desempenho foi selecionado. Uma proposta futura precisa apontar uma configuração documentada do Windows ou do fabricante e usar o mecanismo suportado por ela; não haverá pacote de “tweaks” genéricos.
- **Compatibilidade:** depende da chave, tipo, arquitetura/hive, build, componente proprietário e eventual política. Não existe compatibilidade genérica para caminhos copiados de outras máquinas.
- **Consequências:** valores incorretos podem mudar comportamento de sistema/aplicativo, ser ignorados, sobrescritos ou impedir inicialização correta.
- **Privilégios:** não aplicável enquanto indisponível. Uma eventual escrita em HKCU e uma em HKLM têm escopos e permissões diferentes; o privilégio será declarado para cada item.
- **Captura/reversão:** exigiria caminho/hive/vista do Registro, presença anterior, tipo e dados exatos, além da origem (preferência ou política). Só restaurar se o valor ainda for aquele gravado pelo app; não haverá restauração por importar um `.reg` indiscriminado.
- **Avaliação:** definir uma métrica diretamente ligada à função documentada e testar antes/depois sob a mesma carga. Sem hipótese mensurável, a escrita não entra no catálogo.
- **Estado:** indisponível; sem candidato nem código de escrita.

Fonte: [Implementing Registry-based Policy](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/policy/implementing-registry-based-policy), que descreve locais de Registro específicos para configurações de política de aplicativos, não uma receita de desempenho para o Windows inteiro.

## Políticas — sem alteração candidata (indisponível)

- **Objetivo/mecanismo:** nenhuma mudança de política é proposta para obter desempenho. Políticas são controle administrativo de configuração; uma proposta futura teria de ser uma configuração específica com documentação e escopo de administração.
- **Compatibilidade:** pode depender de edição do Windows, domínio, MDM, ADMX, usuário/computador e ordem de aplicação.
- **Consequências:** atualização de política pode sobrescrever a mudança; alterações locais podem conflitar com a administração da organização ou enfraquecer requisitos de segurança.
- **Privilégios:** indisponível significa nenhuma elevação. Mudanças futuras só por um administrador/gestor autorizado ao escopo e sem contornar domínio/MDM.
- **Captura/reversão:** registrar a origem efetiva e o valor configurado. Se domínio/MDM for a autoridade, o proprietário da política deve corrigir ou reverter; o app não deve escrever diretamente na cópia local do Registro.
- **Avaliação:** medir conformidade com a política e o objetivo administrativo documentado. Nenhum ganho de FPS será inferido de política aplicada.
- **Estado:** indisponível; sem mudança nem comando `gpedit`, Registro de política ou atualização forçada.

Fontes: [Group Policy Preferences](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/group-policy/group-policy-preferences) e [Group Policy Objects](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/policy/group-policy-objects). A documentação explica precedência/refresh e escopo administrativo.

## Rede — TCP receive-window autotuning (não alterar; indisponível)

- **Objetivo/mecanismo:** candidato de investigação, não de tweak: observar o TCP receive-window autotuning e manter a configuração padrão até haver uma medição do workload que justifique outra escolha. O Windows usa a janela de recepção adaptativa para adequar conexões.
- **Compatibilidade:** o guia citado trata de Windows Server e NICs. Não prova uma configuração ideal para PCs, jogos, VPNs, drivers ou redes específicas.
- **Consequências:** restringir/desativar autotuning pode reduzir a janela de recepção e throughput em alguns cenários. Não há afirmação de que alterá-lo reduza ping ou melhore jogos.
- **Privilégios:** app não solicita permissão porque não altera nada. Uma mudança global futura pode exigir elevação; isso precisa ser verificado pelo comando/API e versão antes de habilitar.
- **Captura/reversão:** proposta futura deve capturar cada estado global efetivo e persistente de forma estruturada, sem interpretar saída localizada. Reverter somente os campos alterados e apenas se não houver conflito; não substituir estado global por um valor padrão presumido.
- **Avaliação:** A/B repetido para o mesmo destino e rota, medindo throughput, RTT, perda e variabilidade, além do workload que motivou o estudo. Se não houver diferença repetível ou houver regressão, descartar a mudança.
- **Estado:** indisponível, leitura de diagnóstico ainda não implementada como catálogo e nenhuma chamada `netsh`/API de escrita.

Fontes: [Windows network interface commands](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/netsh-interface) e [Network Adapter Performance Tuning in Windows Server](https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics). O guia apresenta `Normal` como padrão em seu contexto de Windows Server; não é prova de ganho em Windows cliente.

## Regra de entrada no catálogo

Cada futuro item deve ter dono do mecanismo, fonte oficial, limites de plataforma, consequência, captura, verificação, reversão sem sobrescrever mudanças concorrentes e protocolo de avaliação repetível. Até validar esse conjunto, a UI deve dizer **indisponível** e não deve oferecer execução.
