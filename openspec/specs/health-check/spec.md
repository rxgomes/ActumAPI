# health-check Specification

## Purpose

Permite que orquestradores, balanceadores e desenvolvedores verifiquem se a API está em execução e se está pronta para atender requisições, incluindo a disponibilidade do banco de dados.

## Requirements

### Requirement: Endpoint de liveness
A API SHALL expor `GET /health/live`, sem autenticação, que indica apenas que o processo está em execução, sem consultar dependências externas.

#### Scenario: Processo em execução
- **WHEN** um cliente chama `GET /health/live` com a API em execução
- **THEN** a resposta MUST ter status `200` e corpo `Healthy`

#### Scenario: Banco indisponível não afeta liveness
- **WHEN** o PostgreSQL está inacessível e um cliente chama `GET /health/live`
- **THEN** a resposta MUST continuar com status `200`

### Requirement: Endpoint de readiness
A API SHALL expor `GET /health/ready`, sem autenticação, que indica se a API está pronta para atender, verificando a conectividade com o PostgreSQL.

#### Scenario: Banco acessível
- **WHEN** o PostgreSQL está acessível e um cliente chama `GET /health/ready`
- **THEN** a resposta MUST ter status `200` e corpo `Healthy`

#### Scenario: Banco inacessível
- **WHEN** o PostgreSQL está inacessível e um cliente chama `GET /health/ready`
- **THEN** a resposta MUST ter status `503` e corpo `Unhealthy`

### Requirement: Health check não expõe detalhes internos
As respostas dos endpoints de health check MUST NOT conter connection strings, mensagens de exceção ou stack traces.

#### Scenario: Falha de conexão com o banco
- **WHEN** `GET /health/ready` falha por erro de conexão com o PostgreSQL
- **THEN** o corpo da resposta MUST conter apenas o status agregado (`Unhealthy`), sem detalhes do erro
