# Publicação manual da pre-release experimental 0.5.0

A pre-release ainda não foi publicada. O repositório é privado, portanto a Release será visível apenas para quem tem acesso. Não há `gh.exe` nem integração de publicação GitHub disponível nesta sessão; esta preparação deixa os artefatos, tag, hashes, notas e passos prontos para o site.

## Versão e arquivos

- Tag preparada: `v0.5.0-experimental`.
- ZIP autocontido Windows x64: `artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip` — 65.449.812 bytes; SHA-256 `20704611C9967EBE3C47EB809C4079E2BC09227804A00759C25B1EB5C5EB916C`.
- Instalador Windows x64: `artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe` — 46.681.264 bytes; SHA-256 `D13B0A31222EA213444E7995B761AB67590420BCC28B7DE5E1E5E2B2BECFB19B`.

Anexe exatamente esses dois arquivos. Não anexe `bin`, `obj`, logs locais, relatórios diagnósticos ou fontes de dados pessoais. O ZIP tem 406 entradas e inclui o runtime .NET. O instalador foi compilado por Inno Setup 7.1.0. A assinatura Authenticode do compilador foi verificada; o EXE do aplicativo **não é assinado digitalmente**.

## Limitações a declarar

- Versão experimental; não validada em outro computador.
- Instalação e desinstalação foram testadas no PC de desenvolvimento; o app não foi aberto nesse teste.
- O aplicativo inicia em diagnóstico. Não aplica otimizações automaticamente.
- O recurso de modo de energia configurado está no código posterior de desenvolvimento 0.6; não está incluído nos pacotes 0.5.0 desta tag.
- VIDEOIDLE não foi aplicado. Nenhum ajuste de energia, Registro, política, rede, serviço ou driver foi aplicado.
- Os 39 checks locais incluem simulações; não comprovam ganhos de desempenho, compatibilidade ampla ou reversão real de ajustes.
- O Inno Setup informou `Non-commercial use only`; revisar os [termos oficiais](https://jrsoftware.org/isorder.php) antes de qualquer uso comercial.

## Publicar pelo site

1. Abra [Nova Release](https://github.com/icalixtozx/otimizador-calixto/releases/new) com uma conta que tenha acesso ao repositório.
2. Selecione a tag existente `v0.5.0-experimental`. ZIP e instalador correspondem a essa revisão. O `main` atual avançou para desenvolvimento 0.6.0 e inclui modo de energia; não crie nem mova a tag para o `main` atual.
3. Use o título **Calixto Optimizer 0.5.0 experimental**.
4. Anexe o ZIP e o Setup listados acima.
5. Marque **Set as a pre-release** e não marque a versão como a última estável.
6. Cole estas notas, ajustando apenas se houver novos resultados verificados:

   > Versão experimental 0.5.0 para Windows x64. ZIP autocontido e instalador incluídos. Instalação e desinstalação testadas no PC de desenvolvimento; ainda não validada em outro computador. O aplicativo inicia em diagnóstico, sem alterações automáticas. VIDEOIDLE não foi aplicado. O instalador EXE não tem assinatura digital própria. Confira os SHA-256 antes de usar.

7. Confira nomes, tamanhos e SHA-256 dos anexos; então selecione **Publish release**.

A compilação e os testes locais não publicam uma Release. Até a conclusão dos passos do site, a pre-release permanece não publicada.
