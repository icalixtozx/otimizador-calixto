# Instalador experimental

CalixtoOptimizer.iss empacota a publicação autocontida Windows x64 0.5.0-experimental de artifacts/release-v0.5.0-experimental/CalixtoOptimizer.

## Estado do compilador

O Inno Setup não está instalado neste ambiente: ISCC.exe não apareceu no PATH nem nos diretórios padrão de instalação 6/7. Portanto, o script ainda não foi compilado e o EXE do instalador não foi produzido. Instalação e desinstalação permanecem não testadas.

## Inno Setup verificado em 2026-10-19

A página de downloads do fornecedor lista como versão estável mais recente Inno Setup 7.1.0 x64, publicado em 2026-08-12. O comando interativo anunciado pelo próprio fornecedor usa o ID exato JRSoftware.InnoSetup.7. winget show --id JRSoftware.InnoSetup.7 -e -s winget retornou versão 7.1.0, licença “Inno Setup License”, fornecedor jrsoftware.org e instalador do GitHub oficial; a atualização da fonte WinGet falhou nesta consulta, mas o manifesto local disponível foi retornado e corresponde à listagem oficial do fornecedor.

O fornecedor orienta validar Authenticode no Windows: assinatura válida e publicador Pyrsys B.V. A página oficial também documenta attestations da Release GitHub e a forma gh release verify-asset. Não executei instalador de Inno nem verifiquei arquivo baixado porque ele ainda não está instalado/baixado.

O texto da Inno Setup License permite uso e redistribuição, inclusive comercial, sob quatro condições: preservar avisos/condições ao redistribuir fonte; manter avisos e endereços no binário; não alegar autoria do Inno Setup; e marcar versões modificadas. Separadamente, o fornecedor pede que usuários comerciais comprem uma licença comercial; a página de licenças diz que não espera compra antes de o instalador estar pronto para produção. Segundo essa orientação, não se espera compra para este teste experimental pré-produção.

## Uma ação para disponibilizar o compilador

Após sua confirmação, execute no PowerShell:

    winget install --id JRSoftware.InnoSetup.7 -e -s winget -i

Esse comando é interativo. Não o executei, pois instalar a ferramenta depende da sua confirmação.

## Compilar o instalador depois da instalação

Na raiz do repositório, com a publicação atualizada:

    dotnet publish .\src\Optimizer.App\Optimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\release-v0.5.0-experimental\CalixtoOptimizer
    ISCC.exe .\packaging\CalixtoOptimizer.iss

O EXE esperado é artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe. A documentação oficial confirma ISCC.exe <script.iss> e usa código de saída zero para sucesso; depois é preciso conferir arquivo, tamanho e SHA-256. Compilar o .iss não é teste de instalação. Não declarar instalação/desinstalação verificadas até executar ambas em Windows de teste.

## Privilégios do instalador

- PrivilegesRequired=lowest instala por usuário em %LOCALAPPDATA%\Programs\CalixtoOptimizer.
- O instalador não inicia o app nem cria serviço, tarefa privilegiada ou worker.
- A pasta do app é gravável pelo usuário. Um futuro worker elevado não pode ser colocado ali; exigiria localização protegida, ACL adequada e protocolo autenticado.
- Histórico permanece em %LOCALAPPDATA%\CalixtoOptimizer\History; o desinstalador não o apaga.

Fontes oficiais:

- [Downloads, última versão e instruções WinGet](https://jrsoftware.org/isdl.php)
- [Verificação de autenticidade e assinaturas](https://jrsoftware.org/isdl-verify.php)
- [Licença do Inno Setup](https://jrsoftware.org/files/is/license.txt)
- [Licenças comerciais e estágio inicial](https://jrsoftware.org/isorder.php)
- [Compilador ISCC e códigos de saída](https://jrsoftware.org/ishelp/topic_compilercmdline.htm)
- [Instalação não administrativa](https://jrsoftware.org/ishelp/topic_admininstallmode.htm)
- [Privilégios requeridos](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
