# Distribuição experimental

Os tamanhos, hashes e testes abaixo descrevem os artefatos 0.5.0 já produzidos, correspondentes à tag `v0.5.0-experimental`. O código atual em `main` está na versão de desenvolvimento 0.6.0. `packaging/CalixtoOptimizer.iss` já aponta para a pasta de publicação 0.6; nenhum pacote 0.6 foi gerado neste ciclo.

Versão 0.5.0-experimental para Windows x64. Foi testada neste PC de desenvolvimento; não foi testada em outro computador. O instalador não tem assinatura digital própria.

## Compilador, procedência e licença

Inno Setup 7.1.0 x64 foi instalado pelo procedimento oficial autorizado. `ISCC.exe` está em `%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe`; a assinatura Authenticode do compilador foi `Valid` e o certificado identifica Pyrsys B.V. O identificador oficial do WinGet é `JRSoftware.InnoSetup.7`.

O compilador informou `Non-commercial use only`. O artefato foi produzido para este projeto experimental/pessoal. Antes de uso comercial, verifique e cumpra a licença comercial aplicável. Referências oficiais: [downloads](https://jrsoftware.org/isdl.php), [verificação de assinatura](https://jrsoftware.org/isdl-verify.php), [licença](https://jrsoftware.org/files/is/license.txt) e [licença comercial](https://jrsoftware.org/isorder.php).

## Compilação e artefatos

A publicação autocontida histórica do 0.5 foi gerada com:

```powershell
dotnet publish .\src\Optimizer.App\Optimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output .\artifacts\release-v0.5.0-experimental\CalixtoOptimizer
```

O diretório publicado contém 406 arquivos (147.082.618 bytes), incluindo o executável WPF e o runtime .NET. O script `packaging/CalixtoOptimizer.iss` foi compilado com ISCC 7.1.0. Resultado: exit 0, compilação bem-sucedida, 0 avisos e 0 erros. O script usa `x64compatible` e inclui recursivamente os 406 arquivos da publicação.

| Artefato | Local | Bytes | SHA-256 |
|---|---|---:|---|
| ZIP autocontido | `artifacts/CalixtoOptimizer-0.5.0-experimental-win-x64.zip` | 65.449.812 | `20704611C9967EBE3C47EB809C4079E2BC09227804A00759C25B1EB5C5EB916C` |
| Instalador EXE | `artifacts/installer/CalixtoOptimizer-0.5.0-experimental-win-x64-setup.exe` | 46.681.264 | `D13B0A31222EA213444E7995B761AB67590420BCC28B7DE5E1E5E2B2BECFB19B` |

O ZIP foi listado e confirmado com 406 entradas: `Optimizer.App.exe`, `.runtimeconfig.json`, `.deps.json`, `coreclr.dll`, `hostfxr.dll` e `README-PACOTE.txt` estão incluídos. A leitura do README dentro do ZIP confirmou os resultados e limites do teste. O Setup final é `NotSigned`; não o apresente como assinado pelo publicador do aplicativo.

## Testes executados

- Build Release com SDK .NET 10.0.402: passou com 0 avisos e 0 erros.
- Harness `Optimizer.Core.Tests`: 39/39 checks aprovados; casos de adaptadores e falhas usam simulação.
- Instalação real do Setup final no diretório temporário `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Test`: exit 0; 408 arquivos observados (406 arquivos do app e 2 arquivos auxiliares do desinstalador); executável, arquivos de runtime e entrada de desinstalação presentes.
- Desinstalação pelo `unins000.exe`: exit 0. A pasta, a entrada de desinstalação e o atalho do Menu Iniciar foram removidos. O aplicativo não foi aberto.
- Não houve teste em outro computador. A instalação neste PC não valida drivers, hardware, desempenho nem compatibilidade ampla.

O teste de instalação criou e removeu somente os componentes do próprio aplicativo. Nenhum ajuste de energia, Registro, política, serviço, rede, prioridade, driver ou firmware foi aplicado; VIDEOIDLE e os valores atuais foram preservados. O aplicativo continua iniciando em diagnóstico e sem alterações automáticas.

## Publicação

Nenhuma GitHub Release foi publicada. O repositório permanece privado. Para publicar manualmente como pre-release experimental pelo site, anexando somente estes dois artefatos, consulte `docs/RELEASE-EXPERIMENTAL.md`. `gh.exe` não é necessário para esse procedimento.
