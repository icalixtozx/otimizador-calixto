Calixto Optimizer 0.5.0 experimental
Windows x64. Experimental e nao validado em outro computador.

Diagnostico local de hardware, dispositivos/drivers e planos de energia.
Lista planos e identifica o plano ativo; exibe valores VIDEOIDLE AC/DC.
Mostra codigos PnP reportados pelo Windows sem afirmar automaticamente
que indicam driver ausente. Nao classifica drivers como desatualizados.

O JSON diagnostico e exibido para revisao antes de salvar. O arquivo nao e
enviado automaticamente e omite IDs de dispositivo, GUIDs de planos,
nomes da conta/computador, seriais, enderecos de rede e caminhos locais.
Ainda contem modelos/nome de hardware e driver; revise antes de compartilhar.

Estados visiveis:
- Demonstracao sintetica: dados artificiais; nao e ajuste nem backup do Windows.
- Leitura/previsao real VIDEOIDLE: somente leitura sob acao explicita.
- Captura tecnica VIDEOIDLE: persistencia local do valor AC; nao e backup.
- Aplicacao/reversao real: bloqueadas no aplicativo.
- Resultado verificado: somente apos leitura posterior no fluxo real; testes fake nao contam como resultado do Windows.

Distribuicao portatil:
1. Extraia o ZIP completo.
2. Abra Optimizer.App.exe.
3. O runtime .NET Desktop esta incluido no ZIP autocontido.

Instalador por usuario:
- CalixtoOptimizer.iss e o script-fonte do Inno Setup.
- Compilador Inno Setup 7.1.0 ainda precisa ser disponibilizado pelo usuario.
- Instala em LocalAppData\Programs\CalixtoOptimizer, sem pedir elevacao.
- O instalador nao inicia o aplicativo automaticamente.
- A pasta de instalacao e gravavel pelo usuario. Nenhum worker elevado esta incluido.
- O historico em LocalAppData\CalixtoOptimizer fica separado e e mantido ao desinstalar.
- O EXE do instalador ainda nao foi produzido.
- Instalacao e desinstalacao ainda nao foram testadas.

Nenhum ajuste, driver, Registro, politica, servico, rede, energia ou firmware foi alterado para gerar o pacote.
