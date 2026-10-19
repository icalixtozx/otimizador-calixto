Calixto Optimizer 0.4.0 experimental
Windows x64. Experimental e nao validado em outro computador.

O aplicativo inicia em diagnostico. Nenhuma alteracao do Windows e aplicada automaticamente.

Estados visiveis:
- Demonstracao sintetica: dados artificiais; nao e ajuste nem backup do Windows.
- Previa real VIDEOIDLE: leitura sob demanda do plano ativo e valor AC.
- Captura tecnica real: persistencia do valor AC original, separada da demonstracao; nao e backup restauravel.
- Execucao real: bloqueada no aplicativo por padrao.
- Resultado verificado: somente um status apos leitura posterior no fluxo real; testes com fake nao contam como resultado do Windows.

A demonstracao opcional usa apenas dados sinteticos. Energia VIDEOIDLE esta implementado como teste tecnico: captura/leitura real disponivel pelo botao; aplicar e reverter continuam bloqueados. Processos, Registro, politicas e rede permanecem indisponiveis.

Distribuicao portatil:
1. Extraia o ZIP completo.
2. Abra Optimizer.App.exe.
3. O runtime .NET Desktop esta incluido no ZIP autocontido.

Instalador por usuario:
- CalixtoOptimizer.iss e o script-fonte do instalador Inno Setup.
- Instala em LocalAppData\Programs\CalixtoOptimizer, sem pedir elevacao.
- O instalador nao inicia o aplicativo automaticamente.
- A pasta de instalacao e gravavel pelo usuario. Nenhum worker elevado esta incluido.
- O historico em LocalAppData\CalixtoOptimizer fica separado e e mantido ao desinstalar.
- O EXE do instalador nao esta incluido neste ZIP; o script e as instrucoes ficam no repositorio.
- Instalacao, desinstalacao e compilacao do script ainda precisam ser testadas.

Nenhum ajuste, driver, Registro, politica, servico, rede, energia ou firmware foi alterado para gerar o pacote.
