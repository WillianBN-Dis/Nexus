# Migração Nexus → C# / WPF

## Autorização e escopo
Usuário autorizou migrar o que for possível agora e retomar automaticamente quando a cota for reposta. Não publicar no GitHub nem lançar 1.0. Usar branch codex/nexus-wpf e prévias 0.6.0-wpf.N. Não usar subagentes sem nova autorização.

## Local e continuidade
- Workspace: C:\Users\user\Desktop\Projetos\Nexus
- Clone completo do legado de C:\Users\user\Desktop\GitHub\Nexus, preservado sem alterações.
- Branch: codex/nexus-wpf, baseada em codex/interface-visual.
- Automação heartbeat: continuar-migra-o-nexus-wpf, ativa, a cada hora nesta conversa.
- Verificar get_usage_limits antes de retomar; aguardar em silêncio se cota <5%. Reinício da janela atual informado: 23/10/2026 21:28:33 America/Sao_Paulo. Pode mudar; consultar ferramenta.
- SDK sendo instalado pelo script oficial em %LOCALAPPDATA%\NexusDev\dotnet; checar conclusão. Usar caminho absoluto do dotnet.exe, sem depender do PATH.

## Regras de preservação
Catálogo atual: %LOCALAPPDATA%\Nexus\catalog.sqlite. Nunca usar dados reais em testes. Criar testes com pasta temporária. Contas Python usam scrypt N=16384,r=8,p=1, salt 32 bytes, hash 64 bytes, UTF-8. Login C# deve validar esse formato. Metadados e caminhos dos bancos devem continuar compatíveis. Não remover nem modificar bancos reais. Nunca incluir credenciais no repo.

## Plano
1. Instalar SDK .NET 10 e criar native/Nexus.Core, native/Nexus.Wpf e native/Nexus.Smoke.
2. Migrar serviço SQLite: login/cadastro compatíveis, CRUD de bancos, SQL com limites, transferência por backup SQLite e verificação, Lixeira.
3. Montar interface WPF com layout XAML Grid, controles DataGrid, comandos e tarefas de banco fora da thread UI.
4. Migrar editor visual: criação/alteração de tabelas e colunas, inserir/editar/excluir registros, ordenação e páginas.
5. Testar compatibilidade com fixtures temporárias geradas pelo Python; testes funcionais e abertura do executável WPF.
6. Publicar self-contained win-x64 e instalador com nome Nexus-Setup-0.6.0-wpf.1.exe; não sobrescrever release estável antes de validar.
7. Documentar, atualizar checkpoint e fazer commits pequenos. Pausar automação ao concluir migração validada.

## Checkpoint atual
Implementação C#/WPF concluída; empacotamento final em andamento.
- SDK .NET 10.0.402 instalado em %LOCALAPPDATA%/NexusDev/dotnet.
- Núcleo: cadastro/login scrypt compatível, bancos, renomeação, transferência exclusiva verificada, Lixeira, SQL com authorizer/progress handler e cancelamento, edição visual com verificação de linhas obsoletas.
- Interface: login adaptável, navegação de bancos, modos Visual/SQL, formulários de tabela/coluna/registro, grid, estrutura, paginação/ordenação, estados vazios, confirmação de exclusão, operações em background.
- Core smoke: 35 verificações passaram, incluindo timeout com rollback e proteção de arquivos.
- Fixture cruzada: Python → C# e C# → Python passaram (apenas dados temporários).
- UI smoke WPF real passou: criação, validação, edição, SQL, cancelamento responsivo, tamanhos e Lixeira. Relatório e imagens em native/qa.
- Dependências atualizadas: Microsoft.Data.Sqlite 10.0.13, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, BouncyCastle.Cryptography 2.6.2. Auditoria NuGet não encontrou vulnerabilidades.
- Próximo passo: terminar native/build.ps1 -SkipTests, rodar --startup-check e --ui-test no binário autocontido, verificar instalador, registrar commit e encerrar a automação quando tudo passar.
- Nenhum dado real foi usado em testes. Nenhuma branch/release publicada. Versão 1.0 não criada.
