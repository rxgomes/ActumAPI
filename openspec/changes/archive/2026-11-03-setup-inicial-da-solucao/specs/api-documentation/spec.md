# Spec Delta

## Purpose

Publica o contrato HTTP da API em formato OpenAPI para que o front (`..\UI`) e outros consumidores conheçam endpoints, parâmetros e DTOs sem depender do código-fonte.

## ADDED Requirements

### Requirement: Documento OpenAPI publicado
A API SHALL publicar um documento OpenAPI em JSON em `GET /openapi/v1.json`, descrevendo os endpoints registrados pelo host e pelos módulos.

#### Scenario: Documento disponível em desenvolvimento
- **WHEN** a API está no ambiente `Development` e um cliente chama `GET /openapi/v1.json`
- **THEN** a resposta MUST ter status `200`, `Content-Type` JSON e um documento OpenAPI válido com o título `Actum API`

### Requirement: Documento OpenAPI restrito fora de desenvolvimento
O documento OpenAPI e sua interface de navegação MUST NOT ser expostos fora do ambiente `Development` enquanto não houver autenticação.

#### Scenario: Ambiente de produção
- **WHEN** a API está no ambiente `Production` e um cliente chama `GET /openapi/v1.json`
- **THEN** a resposta MUST ter status `404`

### Requirement: Interface de navegação da documentação
Em ambiente `Development`, a API SHALL oferecer uma interface web para navegar e testar os endpoints a partir do documento OpenAPI.

#### Scenario: Acesso à interface em desenvolvimento
- **WHEN** a API está no ambiente `Development` e um desenvolvedor acessa `GET /scalar/v1`
- **THEN** a resposta MUST ter status `200` e exibir a documentação baseada em `/openapi/v1.json`

### Requirement: Endpoints de health check fora do documento
Os endpoints de health check MUST NOT aparecer no documento OpenAPI, pois não fazem parte do contrato consumido pelo front.

#### Scenario: Conteúdo do documento
- **WHEN** um cliente obtém `/openapi/v1.json`
- **THEN** o documento MUST NOT conter os caminhos `/health/live` nem `/health/ready`
