# Catálogo de desempenho e próximos candidatos

**Estado:** o modo de energia foi selecionado e tem código no desenvolvimento 0.6, com leitura real e fluxo de escrita/reversão testado somente por simulação; nenhuma escrita foi feita. Os candidatos de GPU abaixo continuam propostas não implementadas. Nenhum promete aumento de FPS; disponibilidade e resultado dependem do Windows, hardware, driver, workload e gargalo.

## 1. Modo de energia “Melhor desempenho”

- **Objetivo/mecanismo:** o Windows documenta três preferências configuradas pelo usuário, incluindo “Best performance”, como um sinal para o sistema; não fixa frequência nem vence limites térmicos, elétricos ou do firmware. Esta é a preferência de modo, não o plano ativo.
- **Fontes de implementação:** [API para AC](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfiguredacpowermode) e [API para DC](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powersetuserconfigureddcpowermode); ambas documentadas para apps desktop no Windows 11.
- **Compatibilidade:** Windows 11 build 22000+ com as funções PowrProf presentes. AC e DC podem ter valores e falhas independentes. Windows 10 fica indisponível para este recurso; política, OEM e outros sinais podem sobrepor a preferência.
- **Consequências:** Melhor desempenho pode elevar consumo, temperatura e ruído e reduzir autonomia na bateria; não garante ganho em jogos.
- **Privilégios:** app é `asInvoker`; escrita real não foi validada, e o erro nativo será mostrado. Não há worker elevado nem bypass de política.
- **Captura/reversão:** captura persistente por operação só para os lados selecionados. Motor faz detecção de conflito e restaura o valor capturado somente se o estado atual ainda corresponder ao valor capturado ou ao alvo; confirmação por getter não demonstra efetividade. Mudança concorrente entre leitura e setter ainda é possível.
- **Avaliação:** comparar o modo configurado e, após aprovação de execução, repetir o mesmo workload/benchmark e condição térmica, anotando frametime, energia, temperatura e ruído. Nenhum resultado de desempenho foi medido neste ciclo.
- **Estado verificado:** no Windows local build 26300, getters AC e DC retornaram Equilibrado. Build e 50 checks locais passaram; aplicação/reversão foram exercitadas apenas com adaptadores falsos. Nenhum setter real foi chamado. Consulte [detalhes, consentimento e limites](ADJUSTMENTS.md#modo-de-energia-configurado-pelo-usuário-windows-11).

## 2. Preferência de GPU “Alto desempenho” por jogo

- **Objetivo/mecanismo:** em PC com várias GPUs, selecionar a GPU de alto desempenho para um jogo que esteja usando a GPU de economia. A preferência não aumenta a capacidade da GPU.
- **Fontes:** [Preferência gráfica por app no Windows 11](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11); [configurações gráficas do DirectX](https://devblogs.microsoft.com/directx/navigating-the-redesigned-graphics-settings-page/).
- **Compatibilidade:** Windows 11, mais de uma GPU e suporte exposto pelo Windows/driver. Só faz sentido se o jogo estiver usando GPU inadequada; a opção pode não aparecer.
- **Consequências:** GPU discreta pode gastar mais energia, aquecer mais e aumentar ruído, especialmente em notebook. Pode não mudar nada se o jogo já usa a GPU correta; perfil do fabricante também pode influenciar.
- **Privilégios:** configurar em Configurações > Sistema > Tela > Gráficos > app > Opções. Se uma política bloquear, não contornar.
- **Captura/reversão:** registrar executável do jogo e preferência atual exata (Windows decide/economia/alto desempenho); restaurar a seleção anterior e reabrir o jogo para confirmar.
- **Avaliação:** verificar a GPU efetivamente usada no Gerenciador de Tarefas; repetir o mesmo benchmark, comparando tempos de quadro, FPS, temperatura e consumo; reverter se não for repetível.

## 3. Agendamento de GPU acelerado por hardware (HAGS)

- **Objetivo/mecanismo:** delegar a um processador dedicado na GPU a maior parte do escalonamento frequente da GPU, reduzindo trabalho de coordenação no CPU. É mudança ampla no caminho gráfico, não um ajuste universal.
- **Fontes:** [explicação da Microsoft DirectX sobre HAGS](https://devblogs.microsoft.com/directx/hardware-accelerated-gpu-scheduling/); [configurações gráficas](https://devblogs.microsoft.com/directx/navigating-the-redesigned-graphics-settings-page/); [capacidade WDDM para HAGS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmdt/ns-d3dkmdt-d3dkmt_wddm_2_7_caps).
- **Compatibilidade:** Windows 10 versão 2004 ou posterior, GPU e driver com suporte WDDM apropriado. Só considerar quando a opção aparece em Configurações > Sistema > Tela > Gráficos > configurações padrão; não inferir suporte pelo nome da placa. Ausente, fica indisponível.
- **Consequências:** a Microsoft descreve a mudança como geralmente transparente e não espera diferença significativa para a maioria. Resultado depende de GPU/driver; travamento, artefato ou regressão exigem reversão. O Windows pode pedir reinício.
- **Privilégios:** operar somente pela interface do Windows. Não gravar Registro nem contornar políticas. Antes de adaptar o app, validar permissões, escopo e caminho suportado de leitura/escrita e recuperação.
- **Captura/reversão:** capturar estado habilitado/desabilitado e estado do driver; restaurar pela interface ao valor capturado; reiniciar se solicitado e confirmar a leitura.
- **Avaliação:** alterar uma variável por vez; comparar benchmarks repetíveis, tempos de quadro, estabilidade e temperatura. Sem diferença repetível, manter o estado original.

## Protocolo antes de implementar

Confirmar Windows/hardware/suporte; capturar o estado; obter aprovação específica; alterar uma opção por vez; medir em execuções equivalentes; restaurar o estado capturado se houver conflito ou se o resultado não for repetível. Escolher um candidato não autoriza aplicá-lo neste computador.
