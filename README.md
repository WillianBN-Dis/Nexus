# Nexus — C# / WPF

A implementação ativa nesta branch é a prévia **0.6.0-wpf.1** em `native/`. Consulte [o guia WPF](native/README.md) para executar, compilar e testar. O instalador fica em `dist/Nexus-Setup-0.6.0-wpf.1.exe`. Os fontes Python abaixo permanecem preservados como histórico da migração.

---

# Nexus 0.6.0 — Interface UI.1

Aplicativo desktop Windows para bancos SQLite locais. Cadastro e login por usuário, criação e renomeação de bancos, editor SQL para tabelas e registros, exclusão para a Lixeira e transferência entre pastas/discos.

## Executar
Python 3.12: `python -m pip install -r requirements.txt` e `python nexus.py`.

## Gerar instalador
Instale PyInstaller 6.22.3 e Inno Setup 6. Execute:
```
python -m PyInstaller --noconfirm --clean --windowed --onedir --name Nexus --icon assets/nexus.ico --add-data "assets;assets" --collect-all customtkinter --distpath dist-0.6.0-ui.1 --workpath build-0.6.0-ui.1 nexus.py
iscc installer.iss
```
Saída: `dist/Nexus-Setup-0.6.0-ui.1.exe`. Instalador por usuário, com atalhos e desinstalador. Não requer Python no computador de destino. Binário ainda sem assinatura digital.

## Dados e limites
Contas e catálogo: `%LOCALAPPDATA%/Nexus`. Senhas com scrypt e salt aleatório. Bancos separados por conta no aplicativo; arquivos não são criptografados e continuam acessíveis a quem tem acesso ao Windows. Não há login online, sincronização, recuperação de senha ou suporte a PostgreSQL/MySQL nesta versão.

SQL: uma instrução por execução, resultados limitados a 1.000 linhas e consultas com prazo de 5 segundos. ATTACH, DETACH e PRAGMA bloqueados. Para mover, feche outros programas que usam o arquivo; o Nexus cria uma cópia usando SQLite Backup, verifica sua integridade, atualiza o catálogo e remove a origem. Falha na remoção é informada. Não use simultaneamente várias instâncias; transferências interrompidas por queda de energia podem deixar cópias órfãs. Mantenha backups independentes.

## Testes
`python -m unittest -v`

## Interface 0.3.0
Barra lateral com filtro por nome, editor SQL com Ctrl+Enter, resultados com linhas alternadas e login redesenhado. O instalador em português permite escolher se deseja criar um atalho na área de trabalho (desmarcado por padrão).

Teste da interface: `python smoke_ui.py` (usa dados temporários e não modifica suas contas).

## Identidade visual 0.3.0
Interface em CustomTkinter com cantos arredondados, paleta violeta, marca própria e SQL com destaque de sintaxe. Ícone em PNG e ICO (16 a 256 px) em assets/. O executável e o instalador incorporam o ícone. A barra de título nativa recebe ícone e cores escuras pelo DWM, mantendo os controles padrão e Snap do Windows. Cor de título e cantos dependem do suporte da versão do Windows; há fallback para a aparência nativa.

Ícone gerado com a ferramenta integrada de imagens. Direção: N formado por elos curvos, violeta e azul sobre fundo azul-escuro, legível como ícone de aplicativo.

## Melhorias 0.4.0
A tela de entrada abre restaurada em 1280 × 850 e centralizada na área útil do monitor. Novo formulário de criação de banco segue a identidade do app, com validação no próprio formulário e cancelamento. As abas Resultado (texto por registro) e Tabela (grade de linhas e colunas) exibem a mesma consulta, com limite de 1.000 linhas. Ao trocar de banco, as duas visualizações são limpas. Erros SQL aparecem em Resultado e removem dados antigos da grade.

## Correção de abertura 0.4.1
A janela principal não é mais ocultada durante a inicialização. A centralização ocorre após a janela estar mapeada, usando Tk sem laços de eventos aninhados nem chamadas de monitor via ctypes. A aparência nativa é opcional e falhas não impedem a exibição. Diagnósticos de abertura e erros são gravados em `%LOCALAPPDATA%/Nexus/nexus.log`.

Validação do executável: `dist-0.6.0-ui.1/Nexus/Nexus.exe --startup-check` abre a janela, confirma visibilidade após 2 segundos e encerra. A distribuição corrigida está em `dist-0.6.0-ui.1/Nexus/`, e o instalador em `dist/Nexus-Setup-0.6.0-ui.1.exe`.

## Layout adaptável 0.5.0
O login mantém duas colunas centralizadas com largura máxima de 2.050 unidades lógicas, formulário de até 680 e conteúdo visual centralizado verticalmente. Tipografia, ícones e controles crescem até 65% conforme a altura e largura disponíveis. Ao reduzir a janela, os tamanhos retornam ao normal sem perder os campos preenchidos. No painel, a barra lateral, o editor e a área de resultados se adaptam ao espaço. A escala respeita o DPI do CustomTkinter e é aplicada após o redimensionamento, sem reconstruir a tela.

Validação de layout: `python test_layout.py --dashboard`.

## Modo visual 0.6.0
Alterne entre Visual e Editor SQL no banco selecionado. Em Visual, use Criar tabela, defina colunas TEXT/INTEGER/REAL/BLOB e campos obrigatórios. O ID inteiro é automático. Insira e edite registros por formulário (duplo clique também edita); NULL e valores padrão são explícitos. BLOB é informado em hexadecimal. Clique no cabeçalho para ordenar ascendente/descendente no banco inteiro; navegue por páginas de 200 registros. A aba Estrutura exibe as colunas e chaves. É possível adicionar colunas opcionais e excluir uma linha com confirmação ou uma tabela digitando o nome. O modo visual respeita chaves estrangeiras e rejeita alterações de linhas que foram modificadas por outro programa desde a leitura. Dados e estrutura são recarregados na troca de modo. Tabelas sem identificação segura para linhas permitem leitura/inserção, mas não edição/exclusão de linhas. Colunas calculadas são somente leitura. Alterações avançadas de esquema continuam disponíveis via SQL.

Testes: `python -m unittest -v`, `python smoke_visual.py` e `python smoke_ui.py`.

## Branch de interface
Trabalho isolado na branch local `codex/interface-visual`, partindo da versão 0.6.0. A identificação desta atualização é **0.6.0-ui.1**, mantendo `AppVersion=0.6.0` e sem criar uma versão 0.7 ou release 1.0. A versão 1.0 fica reservada à futura publicação no GitHub. Nenhuma branch ou release foi publicada por esta atualização.

A interface visual separa ações de registros e de estrutura, usa seleção contextual, apresenta linhas numeradas e colunas que acompanham a largura da janela, adiciona estados vazios e melhora os formulários de tabela e registro. A exclusão de tabela fica na aba Estrutura.
