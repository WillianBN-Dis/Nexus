# Histórico do Nexus

## 1.1.0

- API de rede local com seleção de bancos, chaves por conta, permissões de leitura/escrita e renovação/revogação.
- Leitura paginada, filtro, ordenação e inserção/edição/exclusão de registros, sem SQL livre ou alteração de estrutura pela rede.
- Servidor desligado por padrão, vínculo a IPv4 privado/loopback, limites de requisição e regra de Firewall opcional restrita à rede privada.
- Janela fecha/minimiza para a bandeja; menu para abrir, desligar API, aplicar atualização e encerrar.
- Inicialização opcional em segundo plano com o Windows e restauração do servidor sem salvar senhas.
- Log de atividade com horário, IP, chave identificada, status e duração; rotação de arquivos, visualização e exportação, sem tokens ou conteúdo dos registros.
- Testes HTTP e de atendimento da API com a janela oculta.

## 1.0.0

- Aplicativo nativo Windows em C#/.NET/WPF, com identidade visual escura e layout adaptável.
- Contas locais compatíveis com as versões Python, sem converter senhas ou bancos existentes.
- Criar, renomear, mover entre pastas/discos e excluir bancos SQLite para a Lixeira.
- Editor SQL e edição visual de tabelas, colunas e registros; ordenação e paginação.
- Confirmações no tema do app, status no cabeçalho e rolagem discreta nos formulários.
- Instalador `Nexus-Setup.exe` com opção de atalho na área de trabalho e runtime incluído.
- Atualizações automáticas pelo GitHub Releases: download em segundo plano e aplicação na próxima abertura, ou pelo botão Reiniciar e atualizar.
- Verificação manual de atualizações e proteção contra abrir duas instâncias.
- Publicação de futuras versões por tags `vMAJOR.MINOR.PATCH` no GitHub Actions, após os testes do núcleo, interface e atualizador.

## Prévias 0.6.0-wpf.1 a 0.6.0-wpf.5

Migração do legado Python, validação de compatibilidade e correções de layout, abas, formulários e recargas. Código legado preservado como referência.
