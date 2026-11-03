# Spec Delta

## Purpose

Garante que toda resposta de erro da API siga um formato único (Problem Details, RFC 9457), para que o front trate erros num só lugar e o suporte correlacione cada erro com os logs.

## ADDED Requirements

### Requirement: Formato Problem Details
Toda resposta de erro (status 4xx ou 5xx) gerada pela API SHALL ter `Content-Type: application/problem+json` e um corpo com `type`, `title`, `status` e `traceId`. O campo `status` MUST ser igual ao status HTTP da resposta.

#### Scenario: Campos obrigatórios
- **WHEN** qualquer requisição resulta em erro 4xx ou 5xx
- **THEN** o corpo MUST conter `type`, `title`, `status` (igual ao status HTTP) e `traceId` não vazio

### Requirement: Títulos em pt-BR
O campo `title` das respostas de erro geradas pela API MUST estar em pt-BR.

#### Scenario: Rota inexistente
- **WHEN** um cliente chama uma rota que não existe
- **THEN** o `title` MUST ser `Recurso não encontrado.`

### Requirement: Correlação com logs
O `traceId` da resposta de erro MUST ser o mesmo identificador de trace registrado nos logs da requisição.

#### Scenario: Exceção não tratada
- **WHEN** um endpoint lança uma exceção não tratada
- **THEN** o `traceId` da resposta MUST aparecer no log de erro dessa requisição

### Requirement: Exceções não tratadas
Uma exceção não tratada SHALL resultar em status `500` com `title` `Ocorreu um erro inesperado.`. Fora de `Development`, a resposta MUST NOT conter mensagem de exceção, tipo de exceção ou stack trace.

#### Scenario: Exceção em produção
- **WHEN** a API está em `Production` e um endpoint lança `InvalidOperationException("detalhe interno")`
- **THEN** a resposta MUST ter status `500` e o corpo MUST NOT conter `detalhe interno`, `InvalidOperationException` nem stack trace

#### Scenario: Exceção em desenvolvimento
- **WHEN** a API está em `Development` e um endpoint lança `InvalidOperationException("detalhe interno")`
- **THEN** a resposta MUST ter status `500` e o campo `detail` MUST conter `detalhe interno`

### Requirement: Rota inexistente e método não suportado
Uma rota inexistente SHALL resultar em `404`, e um método HTTP não suportado por uma rota existente SHALL resultar em `405`, ambos no formato Problem Details.

#### Scenario: Rota inexistente
- **WHEN** um cliente chama `GET /rota-que-nao-existe`
- **THEN** a resposta MUST ter status `404` no formato Problem Details

#### Scenario: Método não suportado
- **WHEN** um cliente chama `DELETE` numa rota que só aceita `GET`
- **THEN** a resposta MUST ter status `405` no formato Problem Details

### Requirement: Corpo de requisição inválido
Um corpo de requisição ilegível (JSON malformado ou de tipo incompatível) SHALL resultar em `400` no formato Problem Details, sem expor detalhes internos fora de `Development`.

#### Scenario: JSON malformado
- **WHEN** um cliente envia `{"nome": ` como corpo JSON para um endpoint que espera um objeto
- **THEN** a resposta MUST ter status `400` no formato Problem Details

### Requirement: Erros de validação por campo
Quando a entrada de um endpoint falhar na validação declarada, a API SHALL responder `400` com `title` `Um ou mais campos são inválidos.` e o campo `errors`, um objeto que mapeia o nome de cada campo inválido para a lista de mensagens.

#### Scenario: Campo obrigatório ausente
- **WHEN** um cliente envia um objeto sem um campo marcado como obrigatório
- **THEN** a resposta MUST ter status `400` e `errors` MUST conter a chave do campo com pelo menos uma mensagem

### Requirement: Health check mantém seu formato
Os endpoints `/health/live` e `/health/ready` MUST continuar respondendo em texto (`Healthy`/`Unhealthy`), inclusive com status `503`, sem conversão para Problem Details.

#### Scenario: Readiness indisponível
- **WHEN** o banco está inacessível e um cliente chama `GET /health/ready`
- **THEN** a resposta MUST ter status `503` e corpo `Unhealthy`, sem `Content-Type: application/problem+json`
