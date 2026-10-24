# Nexus para Windows — C# / WPF

Implementação ativa da versão **1.0.0**. Código Python legado preservado na raiz.

## Executar

- Instalador: `../dist/Nexus-Setup.exe`.
- Arquivos da release: `../dist/releases/1.0.0/`.
- Executável isolado para desenvolvimento: `artifacts/publish/Nexus.exe`; para habilitar o atualizador, use o instalador com seu lançador, pasta current e Update.exe.
- Windows x64, runtime .NET incluído. Sem Python ou SDK no computador de destino.
- Atalho na área de trabalho opcional e atualização da instalação anterior com o mesmo AppId.

## Desenvolvimento

SDK .NET 10 e Inno Setup 6. O script localiza dotnet no PATH ou na instalação privada `%LOCALAPPDATA%/NexusDev/dotnet`.

```powershell
./native/build.ps1 -Version 1.0.0
```

O build executa testes do núcleo e da interface WPF, publica o app autocontido, gera os pacotes Velopack 1.2.161, testa uma atualização real em diretório temporário e compila o instalador. `-SkipTests` serve apenas para reempacotamento após validação.

```powershell
dotnet run --project native/Nexus.Smoke/Nexus.Smoke.csproj
dotnet build native/Nexus.Wpf/Nexus.Wpf.csproj
dotnet native/Nexus.Wpf/bin/Debug/net10.0-windows/Nexus.dll --ui-test
dotnet run --project native/Nexus.UpdateSmoke/Nexus.UpdateSmoke.csproj -- dist/releases/1.0.0 1.0.0
```

Testes usam dados sintéticos, sem contas ou bancos reais. O teste WPF salva imagens em `native/qa`; `--startup-check` abre o app e encerra em três segundos.

O teste do atualizador verifica versão maior, ausência de downgrade, integridade do pacote, troca real de arquivos, abertura do binário e preservação de contas e registros. Um terceiro argumento com a URL do repositório também verifica a release pública.

Compatibilidade cruzada: gere uma fixture temporária com `native/scripts/legacy_fixture.py <pasta>`, passe essa pasta ao Nexus.Smoke e execute o script novamente com `verify`. Nunca gere fixtures sobre a pasta de dados real.

## Arquitetura

- `Nexus.Core`: contas scrypt compatíveis, catálogo, SQLite, SQL com authorizer/prazo/cancelamento, edição visual e operações de arquivo.
- `Nexus.Wpf`: janela, tema, formulários, operações assíncronas e verificação de atualizações.
- `Nexus.Smoke`: testes funcionais do núcleo.
- `Nexus.UpdateSmoke`: teste de atualização em pasta isolada.
- `build.ps1` e `installer.iss`: pacotes, instalador e hashes.

A movimentação executa checkpoint do WAL e usa acesso exclusivo ao arquivo no Windows; copia sem sobrescrever, verifica integridade e só então atualiza o catálogo e remove a origem. Falha em remover a origem informa o caminho; interrupções por queda de energia podem deixar cópias órfãs.

## Distribuição

Leia [UPDATES.md](../UPDATES.md) para o link fixo, os arquivos necessários na release e o workflow de tags. Contas e catálogo ficam em `%LOCALAPPDATA%/Nexus`. O programa é instalado separadamente em `%LOCALAPPDATA%/Programs/Nexus`. A desinstalação não apaga os bancos.

SQL limita uma instrução, 1.000 linhas e cinco segundos. ATTACH, DETACH, PRAGMA, transações manuais e extensões são bloqueados no editor. Arquivos não são criptografados. O binário e o instalador ainda não têm assinatura digital.
