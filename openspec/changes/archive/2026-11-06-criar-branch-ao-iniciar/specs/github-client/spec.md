# Spec Delta

## Purpose

Define como o Actum acessa a API REST do GitHub em nome do usuário: autenticação por token, consulta do último commit de uma branch, criação de branch a partir de um commit e tradução das falhas do GitHub em respostas da API em pt-BR.

## ADDED Requirements

### Requirement: Autenticação por token configurado
O cliente do GitHub SHALL autenticar todas as requisições com `Authorization: Bearer <token>`, lendo o token da configuração `GitHub:Token`, e MUST enviar `Accept: application/vnd.github+json`, `X-GitHub-Api-Version` e um `User-Agent` próprio do Actum. O token MUST NOT ser registrado em logs nem devolvido em respostas da API.

#### Scenario: Token configurado
- **WHEN** `GitHub:Token` está configurado e uma operação do cliente é executada
- **THEN** a requisição ao GitHub MUST conter `Authorization: Bearer <token>` e os cabeçalhos `Accept`, `X-GitHub-Api-Version` e `User-Agent`

#### Scenario: Token ausente
- **WHEN** `GitHub:Token` não está configurado e uma operação do cliente é executada
- **THEN** o cliente MUST falhar com erro de token não configurado, com mensagem em pt-BR, sem chamar o GitHub

### Requirement: Último commit de uma branch
O cliente SHALL obter o SHA do último commit de uma branch de um repositório (`dono/repo`), informando quando a branch não existe.

#### Scenario: Branch existente
- **WHEN** o cliente consulta a branch `main` do repositório `ixact/erp-api` e o GitHub devolve a referência `refs/heads/main`
- **THEN** o cliente MUST devolver o SHA do commit dessa referência

#### Scenario: Branch com barra no nome
- **WHEN** o cliente consulta a branch `release/2026-11`
- **THEN** a requisição MUST usar a referência `heads/release/2026-11`, sem codificar a barra

#### Scenario: Branch inexistente
- **WHEN** o GitHub responde `404` para a referência da branch
- **THEN** o cliente MUST indicar que a branch não existe, sem lançar erro

### Requirement: Criação de branch
O cliente SHALL criar uma branch num repositório apontando para um commit informado, e MUST tratar uma branch que já existe como resultado distinto, sem erro.

#### Scenario: Branch criada
- **WHEN** o cliente cria `261105-consumidor-final-nf` em `ixact/erp-api` a partir do SHA `abc123` e o GitHub responde `201`
- **THEN** o cliente MUST indicar que a branch foi criada

#### Scenario: Branch já existente
- **WHEN** o GitHub responde `422` informando que a referência já existe
- **THEN** o cliente MUST indicar que a branch já existia, sem lançar erro

### Requirement: Falhas do GitHub
As falhas do GitHub SHALL virar respostas Problem Details com `detail` em pt-BR, em qualquer endpoint que use o cliente: token ausente ou recusado, ou repositório inexistente ou sem permissão, MUST virar `400`; GitHub inacessível, tempo esgotado ou resposta `5xx` MUST virar `502`.

#### Scenario: Token recusado
- **WHEN** o GitHub responde `401` durante o início de uma tarefa
- **THEN** a resposta da API MUST ter status `400` com mensagem em pt-BR dizendo que o token do GitHub foi recusado

#### Scenario: Repositório sem acesso
- **WHEN** o GitHub responde `404` ou `403` ao acessar o repositório `ixact/erp-api`
- **THEN** a resposta da API MUST ter status `400` com mensagem em pt-BR citando `ixact/erp-api`

#### Scenario: GitHub fora do ar
- **WHEN** o GitHub não responde dentro do tempo limite
- **THEN** a resposta da API MUST ter status `502` com mensagem em pt-BR
