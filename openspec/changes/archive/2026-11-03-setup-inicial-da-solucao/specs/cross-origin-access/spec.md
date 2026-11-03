# Spec Delta

## Purpose

Define quais origens de navegador podem chamar a API (CORS), para que o front em outra origem consiga consumi-la sem abrir a API a qualquer site.

## ADDED Requirements

### Requirement: Origens permitidas por configuração
A API SHALL aceitar requisições cross-origin apenas das origens listadas na configuração `Cors:AllowedOrigins`. Em `Development`, a lista padrão MUST conter `http://localhost:5173`.

#### Scenario: Origem permitida
- **WHEN** o navegador envia uma requisição com `Origin: http://localhost:5173` e essa origem está configurada
- **THEN** a resposta MUST conter `Access-Control-Allow-Origin: http://localhost:5173`

#### Scenario: Origem não permitida
- **WHEN** o navegador envia uma requisição com `Origin: https://site-desconhecido.com`
- **THEN** a resposta MUST NOT conter o cabeçalho `Access-Control-Allow-Origin`

### Requirement: Preflight para origens permitidas
A API SHALL responder às requisições de preflight (`OPTIONS` com `Access-Control-Request-Method`) de origens permitidas, liberando os métodos e cabeçalhos solicitados.

#### Scenario: Preflight de POST com JSON
- **WHEN** uma origem permitida envia `OPTIONS` com `Access-Control-Request-Method: POST` e `Access-Control-Request-Headers: content-type`
- **THEN** a resposta MUST ter status `204` e conter `Access-Control-Allow-Origin`, `Access-Control-Allow-Methods` com `POST` e `Access-Control-Allow-Headers` com `content-type`

#### Scenario: Preflight de origem não permitida
- **WHEN** uma origem não permitida envia um preflight
- **THEN** a resposta MUST NOT conter `Access-Control-Allow-Origin`

### Requirement: Nenhuma origem liberada por padrão fora de desenvolvimento
Fora de `Development`, sem `Cors:AllowedOrigins` configurado, a API MUST NOT liberar nenhuma origem, e o curinga `*` MUST NOT ser usado.

#### Scenario: Produção sem configuração
- **WHEN** a API roda em `Production` sem `Cors:AllowedOrigins` e recebe uma requisição com `Origin: http://localhost:5173`
- **THEN** a resposta MUST NOT conter `Access-Control-Allow-Origin`

### Requirement: CORS sem credenciais
A API MUST NOT enviar `Access-Control-Allow-Credentials: true` enquanto não houver autenticação.

#### Scenario: Requisição de origem permitida
- **WHEN** uma origem permitida faz uma requisição
- **THEN** a resposta MUST NOT conter `Access-Control-Allow-Credentials`
