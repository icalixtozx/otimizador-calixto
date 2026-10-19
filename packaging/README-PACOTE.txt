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
- EXE experimental produzido com Inno Setup 7.1.0; nao possui assinatura digital propria.
- Instala em LocalAppData\Programs\CalixtoOptimizer, sem pedir elevacao.
- O instalador nao inicia o aplicativo automaticamente.
- A pasta de instalacao e gravavel pelo usuario. Nenhum worker elevado esta incluido.
- O historico em LocalAppData\CalixtoOptimizer fica separado e e mantido ao desinstalar.
- Instalacao e desinstalacao foram executadas no PC de desenvolvimento no diretorio de teste; arquivos, entrada de desinstalacao e atalho foram removidos.
- O aplicativo nao foi aberto durante o teste. O pacote nao foi testado em outro PC.

Nenhum ajuste de energia, Registro de ajuste, politica, servico, rede, driver ou firmware foi aplicado. VIDEOIDLE e os valores do Windows foram preservados. O teste de instalacao criou e removeu apenas os componentes de instalacao do proprio aplicativo.
