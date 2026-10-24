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

## Manutenção posterior — 0.6.0-wpf.2
Correção de oscilação de cor das abas e clarão na recarga após criar/excluir tabelas. Dados preservados até a nova leitura estar pronta, navegação sem mudança para tema desabilitado e foco sem adornos pontilhados. Testes WPF de regressão passaram. A migração continua concluída e a automação permanece encerrada.

## Manutenção posterior — 0.6.0-wpf.3
Ajustes de interface solicitados pelo usuário: mover pasta, confirmação temática, DELETAR/deletar, rolagem discreta, status de tabela no cabeçalho com cores, abas e CTA sem cortes e editor SQL com layout estável. Testes WPF ampliados para esses fluxos.
Manutenção 0.6.0-wpf.4: abas preenchem a altura ao maximizar; barra de bancos oculta mantendo rolagem. Testes em 1040x740, 1920x1080, 2560x1440 e maximizado passaram. Build completo e teste do executável autocontido passaram; instalador wpf.4 gerado.
Manutenção 0.6.0-wpf.5: barras verticais ocultas nos formulários Adicionar registro e Editar registro, mantendo rolagem e removendo o espaço lateral reservado. Build e testes WPF do executável passaram. Versão 1.0 permanece sem publicação.
Preparação 1.0.0 autorizada pelo usuário: Velopack 1.2.161 integrado, download automático ao abrir e a cada seis horas, reinício seguro e instalador Inno com atalho opcional preservado. Testes do núcleo, WPF e 11 verificações de atualização real passaram. Workflow por tags vMAJOR.MINOR.PATCH e documentação adicionados. Publicação da release em andamento; autenticação confirmada na conta WillianBN-Dis com permissão ADMIN.

## Release 1.0.0 — publicada e validada
Publicação autorizada pelo usuário e concluída em https://github.com/WillianBN-Dis/Nexus/releases/tag/v1.0.0. Código em main; tag v1.0.0 aponta para 7851985. Workflow 40106782939 concluído com sucesso: núcleo, WPF, atualizador e instalador.
Verificação posterior à publicação: link fixo público do Nexus-Setup.exe sem autenticação e SHA256 conferidos; teste do atualizador contra o GitHub público passou com 13 verificações (incluindo download remoto, pacote inválido, downgrade, troca real de arquivos e preservação de contas/SQLite).
Instalador público também salvo em dist/Nexus-Setup.exe. Atualizações futuras: tags estáveis vMAJOR.MINOR.PATCH acionam o workflow documentado em UPDATES.md. Usuários das prévias 0.6.0 devem executar o instalador 1.0 uma vez para habilitar o atualizador. Nenhuma conta ou banco real foi usado nos testes. Automação de migração continua encerrada.

## Atualização 1.1.0 — API, bandeja e logs
API HTTP local implementada em Nexus.Api com chaves de 256 bits (hash no catálogo), escopo de bancos por conta, leitura/escrita por chave, filtro/paginação e CRUD de registros com conflito otimista; sem SQL livre. Desligada por padrão, bind somente IPv4 privado/loopback; Firewall privado/LocalSubnet somente pelo botão com UAC.
Nexus permanece na bandeja ao fechar/minimizar, com restauração por nova execução e saída explícita; autostart opcional via HKCU Run. Atualização encerra API antes de reiniciar e preserva configurações e chaves.
Log JSON Lines com IP, rota sem valores, status, chave identificada e duração; eventos de servidor/chaves. Sem tokens/senhas/corpos, rotação em 5 arquivos ~5 MB, visualização/exportação.
Validação local: 33 testes de núcleo, 37 verificações HTTP, testes WPF incluindo API com janela fechada e teste no executável autocontido passaram. Empacotamento 1.1.0 concluído. Guia API.md e changelog atualizados. Publicação 1.1.0 em preparação; API real deste PC não foi ativada e nenhuma regra de Firewall/autostart foi aplicada pelos testes.

## Release 1.1.0 — publicada e validada
Publicada em https://github.com/WillianBN-Dis/Nexus/releases/tag/v1.1.0, tag aa6f760. Workflow 40110552144 concluído com sucesso: 33 verificações do núcleo, 37 HTTP/segurança/logs, interface WPF e 12 de atualização com preservação de chaves de API.
Verificação pública posterior: link fixo do instalador e SHA256 conferidos; teste contra GitHub Releases passou com 14 verificações, incluindo download remoto. Instalador público salvo em dist/Nexus-Setup.exe. A API começa desativada, para configuração pelo usuário em API e rede. Nenhuma porta LAN real, regra de Firewall ou inicialização automática deste PC foi ativada pelos testes. Trabalho concluído sem esgotar a cota; não foi necessário agendar continuação.

## Ajuste 1.1.1 — menu da bandeja
Menu padrão WinForms substituído por ContextMenu WPF no tema do Nexus: cabeçalho com ícone e status da API, cantos arredondados, ícones por ação, hover violeta, separador discreto e ação de encerrar destacada. Sem trilho branco/destaque azul do Windows. As ações indisponíveis ficam desabilitadas.
Teste WPF real confirmou abertura com janela principal oculta e estado correto dos itens; screenshot nativo em native/qa/08-menu-bandeja.png inspecionado. Preservados duplo clique para restaurar, desligamento da API, atualização e encerramento. Empacotamento 1.1.1 em andamento.
Release 1.1.1 publicada: https://github.com/WillianBN-Dis/Nexus/releases/tag/v1.1.1. Workflow 40127279259 passou. Executável autocontido validado com menu aberto em segundo plano; instalador, SHA256 e versão do feed público conferidos. Instalador público salvo em dist/Nexus-Setup.exe.

## Ajuste 1.1.2 — ícone refinado
Ícone editado com image_gen preservando o N azul/violeta; fundo azul-escuro sem contorno luminoso. PNG original gerado preservado; ICO reempacotado em 16, 20, 24, 32, 40, 48, 64, 96, 128 e 256 px com LANCZOS. Prompt em assets/nexus-icon.md.
Janela WPF passa a usar ICO com múltiplas resoluções; bandeja seleciona SmallIconSize do Windows e imagens WPF usam HighQuality. Verificações dos frames, testes do núcleo/API/atualizador e WPF passaram. Aparência inspecionada em native/qa/08-menu-bandeja.png. Instalador local 1.1.2 compilado; publicação em preparação.
Release 1.1.2 publicada em https://github.com/WillianBN-Dis/Nexus/releases/tag/v1.1.2. Workflow 40129295201 passou; teste do executável autocontido também passou. Instalador, SHA256 e feed público verificados. Artefatos finais: assets/nexus.png, assets/nexus.ico e dist/Nexus-Setup.exe.
