# Instalação assistida de drivers — investigação e proposta

## Estado desta etapa

Esta etapa leu inventário local e consultou páginas oficiais. Nenhum pacote foi baixado, aberto, instalado, substituído ou removido. Nenhum driver foi classificado como desatualizado.

## Evidência coletada neste computador

- `Win32_ComputerSystem.Manufacturer`: ASUS. `Model`: `System Product Name`, genérico e insuficiente para selecionar software OEM.
- `Win32_BaseBoard`: `ASUSTeK COMPUTER INC. PRIME B550M-A AC`. Esse identificador de modelo é específico o bastante para abrir a página de suporte correta da placa-mãe; ele não confirma o pacote apropriado para cada função opcional.
- A consulta antiga do app `Win32_NetworkAdapter WHERE PhysicalAdapter=True` também retornou `Kaspersky VPN` neste host, embora a instância PnP seja `ROOT\KLTUN`. Essa propriedade WMI, isoladamente, não é um filtro confiável para escolher hardware físico; o módulo futuro precisa cruzar classe/parent bus e IDs PnP.
- Adaptador Ethernet físico: `Realtek PCIe GbE Family Controller`; hardware ID PnP reportado: `PCI\VEN_10EC&DEV_8168&SUBSYS_86771043`. O inventário de driver informa status `OK`, provider `Microsoft`, INF `rtcx21x64.inf`, versão `1.0.0.14`.
- Adaptador Wi-Fi físico: `Realtek 8821CE Wireless LAN 802.11ac PCI-E NIC`; hardware ID PnP reportado: `PCI\VEN_10EC&DEV_C821&SUBSYS_304A1A3B`. O inventário de driver informa status `OK`, provider `Microsoft`, INF `netrtwlane.inf`, versão `2024.0.8.108`.
- Wi-Fi Direct, VPN, depuração de kernel e Bluetooth PAN são interfaces virtuais ou funções auxiliares e não foram tratados como placas físicas para selecionar um pacote de rede.
- O manual oficial da PRIME B550M-A AC especifica Ethernet Realtek RTL8111H e módulo Realtek RTL8821CE. Isso concorda com os nomes observados e com a página OEM do modelo.

Os IDs acima são hardware IDs sem o sufixo de instância local. IDs devem ser comparados como strings opacas, na ordem fornecida pelo Plug and Play; não são nomes legíveis nem devem ser editados para adivinhar um produto.

## Candidatos para revisão posterior

Fonte principal: [suporte oficial ASUS da PRIME B550M-A AC](https://www.asus.com/us/supportonly/prime%20b550m-a%20ac/helpdesk_download/), selecionando Windows 11 64-bit. Manual: [especificações oficiais da placa](https://dlcdnets.asus.com/pub/ASUS/mb/SocketAM4/PRIME_B550M-A_AC/E16965_PRIME_B550M-A_AC_UM_WEB.pdf).

| Função | Evidência deste PC e do OEM | Pacote exibido pela ASUS | Estado da compatibilidade |
| --- | --- | --- | --- |
| Ethernet | Hardware ID da Realtek `DEV_8168`; manual da placa especifica RTL8111H. | Realtek RTL8111H LAN driver `1168.27.50.919`, Windows 11 64-bit, listado em 2025-12-31; SHA-256 publicado: `3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D`. | Candidato OEM por modelo/chip e sistema. Ainda falta baixar somente após aprovação, conferir assinatura, arquitetura/OS no INF e se o INF contém um hardware ID compatível. Não instalar nesta etapa. |
| Wi-Fi | Hardware ID da Realtek `DEV_C821`; manual da placa especifica módulo RTL8821CE. | A página lista expressamente Realtek RTL8821CE WiFi driver `2024.10.138.3`, Windows 10/11 64-bit, listado em 2022-10-04. | Candidato OEM por modelo/chip e sistema. O pacote está desatualizado no catálogo ASUS em termos de data do próprio catálogo, mas isso não prova que o driver instalado esteja desatualizado nem que a atualização seja necessária. Conferir assinatura e INF antes de qualquer instalação. |

O Windows reporta os dois dispositivos como `OK`. As versões instaladas (`1.0.0.14` e `2024.0.8.108`) usam esquemas que não foram demonstrados como comparáveis às versões publicadas pela ASUS. A data do pacote, sozinha, não é critério de atualização. A igualdade exata dos hardware IDs com os INF dos candidatos ainda não foi verificada, pois os pacotes não foram baixados.

Não recomendo instalar todos os itens da página da placa. Chipset, áudio, vídeo, RAID, utilitários e Bluetooth exigem identificação do componente presente, dependência funcional e pacote explicitamente aplicável. Os dois adaptadores de rede são os únicos candidatos deste levantamento. Para dispositivos sem candidato suficientemente confirmado, o app deve oferecer o link OEM oficial e apresentar “compatibilidade não confirmada”.

## Estratégia de correspondência proposta

1. Consultar sem escrita: produto/edição/build/arquitetura do Windows; hardware IDs e compatible IDs em ordem; fabricante/modelo da placa ou OEM; dispositivo presente, status PnP, provider, INF e versão do driver ativo. Não usar apenas nome amigável ou código de problema.
2. Procurar primeiro na página oficial do OEM do computador/placa, escolhendo o produto exato e o Windows/arquitetura suportados. Para placa ASUS montada, usar a página da placa. Usar a página oficial do fabricante do chip como segunda fonte somente se o OEM não fornecer pacote adequado; avisar que drivers genéricos podem perder personalizações do OEM.
3. Após aprovação do usuário para consulta/download, preservar URL, data, versão, sistema-alvo, tamanho e hash oficial quando publicado. Validar assinatura Authenticode/catálogo do pacote e a assinatura do INF antes de considerá-lo instalável.
4. Extrair um pacote apenas para uma pasta de análise isolada. Comparar seus INF assinados com os hardware/compatible IDs exatos e as decorações de OS/arquitetura. Correspondência do hardware ID específico tem prioridade; compatible ID genérico reduz a confiança e requer revisão explícita. A seleção real do Windows também leva em conta rank, assinatura e aplicabilidade do OS.
5. Se o pacote for um instalador OEM opaco, sem INF verificável ou metadados suficientes, não automatizar a execução; oferecer a página oficial para revisão manual. Mostrar fabricante, dispositivo, IDs correspondentes, edição/build/arquitetura alvo, versão publicada, assinatura/hash e consequências antes de pedir aprovação.
6. Uma futura instalação deve exigir confirmação específica por dispositivo e elevação segura, usar somente uma ação de instalação documentada pelo fabricante/Windows, reler o driver ativo e o estado PnP depois e registrar falhas/reinicialização necessária. Reversão precisa identificar e preservar o pacote anteriormente ativo; não presumir que remover um pacote restaura o anterior.

## Fluxo offline proposto

- A identificação local de modelo, OS e IDs PnP funciona sem Internet; pesquisa/download não.
- Em dispositivo online, mostrar link oficial e permitir baixar o pacote selecionado para cache de usuário. Em máquina offline, permitir apontar para uma pasta/arquivo trazido de outro computador e preservar origem declarada, hash e assinatura.
- Compatibilidade offline só é “confirmada pelo pacote” quando o conteúdo verificável inclui INF/catálogo assinado, arquitetura/OS aplicável e correspondência de hardware ID. Um `setup.exe` que não exponha esses dados permanece “pacote do fabricante para revisão”; abrir ou executar exige autorização separada.
- O app não deve baixar de agregadores, procurar drivers por nome em sites genéricos, inferir compatibilidade por data ou instalar automaticamente todos os pacotes de uma página.

Referências Microsoft para implementação futura: [hardware IDs e compatible IDs são comparados como strings opacas](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/device-identification-strings); [regras de seleção e rank de pacotes](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process); [documentação da seção Models dos INF](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-models-section). Essas fontes descrevem o mecanismo de correspondência do Windows, mas não validam por si só os pacotes ASUS candidatos.
