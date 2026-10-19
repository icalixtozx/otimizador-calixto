Calixto Optimizer 0.3.0 experimental
Windows x64. Nao validado em outro computador.

O aplicativo inicia em diagnostico. Nenhuma alteracao do Windows e aplicada automaticamente.
Catalogo de ajustes reais validado: vazio. A demonstracao opcional usa somente dados sinteticos.
O historico e local do aplicativo. Demonstracoes sinteticas nao criam snapshots nem backups do Windows.

Distribuicao portatil:
1. Extraia o ZIP completo.
2. Abra Optimizer.App.exe.
3. O runtime .NET Desktop esta incluido no ZIP autocontido.

Instalador por usuario:
- CalixtoOptimizer.iss e o script-fonte do instalador Inno Setup.
- Instala em LocalAppData\Programs\CalixtoOptimizer, sem pedir elevacao.
- O instalador nao inicia o aplicativo automaticamente.
- A pasta de instalacao e gravavel pelo usuario. Nenhum worker elevado esta incluido.
- O historico em LocalAppData\CalixtoOptimizer fica separado e e mantido ao desinstalar.
- O EXE do instalador nao esta incluido neste ZIP; o fonte Inno Setup e as instrucoes ficam no repositorio.
- O EXE do instalador nao foi produzido: ISCC.exe nao esta instalado neste ambiente.
- Sintaxe nao compilada e instalacao nao testada; nao trate o script como instalador validado.

Nenhum ajuste, driver, Registro, politica, servico, rede, energia ou firmware foi alterado para gerar o pacote.
