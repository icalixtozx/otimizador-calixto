# Instalador experimental

`CalixtoOptimizer.iss` empacota a publicação autocontida Windows x64 v0.4.0-experimental de `artifacts/release-v0.4.0-experimental/CalixtoOptimizer`.

## Procedimento de compilação

Na raiz do repositório, após disponibilizar o compilador Inno Setup:

```powershell
dotnet publish .\src\Optimizer.App\Optimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\release-v0.4.0-experimental\CalixtoOptimizer
ISCC.exe .\packaging\CalixtoOptimizer.iss
```

O EXE esperado será `artifacts/installer/CalixtoOptimizer-0.4.0-experimental-win-x64-setup.exe`. Confirme existência, tamanho, hash e código de saída 0 do `ISCC.exe`. Ainda será necessário testar instalação e desinstalação em uma VM/PC Windows limpa; a compilação do `.iss` não é teste de instalação.

## Disponibilizar o Inno Setup

O fornecedor oficial lista Inno Setup 7.1.0 e orienta instalação interativa por WinGet com:

```powershell
winget install --id JRSoftware.InnoSetup.7 -e -s winget -i
```

Como alternativa, baixe pelo [site oficial](https://jrsoftware.org/isdl.php). O fornecedor informa que o instalador publicado tem assinatura Authenticode; abra Propriedades → Assinaturas Digitais e confirme a assinatura válida e o publicador `Pyrsys B.V.` antes de usar. Não baixe `ISCC.exe` de sites de terceiros.

Depois da instalação, confira o caminho:

```powershell
Get-Command ISCC.exe
Test-Path "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
```

Se o diretório não estiver no `PATH`, invoque diretamente:

```powershell
& "$env:ProgramFiles\Inno Setup 7\ISCC.exe" .\packaging\CalixtoOptimizer.iss
```

A página oficial descreve `ISCC.exe <arquivo.iss>`; código 0 indica compilação bem-sucedida, 1 erro de argumento/fatal e 2 falha de compilação.

## Procedência, licença e privilégios

Inno Setup é publicado por Jordan Russell/Martijn Laan, é open source e tem código-fonte ligado pelo fornecedor ao GitHub oficial. A página do fornecedor pede licença comercial para usuários comerciais; sua FAQ diz que não é esperado comprá-la antes de o instalador estar pronto para produção. Confira e cumpra os termos comerciais vigentes antes de distribuir comercialmente. Esta nota não é parecer jurídico.

- `PrivilegesRequired=lowest` instala sem UAC em `%LOCALAPPDATA%\Programs\CalixtoOptimizer`.
- O assistente usa mensagens oficiais de Português Brasileiro.
- O instalador não inicia o app nem instala serviço, tarefa privilegiada ou worker.
- A pasta do app é gravável pelo usuário. Um futuro worker elevado não pode ser colocado ali; exigiria localização protegida, ACL adequada e protocolo autenticado.
- Histórico permanece em `%LOCALAPPDATA%\CalixtoOptimizer\History`; desinstalação não o apaga.
- No ambiente atual, `ISCC.exe` não foi encontrado. Portanto, o EXE ainda não foi produzido e o script ainda não passou pelo compilador. Instalação e desinstalação não foram testadas.

Fontes oficiais:

- [Downloads e verificação de autenticidade](https://jrsoftware.org/isdl.php)
- [Verificar downloads](https://jrsoftware.org/isdl-verify.php)
- [Licenças comerciais e estágio inicial](https://jrsoftware.org/isorder.php)
- [Compilador ISCC e códigos de saída](https://jrsoftware.org/ishelp/topic_compilercmdline.htm)
- [Instalação não administrativa](https://jrsoftware.org/ishelp/topic_admininstallmode.htm)
- [Privilégios requeridos](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
