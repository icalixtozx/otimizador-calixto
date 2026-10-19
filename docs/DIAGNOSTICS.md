# Diagnóstico local

## Planos de energia

WindowsPowerPlanInventoryService consulta os planos com as APIs PowrProf do Windows: PowerEnumerate enumera os esquemas, PowerGetActiveScheme identifica o ativo, PowerReadFriendlyName lê o nome localizado e PowerReadACValueIndex/PowerReadDCValueIndex leem VIDEOIDLE separadamente para AC/DC. A tela não mostra o GUID.

A interface marca o plano ativo e apresenta os valores que o Windows devolveu, sem modificar o esquema. Para comparação manual, use powercfg /list, powercfg /getactivescheme e powercfg /query SCHEME_CURRENT SUB_VIDEO VIDEOIDLE. Esses comandos são somente leitura. Valores ausentes e erros são mostrados como indisponíveis/parciais.

APIs oficiais: [PowerEnumerate](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerenumerate), [PowerGetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powergetactivescheme), [PowerReadFriendlyName](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadfriendlyname), [PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex) e [PowerReadDCValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreaddcvalueindex).

## Dispositivos e drivers

A consulta combina Win32_PnPSignedDriver (metadados/assinatura de driver) com Win32_PnPEntity (nome e ConfigManagerErrorCode reportado pelo Windows), cruzando os registros por ID PnP apenas em memória. Linhas sem metadados correspondentes são apresentadas como “Detalhes do driver não retornados”; isso não prova que o driver esteja ausente.

O código numérico é apresentado como “PnP N reportado pelo Windows”. O aplicativo não atribui automaticamente significado causal ao código, nem assume que qualquer erro signifique ausência de driver. A documentação Microsoft descreve vários significados possíveis de ConfigManagerErrorCode; interpretações e recuperação precisam considerar o dispositivo e o contexto. Versão/data não é comparada com uma fonte externa, portanto não há classificação “desatualizado”.

Fontes: [Win32_PnPEntity](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpentity) e [Win32_PnPSignedDriver](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver).

## Exportação JSON

O botão “Revisar diagnóstico” abre uma prévia antes do diálogo de gravação. O usuário escolhe se e onde salvar; nada é enviado automaticamente. O documento omite nome da conta/computador, ID PnP, GUID de plano, serial, endereço de rede e caminhos locais. Inclui campos técnicos como edição/build do Windows, modelos de hardware, metadados de driver, códigos PnP e valores VIDEOIDLE para permitir uma revisão útil.

Nomes/modelos de hardware ainda podem identificar a configuração de alguém. A prévia precisa ser revisada antes de compartilhar. Erros e disponibilidade parcial são mantidos no documento para não transformar dados incompletos em diagnóstico positivo.
