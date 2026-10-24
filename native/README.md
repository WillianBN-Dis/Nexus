# Nexus para Windows — C# / WPF

Prévia **0.6.0-wpf.3**, branch local `codex/nexus-wpf`. A versão 1.0 permanece reservada para a futura release no GitHub.

## Executar
- Instalador: `../dist/Nexus-Setup-0.6.0-wpf.3.exe`.
- Executável independente: `artifacts/publish/Nexus.exe`.
- Windows x64. O pacote inclui o runtime .NET; não é necessário instalar Python nem o SDK para usar.
- O instalador permite escolher o atalho da área de trabalho. Atualiza o aplicativo instalado mantendo o catálogo e os bancos do usuário.

## Recursos migrados
- Cadastro e login compatíveis com o scrypt da versão Python; nenhuma conversão de senhas necessária.
- Criação, renomeação, transferência e exclusão de bancos para a Lixeira.
- Modo visual: tabelas, colunas, registros por formulários, edição e exclusão com confirmação, ordenação no banco inteiro e páginas de 200 linhas.
- Chaves compostas, tabelas WITHOUT ROWID, valores NULL/padrão e BLOB em hexadecimal. Colunas calculadas são somente leitura; tabelas sem identificador seguro não permitem editar/excluir linhas.
- SQL com leitura e escrita, uma instrução por execução. Confirmação antes de instruções de escrita. Resultado em grade e texto, até 1.000 linhas, prazo de 5 segundos e cancelamento.
- ATTACH, DETACH, PRAGMA, transações manuais e carregamento de extensões bloqueados no editor. O gerenciador de tabelas usa operações parametrizadas e identificadores escapados.
- Layout XAML/WPF com controles virtualizados, tema escuro, formulários modais, navegação por teclado e adaptação de janela. Operações de banco e arquivos ficam fora da thread da interface.

## Dados e transferência
Dados atuais: `%LOCALAPPDATA%/Nexus/catalog.sqlite`. Os caminhos dos arquivos dos bancos são preservados. Contas separam o acesso dentro do aplicativo; os arquivos não são criptografados e as permissões do Windows continuam relevantes.

Antes de transferir, feche outros programas que estejam usando o banco. O Nexus faz checkpoint do WAL, obtém acesso exclusivo ao arquivo no Windows, copia sem sobrescrever o destino, verifica `integrity_check`, atualiza o catálogo e só então remove a origem. Se não puder remover a cópia antiga, informa o caminho ao usuário. Como no legado, queda de energia durante operações entre arquivo e catálogo pode deixar cópias órfãs. Mantenha backups independentes.

## Desenvolvimento
SDK .NET 10. O ambiente desta máquina usa `%LOCALAPPDATA%/NexusDev/dotnet/dotnet.exe` (instalação local do SDK, fora do repositório).

```powershell
./native/build.ps1
```

O script testa, publica um executável Windows autocontido e compila o instalador com Inno Setup 6. `-SkipTests` existe para empacotamento após validação já concluída.

```powershell
dotnet run --project native/Nexus.Smoke/Nexus.Smoke.csproj
dotnet build native/Nexus.Wpf/Nexus.Wpf.csproj
dotnet native/Nexus.Wpf/bin/Debug/net10.0-windows/Nexus.dll --ui-test
```

O teste de interface usa dados temporários, exercita as janelas WPF reais e salva imagens em `native/qa`. Não utiliza contas ou bancos reais; testa a Lixeira com um arquivo sintético. `--startup-check` verifica a abertura e encerra após 3 segundos.

Compatibilidade cruzada: gere uma fixture com `native/scripts/legacy_fixture.py <pasta-temporaria>`, execute `Nexus.Smoke` passando essa pasta e depois rode o script com o argumento adicional `verify`. Ele valida senhas Python no C# e senhas C# no Python. Nunca passe a pasta real `%LOCALAPPDATA%/Nexus` para gerar fixtures.

## Distribuição e limites
O binário e o instalador ainda não têm assinatura digital. Esta prévia conserva SQLite local e login local; não adiciona sincronização online nem suporte a MySQL/PostgreSQL. Os fontes Python são mantidos como referência e alternativa durante a transição. Nada é publicado automaticamente no GitHub.

## Correções de interface — WPF.2
Abas mantêm fundo opaco e estável ao mover o mouse; o foco usa uma borda discreta, sem o adorno pontilhado do tema. Durante operações, uma camada transparente bloqueia novas interações sem desabilitar as listas ou mudar suas cores. Os dados são lidos em background e aplicados juntos: a grade não é esvaziada entre a consulta e o resultado. A lista de tabelas só é substituída se seus nomes mudarem. Testes de regressão observam os frames de renderização e as mudanças de ItemsSource durante recarga, criação e exclusão.

## Ajustes de interface — WPF.3
Ação Mover de Pasta; confirmação de registro no tema WPF; exclusão de banco confirmada por DELETAR sem diferenciar maiúsculas; rolagem fina com trilho transparente; status no cabeçalho junto ao SQLite, verde em sucesso e vermelho em erro. Cabeçalhos usam um painel de abas sem recortes; botão Novo banco ganha espaço adequado. O SQL mantém altura limitada, sem divisor arrastável, sem tooltip e sem esticar o botão Executar.
