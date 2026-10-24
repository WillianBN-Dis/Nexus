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

## Checkpoint final — migração concluída
- Branch local: codex/nexus-wpf. Identificação: 0.6.0-wpf.1. Nenhuma versão 1.0, branch remota ou release foi publicada.
- SDK .NET 10.0.402 instalado em %LOCALAPPDATA%/NexusDev/dotnet.
- Núcleo migrado: cadastro/login scrypt compatível; bancos; renomeação; transferência exclusiva com checkpoint e integridade; exclusão para Lixeira; SQL com leitura/escrita, authorizer, prazo de 5 segundos e cancelamento; tabelas/colunas/registros com paginação, ordenação e controle de edições obsoletas.
- Interface WPF/XAML concluída: login adaptável, tema escuro, ícone, barra de título Windows, grades virtualizadas, modos Visual/SQL, formulários com validação, estados vazios, confirmação de exclusão e tarefas de banco fora da thread UI. Perfil compacto para janelas de menor altura.
- Core smoke: 35 verificações passaram, incluindo timeout com rollback e proteção de arquivos.
- Compatibilidade cruzada Python → C# e C# → Python: passou em fixtures temporárias.
- UI smoke em janelas WPF reais: passou para cadastro/login, tamanhos, tabela, validação, registro, edição, coluna, SQL, alternância, cancelamento responsivo e Lixeira. Repetido no executável autocontido: exit 0.
- Dependências: Microsoft.Data.Sqlite 10.0.13, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, BouncyCastle.Cryptography 2.6.2. Auditoria NuGet sem vulnerabilidades reportadas.
- Executável: native/artifacts/publish/Nexus.exe (autocontido Windows x64; não requer Python/runtime separado).
- Instalador: dist/Nexus-Setup-0.6.0-wpf.1.exe, compilado com sucesso. Atalho opcional e dados do usuário preservados.
- Guia: native/README.md. Build reproduzível: native/build.ps1. Relatório e imagens de validação: native/qa (gerados, fora do Git).
- Python legado mantido intacto como referência. Testes não utilizaram bancos nem contas reais.
- Trabalho concluído: encerrar a automação continuar-migra-o-nexus-wpf para não repetir a migração.
