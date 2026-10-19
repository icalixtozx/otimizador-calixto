# Instalador experimental

CalixtoOptimizer.iss empacota a publicação autocontida Windows x64 0.5.0-experimental de artifacts/release-v0.5.0-experimental/CalixtoOptimizer.

## Estado atual do Inno Setup

A instalação foi iniciada pelo comando oficial autorizado:

    winget install --id JRSoftware.InnoSetup.7 -e -s winget -i

O assistente interativo abriu em “Selecione o Idioma do Instalador” e aguarda seleção, leitura/aceitação dos termos e conclusão pela interface. ISCC.exe ainda não foi confirmado; portanto o .iss não foi compilado e nenhum EXE do instalador foi produzido. Uma segunda tentativa simultânea retornou 0x80070020 porque o primeiro processo ainda usava o executável temporário. Termine a janela aberta antes de tentar novamente.

## Verificação oficial e licença

Em 2026-10-19, o fornecedor listava Inno Setup 7.1.0 x64 e indicava o ID WinGet JRSoftware.InnoSetup.7. O comando acima é o interativo documentado pelo fornecedor. winget show retornou versão 7.1.0, licença “Inno Setup License” e fonte oficial GitHub, embora a atualização da fonte WinGet tenha falhado.

O fornecedor documenta três métodos de autenticidade: Authenticode (assinatura válida; publicador Pyrsys B.V.), GitHub Release Attestations e ISSigTool. A instalação iniciada via manifesto oficial ainda não teve sua assinatura verificada independentemente.

A Inno Setup License permite uso e redistribuição, inclusive comercial, sob condições que incluem preservar avisos, não alegar autoria do Inno Setup e marcar versões modificadas. O fornecedor pede licença comercial a usuários comerciais; sua página diz que para projeto recém-iniciado sem instalador de produção não se espera comprar antes do instalador estar pronto para produção. Leia a licença e os termos exibidos pelo assistente antes de concluir.

## Compilar depois da instalação

Na raiz do repositório, publique novamente a versão experimental e compile o script:

    dotnet publish .\src\Optimizer.App\Optimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\release-v0.5.0-experimental\CalixtoOptimizer
    ISCC.exe .\packaging\CalixtoOptimizer.iss

O EXE esperado é artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe. Confirme código de saída zero, existência, tamanho, arquivos incluídos e SHA-256. Compilar o script não comprova a execução da instalação nem da desinstalação. Não execute esse EXE sem aprovação específica.

## Privilégios e instalação

- PrivilegesRequired=lowest instala por usuário em %LOCALAPPDATA%\Programs\CalixtoOptimizer.
- O setup não inicia o app nem cria serviço, tarefa privilegiada ou worker.
- A pasta do app é gravável pelo usuário. Um futuro worker elevado não pode ser colocado ali; exigiria localização protegida, ACL adequada e protocolo autenticado.
- O histórico fica em %LOCALAPPDATA%\CalixtoOptimizer\History; o desinstalador não deve apagá-lo.

## Publicação pelo site

As instruções manuais para anexar o ZIP e o EXE experimental pelo site do GitHub estão em docs/RELEASE-EXPERIMENTAL.md. gh.exe não é necessário para o upload manual. A Release ainda não foi publicada.

## Fontes oficiais

- Downloads, versão e instrução WinGet: https://jrsoftware.org/isdl.php
- Verificação Authenticode/attestations/ISSigTool: https://jrsoftware.org/isdl-verify.php
- Licença: https://jrsoftware.org/files/is/license.txt
- Licenças comerciais e projeto pré-produção: https://jrsoftware.org/isorder.php
- Compilador ISCC e códigos de saída: https://jrsoftware.org/ishelp/topic_compilercmdline.htm
- Instalação não administrativa: https://jrsoftware.org/ishelp/topic_admininstallmode.htm
- Privilégios requeridos: https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm
