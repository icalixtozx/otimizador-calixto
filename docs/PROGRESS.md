# Progresso

## Fase 0 — ambiente e decisões

- [x] Criada a solução WPF em .NET 10 LTS e estrutura modular.
- [x] Diagnóstico local identificou CPU, GPU, placa-mãe, arquitetura, runtime e ferramentas.
- [x] SDK .NET 10.0.402 instalado de forma local em `work/dotnet-sdk-10` para esta validação; a instalação global do usuário não foi alterada.
- [x] Solução compilada em Release com 0 avisos e 0 erros.
- [ ] Resolver divergência nos metadados de Windows: “Windows 10 Pro”, 26H2, build 26300.
- [x] Janela WPF aberta; o processo exibiu o título `Calixto Optimizer`.
- [ ] Revisar a apresentação visualmente e confirmar suporte do build do Windows.

## Fase 1 — inventário somente leitura

- [x] Modelos para drivers e inventário do computador.
- [x] Adaptadores WMI para drivers assinados, códigos Plug and Play e hardware.
- [x] Busca, filtro de assinatura e CSV sanitizado.
- [x] UI em português com estados de carregamento, erro, vazio, parcial e lista.
- [x] Verificações do modelo de busca e exportação.
- [x] Restaurar pacote e compilar todos os projetos em Release.
- [x] Executar verificações do modelo: 4/4 aprovadas.
- [ ] Confirmar saída real no computador do usuário.

## Próxima etapa

Revisar a janela aberta, confirmar no próprio Windows que os nomes de hardware e drivers aparecem corretamente e compartilhar apenas erros/resultados sanitizados. Não iniciar a fase 2 até a coleta real ser observada.

## Estado honesto

O build e as verificações do modelo foram aprovados usando um SDK temporário dentro de `work`. `Get-CimInstance` foi bloqueado pelo ambiente, portanto a saída real do inventário ainda não foi lida por esta sessão. Os metadados de Windows continuam divergentes e precisam ser confirmados. Nenhum instalador ou ajuste do Windows foi produzido.
