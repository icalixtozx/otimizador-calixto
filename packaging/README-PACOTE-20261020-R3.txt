Calixto Optimizer Preview R3 — 0.6.0 experimental
Windows x64 autocontido. Build do commit de origem indicado no rodapé da janela:
o título exibe Preview R3 e o commit curto; passe o mouse sobre o commit no rodapé
para consultar o hash completo. Experimento não validado em outro computador.

PORTÁTIL
1. Extraia o ZIP completo para uma pasta gravável.
2. Abra Optimizer.App.exe nessa pasta. O runtime .NET está incluído.
3. O executável correto é o que fica na raiz da pasta extraída; não use um
   Optimizer.App.exe antigo em src\Optimizer.App\bin nem a Preview R2 instalada.

IDENTIFICAÇÃO E LIMITES
- O aplicativo inicia em diagnóstico. Esta publicação não executa alterações
  automaticamente e não promete ganho de FPS.
- A execução de instalação por WinGet continua bloqueada neste perfil; o motivo
  conhecido é a origem AppX ausente. O inventário e as demais páginas são locais
  e independentes desse gerenciador.
- Drivers assistidos continuam parciais. Não use esta publicação para staging,
  associação, remoção ou atualização de drivers.
- A execução real do serviço de modo de energia neste computador já foi validada
  em um ciclo AC Equilibrado → Melhor desempenho → Equilibrado. DC e plano ativo
  foram preservados. Isso valida o serviço neste PC, não a integração visual nem
  outro computador. Nenhuma alteração permanece autorizada por este teste.
- A leitura AC/DC/plano é uma preferência configurada, não garantia do modo
  efetivo. Capturas são dados de recuperação limitados, não backup do Windows.

HISTÓRICO
O fluxo real do runner e a interface usam o mesmo armazenamento:
%LOCALAPPDATA%\CalixtoOptimizer\History\RealPowerMode. A página Energia >
Histórico lê os registros do serviço e deve mostrar o ciclo real com estado
“Restaurado/Revertido”; ele não é misturado com a demonstração sintética. A
validação de leitura do mesmo diário está documentada no relatório desta build.

CHECKLIST VISUAL — NÃO APLIQUE NOVAS ALTERAÇÕES
- Energia: confira AC, DC e plano atual somente se decidir fazer uma nova leitura;
  confirme que aplicação/restauração pedem autorização explícita e não acione.
- Histórico: encontre o ciclo real como restaurado/revertido; diferencie-o da
  demonstração e confirme que a captura não é apresentada como backup.
- Programas: confirme a indisponibilidade do WinGet com motivo e que o inventário
  local abre; não execute instalação.
- Processos: confira lista, estados de acesso e consequências; não altere prioridade.
- Registro: confira avaliação de gerenciamento/estado desconhecido e o bloqueio
  de escrita; não aplique preferência nem política.

O EXE do instalador é separado desta distribuição portátil e não foi instalado
nem desinstalado para esta validação. Setup não assinado digitalmente pelo
publicador do aplicativo. Sem validação em outro computador.
