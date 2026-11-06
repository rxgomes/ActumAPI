# company-repositories Specification

## Purpose
Associa a cada empresa (workspace do Asana) um repositório de front e um de back no GitHub, usados para criar e conferir as branches das tarefas dessa empresa.

## Requirements

### Requirement: Repositórios da empresa
Cada empresa SHALL ter um repositório de front e um de back opcionais, no formato `dono/repo`, devolvidos em `GET /empresas` como `repositorioFront` e `repositorioBack` (`null` quando não definidos). Empresas criadas pela sincronização MUST começar sem repositórios.

#### Scenario: Empresa nova
- **WHEN** a sincronização cria a empresa do workspace aremas.com.br
- **THEN** `GET /empresas` MUST devolvê-la com `repositorioFront = null` e `repositorioBack = null`

### Requirement: Ajuste dos repositórios
`PATCH /empresas/{identifier}` SHALL aceitar `repositorioFront` e `repositorioBack`. Um valor informado MUST ser gravado sem espaços nas pontas; texto vazio MUST remover o repositório; campo não enviado MUST manter o valor atual. Um valor fora do formato `dono/repo` MUST ser recusado com `400` e o campo em `errors`.

#### Scenario: Definir os repositórios
- **WHEN** um cliente envia `repositorioFront = "ixact/erp-web"` e `repositorioBack = "ixact/erp-api"`
- **THEN** `GET /empresas` MUST devolver a empresa com esses dois repositórios

#### Scenario: Remover um repositório
- **WHEN** um cliente envia `repositorioBack = ""` para uma empresa que tinha `ixact/erp-api`
- **THEN** a empresa MUST ficar com `repositorioBack = null` e o repositório de front MUST permanecer

#### Scenario: Formato inválido
- **WHEN** um cliente envia `repositorioFront = "https://github.com/ixact/erp-web"`
- **THEN** a resposta MUST ter status `400` com `RepositorioFront` em `errors` e mensagem em pt-BR

#### Scenario: Só a cor
- **WHEN** um cliente envia apenas uma nova cor
- **THEN** os repositórios da empresa MUST permanecer iguais
