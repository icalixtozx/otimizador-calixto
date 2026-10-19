# Recuperação

## Estado do aplicativo

O app inicia em diagnóstico; não lê VIDEOIDLE até o usuário pedir, não aplica nem restaura configurações automaticamente. A UI atual não expõe escrita real. A demonstração sintética persiste seu histórico em LocalAppData, separado das capturas técnicas reais.

## Captura técnica VIDEOIDLE

A captura sob demanda consulta o plano ativo e o índice AC exato de `VIDEOIDLE` e grava um registro mínimo em `%LOCALAPPDATA%\CalixtoOptimizer\History\RealVideoIdle`. Esse registro não é backup nem snapshot restaurável do Windows. Ele não contém o índice DC/bateria.

O serviço de escrita (não habilitado no app) persiste a intenção antes de alterar. `MutationMayHaveStarted`, `MutationUnverified`, estados de conflito e interrupção não são sucesso e não disparam rollback automático. A reversão codificada pede uma aprovação específica separada e somente tenta restaurar se o plano ativo ainda for o capturado e o valor atual ainda for exatamente o proposto. Verifica o valor restaurado com leitura posterior. Caso haja conflito, revisão manual.

As chamadas Win32 não formam uma transação com comparação atômica. Outra mudança entre a última leitura e a escrita/ativação pode criar um estado ambíguo; nessa situação o app deve interromper e solicitar revisão. Se o plano foi trocado, o app não reativa o plano antigo para tentar recuperar. A alteração em plano inativo, se houver corrida, exige revisão pela interface de energia ou `powercfg`.

O arquivo do diário fica na pasta do próprio usuário e pode ser editado/removido por esse usuário. O fingerprint detecta corrupção acidental, não prova autenticidade. Não use o estado do diário como autorização para um worker elevado; nenhum worker desse tipo existe nesta versão.

## Evidência de testes

Os testes exercitam sucesso, falha, estado divergente, conflito tardio, falha ao persistir o ponto de recuperação, interrupção após escrita falsa, verificação e reversão condicional com adaptador em memória. Esses testes não provam que a API real funciona, que o privilégio seja suficiente, nem que uma reversão real tenha ocorrido. Não foi realizada alteração no computador de desenvolvimento.

A demonstração sintética não cria capturas reais. Seus estados e capturas não podem ser usados para restaurar configurações.
