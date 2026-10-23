# Nexus 0.4.1

Aplicativo desktop Windows para bancos SQLite locais. Cadastro e login por usuário, criação e renomeação de bancos, editor SQL para tabelas e registros, exclusão para a Lixeira e transferência entre pastas/discos.

## Executar
Python 3.12: `python -m pip install -r requirements.txt` e `python nexus.py`.

## Gerar instalador
Instale PyInstaller 6.22.3 e Inno Setup 6. Execute:
```
python -m PyInstaller --noconfirm --clean --windowed --onedir --name Nexus --icon assets/nexus.ico --add-data "assets;assets" --collect-all customtkinter --distpath dist-0.4.1 --workpath build-0.4.1 nexus.py
iscc installer.iss
```
Saída: `dist/Nexus-Setup-0.4.1.exe`. Instalador por usuário, com atalhos e desinstalador. Não requer Python no computador de destino. Binário ainda sem assinatura digital.

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

Validação do executável: `dist-0.4.1/Nexus/Nexus.exe --startup-check` abre a janela, confirma visibilidade após 2 segundos e encerra. A distribuição corrigida está em `dist-0.4.1/Nexus/`, e o instalador em `dist/Nexus-Setup-0.4.1.exe`.
