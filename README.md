# Nexus 0.2.0

Aplicativo desktop Windows para bancos SQLite locais. Cadastro e login por usuário, criação e renomeação de bancos, editor SQL para tabelas e registros, exclusão para a Lixeira e transferência entre pastas/discos.

## Executar
Python 3.12: `python -m pip install -r requirements.txt` e `python nexus.py`.

## Gerar instalador
Instale PyInstaller 6.22.3 e Inno Setup 6. Execute:
```
python -m PyInstaller --noconfirm --clean --windowed --onedir --name Nexus nexus.py
iscc installer.iss
```
Saída: `dist/Nexus-Setup-0.2.0.exe`. Instalador por usuário, com atalhos e desinstalador. Não requer Python no computador de destino. Binário ainda sem assinatura digital.

## Dados e limites
Contas e catálogo: `%LOCALAPPDATA%/Nexus`. Senhas com scrypt e salt aleatório. Bancos separados por conta no aplicativo; arquivos não são criptografados e continuam acessíveis a quem tem acesso ao Windows. Não há login online, sincronização, recuperação de senha ou suporte a PostgreSQL/MySQL nesta versão.

SQL: uma instrução por execução, resultados limitados a 1.000 linhas e consultas com prazo de 5 segundos. ATTACH, DETACH e PRAGMA bloqueados. Para mover, feche outros programas que usam o arquivo; o Nexus cria uma cópia usando SQLite Backup, verifica sua integridade, atualiza o catálogo e remove a origem. Falha na remoção é informada. Não use simultaneamente várias instâncias; transferências interrompidas por queda de energia podem deixar cópias órfãs. Mantenha backups independentes.

## Testes
`python -m unittest -v`

## Interface 0.2.0
Barra lateral com filtro por nome, editor SQL com Ctrl+Enter, resultados com linhas alternadas e login redesenhado. O instalador em português permite escolher se deseja criar um atalho na área de trabalho (desmarcado por padrão).

Teste da interface: `python smoke_ui.py` (usa dados temporários e não modifica suas contas).
