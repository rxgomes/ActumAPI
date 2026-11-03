# Spec Delta

## Purpose

Define como o Actum acessa a API do Asana em nome do usuário: autenticação, leitura de tarefas atribuídas, histórico e comentários, escrita de tags e conclusão de tarefas, com tratamento uniforme de limites e falhas.

## ADDED Requirements

### Requirement: Autenticação por token configurado
O cliente do Asana SHALL autenticar todas as requisições com o cabeçalho `Authorization: Bearer <token>`, lendo o token da configuração `Asana:Token`. O token MUST NOT ser registrado em logs nem devolvido em respostas da API.

#### Scenario: Token configurado
- **WHEN** `Asana:Token` está configurado e qualquer operação do cliente é executada
- **THEN** a requisição ao Asana MUST conter `Authorization: Bearer <token>`

#### Scenario: Token ausente
- **WHEN** `Asana:Token` não está configurado e uma operação do cliente é executada
- **THEN** o cliente MUST falhar com erro de token não configurado, com mensagem em pt-BR, sem chamar o Asana

### Requirement: Verificação da conexão
A API SHALL expor `GET /asana/conexao`, que informa se o token está configurado, se é válido, o nome do usuário e os workspaces acessíveis.

#### Scenario: Token válido
- **WHEN** o token é válido e um cliente chama `GET /asana/conexao`
- **THEN** a resposta MUST ter status `200`, `configurado=true`, `valido=true`, o nome do usuário e a lista de workspaces com `gid` e `nome`

#### Scenario: Token inválido
- **WHEN** o Asana responde `401` e um cliente chama `GET /asana/conexao`
- **THEN** a resposta MUST ter status `200`, `configurado=true` e `valido=false`

#### Scenario: Token não configurado
- **WHEN** o token não está configurado e um cliente chama `GET /asana/conexao`
- **THEN** a resposta MUST ter status `200` e `configurado=false`

### Requirement: Listagem de tarefas abertas atribuídas ao usuário
O cliente SHALL listar, por workspace, apenas as tarefas não concluídas atribuídas ao usuário autenticado, percorrendo todas as páginas.

#### Scenario: Várias páginas
- **WHEN** o Asana devolve 100 tarefas com `next_page.offset` preenchido e, na página seguinte, 20 tarefas sem `next_page`
- **THEN** o cliente MUST devolver as 120 tarefas

#### Scenario: Campos da tarefa
- **WHEN** uma tarefa é devolvida pelo Asana
- **THEN** o resultado MUST conter gid, nome, notas em texto e em HTML, prazo (data ou nulo), indicador de concluída, data de modificação, link, nomes dos projetos e tags

### Requirement: Consulta de tarefa individual
O cliente SHALL obter uma tarefa pelo gid e MUST devolver nulo quando o Asana responder `404`.

#### Scenario: Tarefa excluída
- **WHEN** o Asana responde `404` para o gid informado
- **THEN** o cliente MUST devolver nulo, sem erro

### Requirement: Histórico da tarefa
O cliente SHALL listar as stories de uma tarefa, classificando cada uma como comentário, marcada como incompleta, marcada como concluída ou outro tipo, com data, autor e texto.

#### Scenario: Reabertura no histórico
- **WHEN** o Asana devolve uma story com `resource_subtype` `marked_incomplete`
- **THEN** o cliente MUST devolvê-la com o tipo "marcada como incompleta", a data de criação em horário local e o nome do autor

#### Scenario: Comentário no histórico
- **WHEN** o Asana devolve uma story com `resource_subtype` `comment_added`
- **THEN** o cliente MUST devolvê-la com o tipo "comentário" e o texto do comentário

### Requirement: Tag por nome exato
O cliente SHALL obter a tag do workspace cujo nome seja exatamente igual ao informado (sensível a maiúsculas) e, se não existir, MUST criá-la no workspace.

#### Scenario: Tag existente
- **WHEN** o workspace já tem a tag `feature/ipi-devolvido`
- **THEN** o cliente MUST devolver essa tag sem criar outra

#### Scenario: Tag inexistente
- **WHEN** o workspace não tem tag com o nome `feature/ipi-devolvido`
- **THEN** o cliente MUST criar a tag com esse nome no workspace e devolvê-la

#### Scenario: Diferença de maiúsculas
- **WHEN** o workspace tem a tag `Main` e é pedida a tag `main`
- **THEN** o cliente MUST criar a tag `main`

### Requirement: Escrita na tarefa
O cliente SHALL adicionar uma tag a uma tarefa e marcar uma tarefa como concluída.

#### Scenario: Concluir tarefa
- **WHEN** a conclusão de uma tarefa é solicitada
- **THEN** o cliente MUST enviar `PUT /tasks/{gid}` com `completed: true`

### Requirement: Limite de requisições
Quando o Asana responder `429`, o cliente SHALL aguardar o tempo indicado em `Retry-After` e repetir a requisição, até 3 tentativas adicionais.

#### Scenario: Limite temporário
- **WHEN** o Asana responde `429` com `Retry-After: 1` e, na repetição, `200`
- **THEN** a operação MUST concluir com sucesso

#### Scenario: Limite persistente
- **WHEN** o Asana responde `429` em todas as tentativas
- **THEN** a operação MUST falhar com erro de Asana indisponível

### Requirement: Tradução de falhas do Asana
Falhas de comunicação com o Asana SHALL resultar em respostas Problem Details com mensagem em pt-BR: token ausente ou acesso negado (`401`/`403`) resultam em `400`; rede, timeout, `5xx` ou limite esgotado resultam em `502`.

#### Scenario: Asana fora do ar
- **WHEN** um endpoint do Actum depende do Asana e o Asana responde `503`
- **THEN** a resposta do Actum MUST ter status `502` no formato Problem Details, com `detail` em pt-BR e sem o token

#### Scenario: Token revogado
- **WHEN** um endpoint do Actum depende do Asana e o Asana responde `401`
- **THEN** a resposta do Actum MUST ter status `400` informando que o token do Asana é inválido
