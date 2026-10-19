Calixto Optimizer 0.6.0 experimental
Windows x64. Experimento nao validado em outro computador.

Diagnostico local de hardware, dispositivos, drivers e planos de energia.
Consulta AC, DC e plano ativo sob acao explicita; captura persistida nao e
backup do Windows. Revisa consequencias por lado e apresenta fluxo separado
de autorizacao/aplicacao/restauracao. O app inicia sem executar alteracoes.
Nao promete ganho de FPS ou melhoria universal de desempenho.

Compatibilidade de modo de energia: Windows 11 desktop com APIs documentadas
e estado AC/DC/plano ativo legivel. Capturas antigas incompletas permanecem
historico e nao podem ser executadas. Divergencia requer nova captura/revisao.
As APIs nao oferecem compare-and-swap; uma corrida muito curta entre leitura
e setter ainda e possivel.

Estados:
- Diagnostico, leitura real e captura: somente leitura do Windows.
- Revisao da consequencia: nao e autorizacao.
- Aplicacao/restauracao: exigem consentimentos por consequencia, autorizacao
  explicita e frase exata; nao foram executadas nesta validacao.
- Resultado verificado: exige leituras reais posteriores; testes simulados
  nao comprovam aplicacao ou reversao no Windows.
- Demonstracao sintetica: dados artificiais, nao e backup nem diagnostico real.

Distribuicao portatil:
1. Extraia o ZIP completo.
2. Abra Optimizer.App.exe. O runtime .NET Desktop esta incluido.

Instalador por usuario:
- EXE experimental produzido com Inno Setup 7.1.0; nao possui assinatura
  digital propria.
- Instala em LocalAppData\Programs\CalixtoOptimizer sem pedir elevacao.
- O instalador nao inicia o aplicativo automaticamente.
- Instalacao/desinstalacao do pacote 0.6.0 nao foram testadas nesta etapa.
- Nenhum worker elevado esta incluido; nao confie na pasta de instalacao ou
  historico gravavel pelo usuario como canal privilegiado.

Um diagnostico JSON revisavel pode incluir nomes/modelos de hardware e
drivers. Revise antes de compartilhar. IDs de dispositivo, GUIDs de planos,
nomes de conta/computador, seriais, enderecos de rede e caminhos locais sao
omitidos pelo relatorio sanitizado.

Os pacotes 0.5.0-experimental e a tag v0.5.0-experimental sao anteriores e
inalterados; nao incluem o recurso desta versao.

Nenhuma configuracao de energia, Registro, politica, servico, rede, driver,
prioridade, firmware ou VIDEOIDLE foi alterada para validar este pacote.
