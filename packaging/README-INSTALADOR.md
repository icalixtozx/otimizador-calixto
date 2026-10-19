# Distribuição experimental

Versão do aplicativo: 0.5.0-experimental, Windows x64. Não validada em outro computador.

## Compilador e procedência

Inno Setup 7.1.0 x64 foi instalado pelo procedimento oficial autorizado. ISCC.exe foi encontrado em %LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe. A assinatura Authenticode do compilador está válida e o publicador é Pyrsys B.V. O comando oficial WinGet usa ID JRSoftware.InnoSetup.7.

O compilador informou “Non-commercial use only”. Este artefato foi criado para o projeto experimental/pessoal. Uso comercial futuro exige avaliar e cumprir a licença comercial do fornecedor. Fontes: https://jrsoftware.org/isdl.php, https://jrsoftware.org/isdl-verify.php, https://jrsoftware.org/files/is/license.txt e https://jrsoftware.org/isorder.php.

## Publicação e compilação

A publicação autocontida foi criada com:

    dotnet publish .\src\Optimizer.App\Optimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\release-v0.5.0-experimental\CalixtoOptimizer

O script foi compilado com ISCC.exe 7.1.0. Resultado final: código de saída 0, compilação bem-sucedida, zero avisos e zero erros. A compilação final processou 406 arquivos da pasta de publicação; o .iss seleciona todos por wildcard recursivo. A primeira compilação apontou o identificador x64 depreciado; o script foi corrigido para x64compatible nos dois campos conforme a [documentação oficial de arquitetura](https://jrsoftware.org/ishelp/topic_archidentifiers.htm), e a compilação final terminou sem aviso.

## Artefatos conferidos

- ZIP autocontido: artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip
- Tamanho: 65.449.730 bytes; 406 entradas.
- SHA-256 do ZIP: 586CD10AE589985FA84E8EF6C306D6CDB815B2995A2907F767FAA93567C1476A
- Setup: artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe
- Tamanho: 46.692.928 bytes.
- SHA-256 do Setup: EF1D03A6518079BE12843FD0CD2B433F747F99660B22427B6FE591EC7C7D2B9B
- O Setup foi verificado como não assinado digitalmente (NotSigned). Ele não deve ser apresentado como executável assinado pelo publicador do aplicativo.
- Conferidos no pacote: Optimizer.App.exe, Optimizer.App.runtimeconfig.json, Optimizer.App.deps.json, coreclr.dll, hostfxr.dll e README-PACOTE.txt. A publicação tem 406 arquivos; a compilação registrou 406 operações de compressão.

## O que foi e não foi testado

- Build Release: passou, 0 avisos/0 erros.
- Harness: 39/39 checks passaram; casos de adapters e falhas usam simulação.
- O ISCC compilou o instalador e confirmou os arquivos de entrada. Isso não comprova o comportamento de Setup/Uninstall em execução.
- O instalador do Calixto Optimizer não foi executado. Instalação e desinstalação não foram testadas e precisam de aprovação específica antes de testar.
- O ZIP foi criado e sua listagem, arquivos principais e SHA-256 foram conferidos.
- O aplicativo continua iniciando em diagnóstico e não executa alterações automaticamente.
- VIDEOIDLE não foi aplicado; valores atuais permanecem preservados.
- Nenhuma GitHub Release foi publicada. Para upload manual pelo site, consulte docs/RELEASE-EXPERIMENTAL.md. O repositório privado continuará visível apenas para quem tem acesso.
