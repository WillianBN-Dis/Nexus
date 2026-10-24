# Publicar atualizações do Nexus

O app verifica versões estáveis em `https://github.com/WillianBN-Dis/Nexus` ao abrir e a cada seis horas enquanto permanece aberto. Não acessa credenciais do GitHub do usuário. O repositório de distribuição deve permanecer público.

Quando existe uma versão maior, o Nexus baixa o pacote em segundo plano e verifica sua integridade. A atualização é aplicada antes de abrir o aplicativo na próxima execução. O aviso também permite escolher **Reiniciar e atualizar**; essa ação fica bloqueada enquanto houver uma operação ou formulário aberto.

Falhas de internet não interrompem o uso dos bancos. O botão **Verificar atualizações** permite tentar novamente; o diagnóstico fica em `%LOCALAPPDATA%/Nexus/updates.log`.

## Link fixo do instalador

`https://github.com/WillianBN-Dis/Nexus/releases/latest/download/Nexus-Setup.exe`

O nome e o endereço permanecem os mesmos. O conteúdo do instalador é gerado novamente em cada release. As versões 0.6.0 não tinham atualizador: seus usuários precisam executar esse instalador uma vez para migrar para a 1.0.0.

## Próximas versões

Depois de implementar e revisar as mudanças, atualize `CHANGELOG.md` e publique o código em `main`. Em seguida, crie uma tag estável, com versão maior que a anterior:

```powershell
git tag v1.1.2
git push origin v1.1.2
```

O workflow `.github/workflows/release.yml` usa a versão da tag para compilar e empacotar; não é necessário editar números em vários arquivos. Ele roda os testes do núcleo, das janelas WPF e do atualizador, cria a release como rascunho com todos os arquivos e só depois a publica como a mais recente. Um push de código sem uma tag não lança atualização.

Não reutilize uma versão já publicada. Em caso de correção, publique uma versão maior. O atualizador não faz downgrade e ignora rascunhos e pré-releases.

## Compilação local

Requisitos para desenvolver: SDK .NET 10 e Inno Setup 6. O `vpk` 1.2.161 é instalado automaticamente em `native/artifacts/tools`, fora do Git.

```powershell
./native/build.ps1 -Version 1.1.2
```

Saída: `dist/releases/1.1.2/`. Para publicação manual, envie à mesma release:

- `Nexus-Setup.exe`
- `releases.win.json`
- todos os arquivos `*.nupkg` gerados para essa versão
- `SHA256SUMS.txt`

Somente enviar o instalador não fornece o feed/pacote que os apps já instalados usam para atualizar. O ZIP Portable é usado como base pelo instalador e pelos testes locais e não precisa ser publicado.

## Instalação e dados

O Inno Setup mantém o assistente em português, o identificador antigo `Nexus.DatabaseStudio` e o atalho opcional. Ele instala a distribuição atualizável do Velopack em `%LOCALAPPDATA%/Programs/Nexus`. Os atalhos apontam para o lançador estável `Nexus.exe`; a pasta `current` contém os binários substituídos a cada atualização. O marcador `.portable` mantém o gerenciamento de atalhos e desinstalação sob responsabilidade do Inno Setup.

Contas e catálogo permanecem em `%LOCALAPPDATA%/Nexus`; os caminhos dos bancos permanecem no catálogo. A atualização não converte nem recria esses arquivos. A desinstalação remove o programa, sem apagar a pasta de dados ou os bancos externos.

Os testes usam diretórios temporários: validam download, integridade, ausência de downgrade, substituição real dos arquivos, abertura WPF e preservação de contas/SQLite. Não executam o assistente sobre a instalação pessoal do desenvolvedor.

## Referências

- [Velopack: integração e aplicação de atualizações](https://docs.velopack.io/integrating/overview)
- [Velopack: fontes de atualização](https://docs.velopack.io/integrating/update-sources)
- [GitHub: links para a release mais recente](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases)
