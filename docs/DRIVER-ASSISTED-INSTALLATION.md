# Instalação assistida de drivers — investigação e proposta

## Estado atual — pacotes candidatos revisados em 20/10/2026

Foram baixados somente dois arquivos ZIP da página oficial da placa ASUS PRIME B550M-A AC. Os ZIPs e extrações ficam em `artifacts/driver-review-20261020/`, pasta ignorada pelo Git; não fazem parte do app nem do commit. Nenhum `setup.exe` interno foi executado. Nenhum driver foi instalado, removido ou substituído e nenhuma configuração do Windows foi alterada.

## Evidência coletada neste computador

- `Win32_ComputerSystem.Manufacturer`: ASUS. `Model`: `System Product Name`, genérico e insuficiente para selecionar software OEM.
- `Win32_BaseBoard`: `ASUSTeK COMPUTER INC. PRIME B550M-A AC`. Esse identificador de modelo é específico o bastante para abrir a página de suporte correta da placa-mãe; ele não confirma o pacote apropriado para cada função opcional.
- A consulta antiga do app `Win32_NetworkAdapter WHERE PhysicalAdapter=True` também retornou `Kaspersky VPN` neste host, embora a instância PnP seja `ROOT\KLTUN`. Essa propriedade WMI, isoladamente, não é um filtro confiável para escolher hardware físico; o módulo futuro precisa cruzar classe/parent bus e IDs PnP.
- Adaptador Ethernet físico: `Realtek PCIe GbE Family Controller`; Hardware ID PnP inclui `PCI\VEN_10EC&DEV_8168&SUBSYS_86771043&REV_15`. A leitura WMI real do app retornou status de inventário disponível, provider `Microsoft`, INF `rtcx21x64.inf`, versão `1.0.0.14`.
- Adaptador Wi-Fi físico: `Realtek 8821CE Wireless LAN 802.11ac PCI-E NIC`; Hardware ID PnP inclui `PCI\VEN_10EC&DEV_C821&SUBSYS_304A1A3B`. A leitura WMI real do app retornou provider `Microsoft`, INF `netrtwlane.inf`, versão `2024.0.8.108`.
- Wi-Fi Direct, VPN, depuração de kernel e Bluetooth PAN são interfaces virtuais ou funções auxiliares e não foram tratados como placas físicas para selecionar um pacote de rede.
- O manual oficial da PRIME B550M-A AC especifica Ethernet Realtek RTL8111H e módulo Realtek RTL8821CE. Isso concorda com os nomes observados e com a página OEM do modelo.

Os IDs acima são hardware IDs sem o sufixo de instância local. IDs devem ser comparados como strings opacas, na ordem fornecida pelo Plug and Play; não são nomes legíveis nem devem ser editados para adivinhar um produto.

## Pacotes oficiais e validação real dos arquivos

Fonte: [suporte oficial ASUS da PRIME B550M-A AC](https://www.asus.com/us/motherboards-components/motherboards/prime/prime-b550m-a-ac/helpdesk_download?model2Name=PRIME-B550M-A-AC), com Windows 11 64-bit selecionado. O [manual ASUS da placa](https://dlcdnets.asus.com/pub/ASUS/mb/SocketAM4/PRIME_B550M-A_AC/E16965_PRIME_B550M-A_AC_UM_WEB.pdf) especifica Ethernet RTL8111H e Wi-Fi RTL8821CE.

| Função | Pacote listado pela ASUS | ZIP local e integridade | Correspondência e SO |
| --- | --- | --- | --- |
| Ethernet | Realtek RTL8111H LAN `1168.27.50.919`; publicado em 31/12/2025; Windows 11 64-bit. [Download ASUS](https://dlcdnets.asus.com/pub/ASUS/mb/04LAN/DRV_LAN_Realtek_8111_SZ-TSD_W11_64_V11682750919_20251230R.zip). | `ASUS-RTL8111H-LAN-1168.27.50.919.zip`, 15.387.705 bytes. SHA-256 local `3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D`, idêntico ao SHA-256 que a ASUS publica. 722 entradas; todos os CRC32 e comprimentos descompactados conferem. | `rt68cx21x64.inf` contém o Hardware ID completo ASUS `PCI\VEN_10EC&DEV_8168&SUBSYS_86771043&REV_15`; seção `NTamd64.10.0...22000`; Windows 11 x64 build 26300.9711 do host excede o mínimo 22000. Correspondência exata. |
| Wi-Fi | Realtek RTL8821CE WiFi `2024.10.138.3`; publicado em 04/10/2022; Windows 10/11 64-bit. [Download ASUS](https://dlcdnets.asus.com/pub/ASUS/mb/08WIRELESS/DRV_WiFi_RTK_8821CE_SZ-TSD_W11_64_V2024101383_20220929R.zip). | `ASUS-RTL8821CE-WiFi-2024.10.138.3.zip`, 18.051.928 bytes. SHA-256 local `E061EBD3E77F54DA07ECED9876DB54DCAA46E871DB4015FEE6EC1F2987535647`; a ASUS não publica um hash para esse ZIP. 37 entradas; todos os CRC32 e comprimentos descompactados conferem. | `netrtwlane.inf` contém o Hardware ID `PCI\VEN_10EC&DEV_C821&SUBSYS_304A1A3B`; seção x64 `NTamd64.10.0`; Windows 10/11 64-bit. Correspondência exata. |

Para cada catálogo `.cat` incluído nos dois ZIPs, `Get-AuthenticodeSignature` reportou `Valid`; o signatário é `Microsoft Windows Hardware Compatibility Publisher`. Também validei o INF alvo de cada candidato como membro do seu catálogo por `WinVerifyTrust` com `DRIVER_ACTION_VERIFY`, usando as APIs documentadas de catálogo do Windows; retorno `0x00000000` nos dois casos. A ASUS não publicou hash para o pacote Wi-Fi; o hash calculado localmente reconhece a cópia baixada e não é prova independente da origem.

Resultado da leitura WMI real pelo `WmiDriverInventoryService` depois da integração: 183 linhas de dispositivo, disponibilidade `Available`, zero avisos. Os dois candidatos corresponderam a um único dispositivo cada por Hardware ID exato. O driver instalado reportado pelo Windows permanece Microsoft `1.0.0.14`/`rtcx21x64.inf` para Ethernet e Microsoft `2024.0.8.108`/`netrtwlane.inf` para Wi-Fi. Não inferi que os candidatos sejam versões superiores, necessários ou melhores; os formatos de versão e as funções não foram comparados funcionalmente.

O catálogo da página ASUS também tem um pacote Wi-Fi genérico `2024.10.141.0`, mas sua descrição não nomeia RTL8821CE e seus arquivos não foram baixados nem analisados. Ele não é apresentado como compatível. A data do candidato específico `2024.10.138.3` também não classifica o driver instalado como desatualizado.

Não recomendo instalar todos os itens da página da placa. Chipset, áudio, vídeo, RAID, utilitários e Bluetooth exigem identificação do componente presente, dependência funcional e pacote explicitamente aplicável. Os dois adaptadores de rede são os únicos candidatos deste levantamento. Para dispositivos sem candidato suficientemente confirmado, o app deve oferecer o link OEM oficial e apresentar “compatibilidade não confirmada”.

## Estratégia de correspondência proposta

1. Consultar sem escrita: produto/edição/build/arquitetura do Windows; hardware IDs e compatible IDs em ordem; fabricante/modelo da placa ou OEM; dispositivo presente, status PnP, provider, INF e versão do driver ativo. Não usar apenas nome amigável ou código de problema. Os IDs são mantidos apenas em memória e não entram no CSV/relatório sanitizado.
2. Procurar primeiro na página oficial do OEM do computador/placa, escolhendo o produto exato e o Windows/arquitetura suportados. Para placa ASUS montada, usar a página da placa. Usar a página oficial do fabricante do chip como segunda fonte somente se o OEM não fornecer pacote adequado; avisar que drivers genéricos podem perder personalizações do OEM.
3. Preservar URL, data, versão, sistema-alvo, tamanho e hash oficial quando publicado. Conferir integridade do ZIP, assinatura Authenticode/catálogo do pacote e pertencimento do INF ao catálogo. Uma assinatura válida não comprova compatibilidade com todo o hardware nem necessidade de atualização.
4. Extrair um pacote apenas para uma pasta de análise isolada. Comparar seus INF assinados com os hardware/compatible IDs exatos e as decorações de OS/arquitetura. Correspondência do hardware ID específico tem prioridade; compatible ID genérico reduz a confiança e requer revisão explícita. A seleção real do Windows também leva em conta rank, assinatura e aplicabilidade do OS.
5. Se o pacote for um instalador OEM opaco, sem INF verificável ou metadados suficientes, não automatizar a execução; oferecer a página oficial para revisão manual. Mostrar fabricante, dispositivo, IDs correspondentes, edição/build/arquitetura alvo, versão publicada, assinatura/hash e consequências antes de pedir aprovação.
6. Uma futura instalação deve exigir confirmação específica por dispositivo e elevação segura, usar somente uma ação de instalação documentada pelo fabricante/Windows, reler o driver ativo e o estado PnP depois e registrar falhas/reinicialização necessária. Reversão precisa identificar e preservar o pacote anteriormente ativo; não presumir que remover um pacote restaura o anterior.

## Integração atual da página Drivers e uso offline

- **Inventário instalado:** continua uma consulta WMI local; não precisa de Internet. A página exibe provider, versão, INF e assinatura reportados pelo Windows sem afirmar que uma versão está atualizada.
- **Candidatos oficiais:** o botão “Candidatos oficiais” apresenta os dois candidatos ASUS revisados, status da correspondência de IDs, dados do pacote instalado, origem, resultado da verificação do arquivo e pendências. IDs completos não são mostrados nem exportados.
- **Pacote local:** a implementação desta etapa acrescenta análise genérica de ZIP/INF. ZIPs desconhecidos deixam de depender de reconhecimento por hash; o digest conhecido permanece apenas como fingerprint de origem já revisada.
- O hash de origem cobre o ZIP inteiro quando a entrada é `.zip`; para `.inf`, cobre só o INF selecionado. Catálogo e payloads necessários recebem hashes individuais, usados para detectar mudança entre análise/captura e uma possível chamada futura.
- A Preview R2 anterior precede o parser genérico. Os resultados específicos de validação visual daquela versão não significam que a interface desta alteração tenha sido aberta ou validada visualmente.

Referências técnicas: [hardware IDs e compatible IDs](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/device-identification-strings); [seleção e rank dos pacotes](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process); [seção Models do INF](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-models-section); [WINTRUST_CATALOG_INFO](https://learn.microsoft.com/en-us/windows/win32/api/Wintrust/ns-wintrust-wintrust_catalog_info); [SignTool e verificação de INF em catálogo](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/signtool); [WinVerifyTrust e DRIVER_ACTION_VERIFY](https://learn.microsoft.com/pt-br/windows/win32/api/wintrust/nf-wintrust-winverifytrust). A validação real dos pacotes está limitada aos arquivos e ao host descritos acima; fontes técnicas não substituem teste funcional.

## Inspeção visual e importação local — 2026-10-20

Na Preview R2, a página exibiu o pacote instalado reportado pelo Windows, os candidatos ASUS, a origem oficial, versão/alvo de Windows, correspondência por Hardware ID, integridade ZIP, assinatura/catálogo, INF e pendências. O inventário local retornou 183 linhas; a revisão encontrou um dispositivo Ethernet e um Wi-Fi por Hardware ID exato. O Windows reporta os pacotes instalados como Microsoft; isso não demonstra que a troca seja necessária ou superior.

O fluxo **Conferir ZIP local (somente leitura)** da Preview R2 anterior foi exercitado com os dois ZIPs conhecidos. O app calculou SHA-256 e reconheceu cada arquivo apenas por igualdade byte a byte com o digest revisado. Nenhum conteúdo foi extraído ou executado naquela compilação. A análise genérica descrita abaixo foi implementada depois, ainda sem inspeção visual desta versão. Nenhum driver foi instalado, substituído ou removido.

## Análise genérica e preparação assistida bloqueada — 2026-10-20

### Entrada, isolamento e resultado

- A página Drivers aceita somente um `.inf` ou `.zip`. Nunca inicia `setup.exe`, MSI, script ou outro conteúdo do pacote.
- ZIP: até 128 MiB comprimidos, 512 MiB descompactados, 128 MiB por arquivo e 4096 entradas. Rejeita caminhos absolutos, `..`, caminhos duplicados, links simbólicos e extração fora de uma pasta temporária própria sob `%TEMP%\CalixtoOptimizer\DriverAnalysis\<GUID>`. Cada entrada tem tamanho e CRC32 conferidos. A análise libera apenas a sua pasta temporária ao encerrar/descartar o resultado.
- INF local: lê a pasta do INF sem extrair nem alterar arquivos, omitindo links/reparse points e aplicando os mesmos limites de contagem/tamanho. INF auxiliar como `Autorun.inf` sem `[Version]` e `[Manufacturer]` não é apresentado como candidato PnP. INF malformado permanece visível como incompleto.
- O parser conservador resolve modelos e decorações de arquitetura/Windows e cruza hardware/compatible IDs do inventário atual. Só um Hardware ID exato, para um único dispositivo presente, pode receber prévia. Compatible ID apenas, mais de um alvo ou parse não suportado bloqueiam a prévia. A data/versão do pacote não é usada como prova de atualização ou desempenho.
- O INF é verificado no catálogo com `SetupVerifyInfFileW`. Arquivos enumerados em `SourceDisksFiles` são localizados sem ambiguidade, verificados como membros do catálogo via APIs WinTrust e recebem SHA-256 para conferência posterior. O catalog verifier atual cobre esses payloads enumerados; diretivas complexas ou parametrizadas não resolvidas resultam em **análise incompleta**, e assinatura válida nunca significa compatibilidade funcional.
- Cada candidato aparece como **Compatível segundo as verificações realizadas**, **Incompatível**, **Ambíguo** ou **Análise incompleta**, com motivos. “Compatível” não é promessa de que o Windows selecionará o pacote nem que o dispositivo funcionará depois.

### Serviço e estados distintos

- A prévia identifica o dispositivo, driver ativo conhecido, INF e versão declarados pelo candidato, hash, permissões administrativas, chance de perda temporária de rede/reinício e limites de recuperação. O usuário pode consentir às consequências e persistir captura somente leitura no diário compartilhado. Isso não autoriza instalação.
- `PnpUtilDriverPackageInstaller` usa PnPUtil documentado, com dois métodos distintos: `pnputil /add-driver <inf>` para pedir staging no Driver Store e `/add-driver <inf> /install` para pedir instalação/atualização em dispositivos correspondentes. Nunca usa `/reboot`, `/delete-driver`, `/uninstall`, `/force`, executáveis OEM ou downgrade forçado; saída localizada não é interpretada. A API requer que o processo já esteja elevado; não há worker elevado nem prompt UAC.
- A composição de produção usa `BlockedDriverPackageMutationPolicy`: o caminho real termina como **bloqueado antes de chamar PnPUtil**. Só há teste de protocolo com instalador falso. Staging reportado como sucesso não prova associação. Após a etapa de associação, o serviço relê o inventário e só reporta INF ativo no mesmo identificador de dispositivo; nem isso comprova conectividade ou funcionamento funcional.
- Limitação importante: PnPUtil `/install` age sobre **qualquer dispositivo correspondente**, não recebe o instance ID exato. A análise atual requer um alvo único pelo inventário e revalida imediatamente antes, mas mudanças concorrentes/hot-plug ainda são um limite. Antes de desbloquear associação real, será necessário restringir a API a um único devnode ou apresentar e aprovar esse escopo por dispositivo. Instalação real não está habilitada nem autorizada nesta entrega.
- Falhas após início de uma chamada podem deixar o Driver Store ou o dispositivo em estado parcial; não há remoção automática nem rollback do pacote. Preservar nome/versão do INF anterior não garante restaurá-lo. O procedimento seguro futuro começa por backup/exportação do pacote existente e plano de recuperação do OEM ou execução em VM/dispositivo sobressalente.

### Resultado real dos dois ZIPs ASUS com o analisador desta entrega

Probe temporário somente leitura usou `WmiDriverInventoryService`, `WindowsDriverPackageSignatureVerifier` e `LocalDriverPackageAnalyzer`. Inventário `Available`, 193 linhas, Windows `10.0.26300.0`, arquitetura `amd64`. Os arquivos continuam fora do Git em `artifacts/driver-review-20261020/`.

| Pacote | Resultado por INF | Evidência coletada pelo analisador | Consequência |
| --- | --- | --- | --- |
| LAN RTL8111H `1168.27.50.919` | `rt68cx21x64.inf`: **Compatível segundo as verificações realizadas**. | ZIP 722 entradas / 34.802.917 bytes; um Hardware ID exato; `amd64` e decoração `NTamd64.10.0...22000`; catálogo/INF válido pelo Windows, signer Microsoft Windows Hardware Compatibility Publisher; único arquivo obrigatório localizado e membro do catálogo. SHA-256 ZIP coincide com o publicado pela ASUS. | Pode criar uma prévia de revisão para o alvo atual. Não é recomendação de atualização, não garante seleção pelo rank do Windows e não foi instalado/testado funcionalmente. |
| Wi-Fi RTL8821CE `2024.10.138.3` | `netrtwlane.inf`: **Análise incompleta**. `Autorun.inf` auxiliar foi corretamente ignorado. | ZIP 37 entradas / 31.111.699 bytes; Hardware ID exato, assinatura/membership INF válido; INF declara `rtlCoInst.dll` e `rtlCoInst.dat` em `[SourceDisksFiles]`, mas ambos não existem no ZIP. Hash ZIP é local, pois ASUS não publica digest. | Bloqueado. Não pode receber prévia ou instalação até obter pacote completo e validar novamente. A assinatura do catálogo não compensa arquivos necessários ausentes. |

Nenhum setup foi iniciado; PnPUtil não foi chamado; nenhum driver foi staged, associado, removido ou substituído. Os hashes acima são os arquivos locais já descritos na tabela anterior, não publicados como parte do aplicativo.

### Teste real futuro a submeter para aprovação específica

O primeiro candidato elegível para uma eventual experiência é o pacote Ethernet LAN acima, mas sua versão não foi comparada funcionalmente ao driver instalado nem aprovada para uso. Antes de solicitar autorização concreta, atualizar inventário e análise do pacote e apresentar hardware ID, dispositivo alvo, driver atual, INF escolhido, rank/seleção esperado (se conhecido), hash, assinaturas, efeitos e plano de recuperação. A associação por PnPUtil poderá atingir todo dispositivo correspondente; não fazer o teste a partir de sessão remota que dependa dessa Ethernet, nem em NIC necessária para recuperação.

Procedimento a submeter depois, preferencialmente em VM ou máquina sobressalente com console local e rede alternativa:

1. Confirmar um único devnode exato e reler edição/build/arquitetura, IDs e driver ativo; conferir ZIP/hash/INF/catálogo de novo e obter aprovação para aquele dispositivo e pacote específicos.
2. Exportar/guardar o pacote atualmente ativo e assegurar mídia/rede de recuperação do OEM. Persistir a consequência aceita e captura no diário; falha, conflito ou inventário parcial cancela o teste.
3. Com administrador, executar primeiro apenas staging do INF e verificar Driver Store por consulta nativa documentada. Registrar que staging não associou driver.
4. Só com uma segunda aprovação específica, pedir associação. Reconsultar o INF ativo e status/código PnP do mesmo instance ID, verificar interface e conectividade pela conexão alternativa, registrar reinício solicitado e não forçar restart.
5. Se falhar, parar para avaliação manual; não remover pacote nem restaurar automaticamente. Usar rollback/OEM planejado e confirmar driver/funcionalidade após a recuperação. Repetir só depois de aprovar plano atualizado.

Este procedimento ainda não foi executado. Instalação limpa, atualização funcional, desinstalação/recuperação e outro PC continuam pendentes.

Referências Microsoft: [PnPUtil Command Syntax](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax) (`/add-driver` stages; `/install` acts on matching devices and does not force lower-ranked packages); [Driver Package Selection Process](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process); [INF Manufacturer Section](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-manufacturer-section); [SetupVerifyInfFileW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupverifyinffilew).
