# Recuperação

## Aplicativo atual

O app inicializa em diagnóstico somente leitura. Nenhuma configuração do Windows é modificada, então encerrar ou remover esta versão não exige restaurar configurações.

## Infraestrutura no Core

O Core contém um diário JSON separado por operação, salvamento por arquivo temporário e substituição no mesmo diretório, consentimento associado ao plano e modelos para snapshots. Testes exercitam essa persistência em uma pasta temporária.

O diário pode classificar uma operação potencialmente real como snapshot disponível para revisão manual ou snapshot ausente. A avaliação de retomada diferencia preparação ainda não iniciada, conflito de plano, operação terminada e estado que exige revisão manual. Ela nunca reaplica nem reverte automaticamente.

O histórico está ligado à interface. O usuário pode executar uma demonstração sintética explícita, que persiste apenas o plano, consentimento e resultado do simulador em memória. Esse registro não contém snapshots e não é backup do Windows. Não há provedor que leia configurações reais do sistema, executor, rollback ou recuperação automática. Testes de corrupção, falha de escrita, disco cheio, interrupção e conflito usam dados temporários e falhas injetadas.

## Requisitos antes de qualquer ajuste real

Cada ajuste futuro precisa de captura allowlist do estado anterior, persistida e associada à versão exata do ajuste; consentimento explícito por consequência; verificação do resultado; conflito detectado; e estratégia de reversão documentada. Snapshots devem conter somente os valores necessários, sem identificadores ou segredos desnecessários.

Instalação/remoção de drivers, limpeza, firmware, tensão, frequência e outros efeitos externos não podem prometer rollback completo. Qualquer reversão real exigirá implementação e validação próprias; o avaliador atual só sinaliza revisão manual e não executa comandos de restauração.
