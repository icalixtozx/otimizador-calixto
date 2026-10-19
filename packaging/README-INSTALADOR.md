# Instalador experimental

O fonte `CalixtoOptimizer.iss` empacota a publicação autocontida Windows x64 de `artifacts/release-v0.3.0-experimental/CalixtoOptimizer`.

## Compilação

No PowerShell, a partir da raiz do repositório, depois de instalar Inno Setup:

```powershell
dotnet publish .\src\Optimizer.App\Optimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\release-v0.3.0-experimental\CalixtoOptimizer
ISCC.exe .\packaging\CalixtoOptimizer.iss
```

O script direciona a saída para `artifacts/installer/CalixtoOptimizer-0.3.0-experimental-win-x64-setup.exe`. O compilador oficial aceita `ISCC.exe <arquivo.iss>` e retorna código zero quando a compilação termina sem erro; consultar a documentação oficial antes de mudar opções.

## Privilégios e limites

- `PrivilegesRequired=lowest` instala sem elevação em `%LOCALAPPDATA%\Programs\CalixtoOptimizer`.
- O assistente usa as mensagens oficiais de Português Brasileiro do Inno Setup.
- O instalador não inicia o app e não instala serviço, tarefa privilegiada ou worker.
- A pasta do app é modificável pelo usuário. Um futuro worker elevado não pode ser colocado nela. Esse componente exigiria uma arquitetura protegida, com instalação/ACL adequadas e protocolo autenticado entre processos.
- O histórico permanece em `%LOCALAPPDATA%\CalixtoOptimizer\History`; a desinstalação do app não o apaga.
- A compilação do script e a instalação/desinstalação precisam ser verificadas quando `ISCC.exe` estiver disponível. Nenhum teste de instalação foi realizado nesta entrega.

## Disponibilidade e licença do Inno Setup

Na consulta de 2026-10-19, a página oficial lista Inno Setup 7.1.0 e 6.7.3. O compilador não foi encontrado no `PATH` nem nos caminhos comuns de instalação deste ambiente; portanto, o EXE de setup ainda não foi produzido.

O projeto Inno Setup é open source. O fornecedor pede que usuários comerciais comprem uma licença; a própria FAQ diz que quem ainda está em etapa inicial não precisa comprar antes de o instalador estar pronto para produção. O uso futuro comercial deve seguir a licença vigente do fornecedor. Esta nota não constitui análise jurídica.

Fontes oficiais:

- [Downloads do Inno Setup](https://jrsoftware.org/isdl.php)
- [Licenças comerciais e uso em etapa inicial](https://jrsoftware.org/isorder.php)
- [Compilador de linha de comando ISCC](https://jrsoftware.org/ishelp/topic_compilercmdline.htm)
- [Instalação não administrativa](https://jrsoftware.org/ishelp/topic_admininstallmode.htm)
- [Privilégios requeridos](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm)
