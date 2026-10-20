Calixto Optimizer Preview R4 — 0.6.0 experimental
Windows x64 autocontido. A janela exibe Preview R4 e o commit de origem;
passe o mouse sobre o commit no rodapé para consultar o hash completo.
Experimento não validado em outro computador.
Origem do binário: commit @SOURCE_COMMIT@.

PORTÁTIL
1. Extraia o ZIP completo para uma pasta gravável.
2. Abra Optimizer.App.exe nessa pasta. O runtime .NET está incluído.
3. Este é R4. Não use o executável framework-dependent em src/Optimizer.App/bin.

ALTERAÇÃO NESTA COMPILAÇÃO
- Corrige um encerramento ao abrir Drivers: as propriedades somente leitura
  da prévia agora usam bindings de leitura. A correção foi compilada e os
  testes de Core passaram; a abertura visual de Drivers ainda requer esta
  nova compilação.
- Nenhuma operação de driver é habilitada por este ajuste. Staging, associação,
  remoção e instalação de drivers continuam indisponíveis/bloqueados.

LIMITES CONHECIDOS
- O aplicativo inicia em diagnóstico. Nenhuma alteração é aplicada automaticamente.
- A origem WinGet está indisponível neste perfil do Windows; a instalação de
  programas por WinGet continua bloqueada.
- O histórico persistido pelo runner apareceu em leitura do modelo, mas não foi
  exibido nas capturas visuais R3. A apresentação desse histórico segue pendente.
- A tela de Rede faz inventário local sob demanda. As sondagens de conectividade
  não são iniciadas automaticamente.
- Este pacote não foi validado em outro computador, nem se testou a instalação
  ou desinstalação do R4. O instalador não tem assinatura digital do publicador.

IDENTIDADE DA INSTALAÇÃO
O instalador usa AppId, diretório e atalho próprios R4. Não substitui nem atualiza
R2 ou R3. A publicação portátil pode coexistir em outra pasta.

VALIDAÇÃO DE DRIVER
O botão Drivers deve abrir a página sem encerrar o aplicativo. Isso valida somente
a navegação; não selecione instaladores nem tente staging/associação.
