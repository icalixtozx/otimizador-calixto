# Candidatos iniciais de desempenho (propostos)

**Estado:** propostas para escolha. Não estão implementadas nem foram aplicadas. Nenhuma promete aumento de FPS; disponibilidade e resultado dependem do Windows, do hardware, do driver, do jogo e do gargalo atual.

## 1. Modo de energia “Melhor desempenho”

- **Objetivo/mecanismo:** favorecer desempenho quando necessário. A Microsoft descreve “Best performance” como maximizando desempenho e podendo ajudar a CPU a operar em desempenho mais alto quando necessário; não fixa frequência nem vence limites térmicos, elétricos ou do firmware.
- **Fontes:** [Alterar modo de energia](https://support.microsoft.com/en-us/windows/change-the-power-mode-for-your-windows-pc-c2aff038-22c9-f46d-5ca0-78696fdf2de8); [dicas de desempenho](https://support.microsoft.com/en-us/windows/experience/performance-optimization/tips-to-improve-pc-performance-in-windows).
- **Compatibilidade:** Windows 10/11, mas a opção pode não existir em todo PC ou ficar indisponível com plano personalizado. Portáteis podem expor estados separados para tomada e bateria.
- **Consequências:** maior consumo; em notebook, menor duração de bateria, mais calor e ventoinha mais rápida/ruidosa. Não garante ganho em jogos.
- **Privilégios:** mudança manual em Configurações. Plano personalizado ou política do fabricante/organização pode impedir mudança; o app não contorna bloqueios.
- **Captura/reversão:** registrar a seleção para tomada e bateria e o plano ativo; restaurar cada seleção exata capturada e confirmar por leitura.
- **Avaliação:** repetir o mesmo benchmark/cena, jogo e versão, resolução, qualidade e condição térmica; registrar FPS médio, 1% low, tempos de quadro, CPU/GPU, temperatura, energia e ruído. Reverter se não houver diferença repetível ou se os efeitos forem indesejáveis.

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
