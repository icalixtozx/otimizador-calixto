# Recuperação

## Primeira entrega

O aplicativo é somente leitura. Encerrar ou remover o app não requer restaurar configurações do Windows, pois nenhuma configuração é modificada.

O CSV é salvo apenas quando o usuário escolhe um caminho no diálogo de exportação. Revise e apague o arquivo manualmente se não quiser mantê-lo.

## Futuras versões com escrita

Não implemente escrita no Windows antes de haver diário persistente com captura do estado anterior, checagem de conflito, consentimento e reversão por ajuste. Instalação/remoção de driver, limpeza, firmware e efeitos externos não podem prometer rollback completo.
