# API local do Nexus — 1.1.0

## Ativar

1. Abra o Nexus, entre na sua conta e clique em **API e rede** na barra lateral.
2. Marque **Ativar API na rede local**, escolha o IPv4 do computador na rede e a porta (padrão 5080). `127.0.0.1` permite acesso somente neste PC.
3. Dê um nome à chave, selecione os bancos que ela pode acessar e escolha somente leitura ou leitura e escrita. Clique em **Gerar chave** e copie a chave: ela só é exibida nesse momento.
4. Clique em **Salvar servidor**. Para outros dispositivos, libere a porta pelo botão **Liberar no Firewall (rede privada)**; o Windows pedirá elevação. A regra vale apenas para o aplicativo, IP e porta selecionados, perfil privado e sub-rede local. Se mudar IP ou porta, atualize a regra.

A API usa HTTP, para redes locais confiáveis. O transporte não é criptografado. Não exponha a porta na internet nem encaminhe portas no roteador. O servidor aceita conexões de IPv4 privados e loopback, usa chaves de 256 bits e guarda apenas seus hashes. Não há login de usuário/senha pela API, SQL livre, acesso a arquivos ou CORS habilitado.

O servidor tem um endereço/porta por instalação do Windows. Cada conta Nexus gerencia apenas suas chaves e só pode compartilhar seus próprios bancos. Renovar ou revogar uma chave invalida o token antigo nas próximas solicitações.

## Segundo plano

Fechar ou minimizar a janela mantém o Nexus na bandeja, ao lado do relógio. A API continua atendendo mesmo após sair da conta na interface. No menu da bandeja:

- **Abrir Nexus** restaura a janela; abrir o aplicativo novamente também a restaura.
- **Desligar API** interrompe o servidor e salva a configuração desligada.
- **Atualizar e reiniciar** aplica um update já baixado, aguardando o encerramento da API.
- **Encerrar Nexus e API** encerra o processo. Não é possível encerrar/reiniciar durante uma operação da interface ou com formulário aberto.

Para iniciar sem abrir a interface após entrar no Windows, marque **Iniciar em segundo plano com o Windows** nas configurações e salve. Essa opção exige a versão instalada, é opcional e usa o lançador estável do app. A API habilitada é restaurada sem guardar a senha da sua conta. Se o IP deixar de existir, abra as configurações e selecione o novo endereço.

Não é um serviço de sistema: o computador precisa estar ligado e o usuário do Windows conectado. Suspensão, desligamento e saída do Windows interrompem o acesso.

## Autenticação

Envie a chave em todas as solicitações:

```http
Authorization: Bearer nxs_SUA_CHAVE
```

Não coloque chaves na URL. Os exemplos abaixo usam um IP ilustrativo; substitua pelo endereço mostrado no seu Nexus.

```powershell
$base = 'http://192.168.1.10:5080'
$headers = @{ Authorization = 'Bearer nxs_SUA_CHAVE' }
$bancos = Invoke-RestMethod "$base/api/bancos" -Headers $headers
$bancos
```

## Endpoints

`{id}` é o identificador retornado por `/api/bancos`; `{tabela}` é o nome da tabela, codificado para URL quando necessário.

| Método | Caminho | Função |
| --- | --- | --- |
| GET | `/api/bancos` | Lista somente os bancos permitidos, sem expor caminhos de arquivos |
| GET | `/api/bancos/{id}/tabelas` | Lista nomes das tabelas |
| GET | `/api/bancos/{id}/tabelas/{tabela}/estrutura` | Colunas e identificação segura dos registros |
| GET | `/api/bancos/{id}/tabelas/{tabela}/registros` | Lê até 200 registros por página |
| POST | mesmo caminho de registros | Insere uma linha |
| PATCH | mesmo caminho de registros | Atualiza uma linha com controle de conflito |
| DELETE | mesmo caminho de registros | Exclui uma linha com controle de conflito |

Chaves de leitura recebem HTTP 403 em operações de escrita. Não existem endpoints de criação/exclusão de bancos, alteração de tabelas ou execução de SQL.

### Leitura e filtros

```http
GET /api/bancos/ID/tabelas/produtos/registros?offset=0&ordenarPor=nome&desc=false&coluna=categoria&valor=livros
```

`offset` vai de 0 a 1000000, em páginas de até 200 linhas. `ordenarPor` aceita uma coluna existente; `desc=true` inverte a ordem. `coluna` e `valor` fazem filtro de igualdade parametrizado; omitir `valor` com uma coluna filtra por NULL. Sem ordenação explícita, a ordem do SQLite não é garantida. Alterações simultâneas podem deslocar registros entre páginas.

```json
{
  "colunas": ["id", "nome"],
  "identidade": ["rowid"],
  "offset": 0,
  "limite": 200,
  "temMais": false,
  "registros": [{"chave": [1], "valores": [1, "Exemplo"]}]
}
```

Cada array `valores` segue a ordem de `colunas`. `chave` segue `identidade`. Guarde ambos para editar ou excluir exatamente a linha lida. BLOB é retornado como base64; números e NULL preservam seus tipos JSON. Colunas SQLite dinamicamente tipadas devem ser usadas de acordo com o tipo declarado; valores incompatíveis podem gerar conflito ao editar.

### Inserir

Envie `Content-Type: application/json`. Campos omitidos usam o padrão definido no banco.

```http
POST /api/bancos/ID/tabelas/produtos/registros
```

```json
{"valores": {"nome": "Novo produto", "preco": 19.9}}
```

### Editar ou excluir

Envie a `chave` e o array `valores` da última leitura como `anteriores`. Para editar, inclua somente os campos que deseja mudar em `valores`:

```json
{"chave": [1], "anteriores": [1, "Exemplo"], "valores": {"nome": "Atualizado"}}
```

Use PATCH para editar. Para DELETE, envie `chave` e `anteriores` no corpo, sem `valores`. O servidor nunca executa exclusão em massa. Se a linha mudou desde a leitura, retorna HTTP 409; consulte novamente antes de repetir. Tabelas sem identidade segura não permitem editar ou excluir linhas.

## Limites e diagnóstico

- Corpo JSON de até 1 MB; texto, números, NULL e base64 nas colunas BLOB.
- Até quatro solicitações em execução e 120 solicitações por minuto por endereço IP; excesso retorna HTTP 429.
- As operações SQLite usam o limite de execução de cinco segundos do núcleo e controle de concorrência do SQLite.
- HTTP 400: entrada inválida; 401: chave inválida/revogada; 403: escrita não permitida; 404: banco fora do escopo; 409: conflito/restrição; 415: conteúdo não JSON; 503: banco/disco ocupado ou indisponível; 504: tempo de consulta excedido.

As configurações ficam em `%LOCALAPPDATA%/Nexus/lan-api.json`, sem chaves em texto puro. Contas e permissões ficam no catálogo existente. Mover um banco pela interface mantém seu ID; o servidor resolve o caminho atualizado a cada solicitação. Operações em andamento podem falhar enquanto um banco é movido ou removido; tente novamente após concluí-las.


## Log de atividade

Em **API e rede → Ver log da API**, consulte os últimos 300 eventos, atualize a visualização, copie o texto ou exporte o arquivo atual em JSON Lines.

Os arquivos ficam em `%LOCALAPPDATA%/Nexus/logs/api.log`, com arquivos anteriores `api.log.1` até `api.log.4`. A rotação ocorre perto de 5 MB por arquivo, mantendo aproximadamente 25 MB. Exporte o log quando precisar guardar um histórico maior.

São registrados início/parada do servidor, falhas de configuração, criação/renovação/revogação de chaves pela interface e solicitações HTTP. Cada solicitação inclui horário com fuso, IP, método, modelo de rota, ID do banco quando válido, nome/ID da chave reconhecida, status HTTP e duração. O log é do servidor deste computador, incluindo as chaves das contas que o utilizam.

Tokens de acesso, senhas, cabeçalhos Authorization, valores de filtros, corpos de requisição e conteúdo dos registros não são gravados. Respostas 401, 403, 409, 429 e demais falhas ficam identificadas pelo status. O log não substitui backup dos bancos. Falha de escrita gera um aviso pela bandeja; confira espaço em disco e permissões da pasta.
