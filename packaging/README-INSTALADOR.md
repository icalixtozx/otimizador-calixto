# Distribuição experimental

## Preview autocontido 0.6.0 experimental — build 20261020-r2

Origem: commit `bb3c5eb60c310cbaaa18d20f370b67c7da10d81a`. Publicação Windows x64 autocontida, 406 arquivos; o pacote inclui o runtime .NET. O ZIP e o instalador foram produzidos da mesma pasta `dotnet publish`.

| Artefato | Caminho no repositório | Bytes | SHA-256 |
|---|---|---:|---|
| ZIP portátil | `artifacts/preview-v0.6.0-experimental-selfcontained-20261020-r2/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-portable.zip` | 65.599.195 | `20FAD64165F5166F5A7FDFB46BF30724ACBF2EE9BE649EEF07F10DA63031EF0D` |
| Instalador EXE | `artifacts/installer-preview-20261020-r2/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r2-win-x64-setup.exe` | 46.799.063 | `ADE4D74A0C7E75E86BE70C9E722FCE758D0C01EABC73CE99911577DCF95966DD` |

O instalador foi compilado pelo Inno Setup 7.1.0. O upgrade da instalação Preview existente passou com código 0. Os hashes dos 406 arquivos instalados coincidiram com a publicação; os dois arquivos de histórico preservaram nome, tamanho e SHA-256. O atalho do Menu Iniciar aponta para `%LOCALAPPDATA%\Programs\CalixtoOptimizer-Preview-20261019\Optimizer.App.exe`. O app abriu na instalação atualizada. Não foram testadas instalação limpa nem desinstalação do R2, outro PC ou drivers. O Setup não tem assinatura digital própria. A primeira compilação Preview de 20/10 foi preservada e marcada como supersedida após a validação visual detectar o defeito da página Registro; ela não é o pacote indicado para distribuição.

## Histórico de distribuição 0.5.0

Os tamanhos, hashes e testes a seguir descrevem os artefatos 0.5.0 já produzidos, correspondentes à tag `v0.5.0-experimental`. O script `packaging/CalixtoOptimizer.iss` continua sendo a referência histórica do instalador 0.5.

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

## Preview R3 — 2026-10-20

Build de origem: `6839b64e4c79ca0d730632c718467f1a2e8651da`. Identificação: `PREVIEW R3 · v0.6.0 experimental`; o app mostra o commit curto e seu hash completo no tooltip. O assembly publicado contém `0.6.0-experimental-preview-20261020-r3+6839b64e4c79ca0d730632c718467f1a2e8651da`.

| Artefato | Caminho no repositório | Bytes | SHA-256 |
|---|---|---:|---|
| ZIP portátil | `artifacts/preview-v0.6.0-experimental-selfcontained-20261020-r3/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r3-win-x64-portable.zip` | 65.715.392 | `0B58EBA03E55E896F04759A654ECF7BAA7372054E0A2C1A1B84AF574FEE4CC0F` |
| Instalador EXE | `artifacts/installer-preview-20261020-r3/CalixtoOptimizer-Preview-0.6.0-experimental-20261020-r3-win-x64-setup.exe` | 46.887.177 | `A965CBEF13BCE5C6368111C2092D36F08AB4DF40C9B51C502A86228CA305F0F0` |

Build Release .NET SDK 10.0.402: 0 avisos/erros; harness 127/127 simulado. A publicação autocontida tem 406 arquivos. Inno Setup 7.1.0 compilou o script com sucesso. App e instalador não foram executados; instalação, atualização, remoção e validação visual seguem pendentes. O AppId e destino são exclusivos R3, separados do R2. Assinatura do Setup: `NotSigned`.

O ciclo real AC autorizado anteriormente aparece no histórico lido pela mesma view model da tela Energia como `PREFERÊNCIAS CAPTURADAS RESTAURADAS POR LEITURA`. A interface ainda precisa de conferência visual. Para abrir sem instalar runtime, execute o `Optimizer.App.exe` na pasta R3 publicada indicada no checklist da entrega. Não use a Preview R2 nem o executável framework-dependent em `src/Optimizer.App/bin`.
