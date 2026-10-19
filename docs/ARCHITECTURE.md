# Arquitetura

## Primeira fatia

- `Optimizer.Core` mantém contratos, dados e filtragem/exportação sem APIs específicas do Windows.
- `Optimizer.Windows` consulta WMI em uma tarefa fora da thread da interface. O escopo da consulta é somente leitura.
- `Optimizer.App` é WPF e abre com `asInvoker`, sem pedir UAC para inventário básico.
- `Optimizer.Core.Tests` é um executável pequeno e sem dependências externas para validar busca e exportação.

Não há worker elevado, serviço residente, servidor local, banco de dados ou rotina de inicialização. Nenhum código deste corte grava em Registro, políticas, rede, energia ou arquivos de drivers.

## Inventário

Drivers vêm de `Win32_PnPSignedDriver`. Códigos de problema são lidos separadamente de `Win32_PnPEntity` e associados em memória por `DeviceID`/`PNPDeviceID`. Se uma consulta falha, a UI mostra erro ou aviso parcial em vez de converter a falha em lista vazia.

Hardware vem de consultas de leitura ao WMI e da chave `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`. Valores que não chegam do Windows ficam desconhecidos. O app não coleta número de série, MAC ou endereços IP na primeira versão. IDs PnP são mantidos somente em memória e não entram no CSV.

## Dependências e evidência técnica

- .NET 10 LTS: [ciclo de suporte oficial](https://learn.microsoft.com/dotnet/core/releases-and-support).
- Instalação e restrições por versão do Windows: [Microsoft Learn — instalar .NET no Windows](https://learn.microsoft.com/dotnet/core/install/windows).
- WMI de assinatura de drivers: [Microsoft Learn — Win32_PnPSignedDriver](https://learn.microsoft.com/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver).
- Estado Plug and Play: [Microsoft Learn — Win32_PnPEntity](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-pnpentity).
- API de consultas WMI: [Microsoft Learn — ManagementObjectSearcher](https://learn.microsoft.com/dotnet/api/system.management.managementobjectsearcher?view=net-10.0).
- Pacote fixado: [System.Management 10.0.12 no NuGet](https://www.nuget.org/packages/System.Management/10.0.12).

## Segurança e limites

Inventário não significa diagnóstico de atualização. A versão e a data brutas são preservadas sem classificar driver como antigo ou atual. Um código de problema só é mostrado quando o Windows o retorna. `Não informado` não quer dizer que o dispositivo foi aprovado.

No futuro, qualquer alteração real requer suporte detectado, consequência visível, consentimento vinculado ao plano, captura persistida, verificação e estratégia de reversão. O perfil extremo não selecionará mudanças automaticamente. Operações administrativas ficam fora desta entrega.
