# Publicação manual da Release experimental

O repositório mostrado anteriormente está privado. Enquanto continuar privado, a Release só será visível a quem tem acesso ao repositório. Este documento não publica arquivos.

## Arquivos

- ZIP autocontido: `artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip`
- EXE após compilação: `artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe`

Confira os SHA-256 registrados em `docs/PROGRESS.md`. Envie exatamente esses arquivos; não envie bin, obj, logs locais ou relatórios diagnósticos.

## Pelo site do GitHub

1. Abra [Nova Release](https://github.com/icalixtozx/otimizador-calixto/releases/new) com a conta que acessa o repositório.
2. Crie a tag `v0.5.0-experimental` apontando para o commit `main` que contém a documentação e o script compilado neste ciclo.
3. Título: **Calixto Optimizer 0.5.0 experimental**.
4. Anexe o ZIP e o EXE.
5. Marque **pre-release** e não marque como versão estável mais recente.
6. Notas sugeridas:

   > Versão experimental 0.5.0 para Windows x64. Ainda não validada em outro computador. O aplicativo inicia em diagnóstico e não altera configurações automaticamente. Compilar o instalador não comprova instalação ou desinstalação; esses testes estão pendentes. Confira os SHA-256 antes de usar.

7. Confira nomes, tamanhos e hashes dos anexos e publique pelo botão **Publish release**.

Não publique como estável nem declare instalação/desinstalação verificadas antes de executar os testes. Nenhuma Release foi publicada nesta preparação.
