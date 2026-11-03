# database-conventions Specification

## Purpose

Garante que todos os objetos de banco criados pelos módulos sigam o mesmo padrão no PostgreSQL: nomes consultáveis sem aspas, datas sem timezone e isolamento de schema e migrations por módulo.

## Requirements

### Requirement: Nomes de objetos em snake_case minúsculo
Tabelas, colunas, chaves, índices e constraints criados pelas migrations de qualquer módulo MUST ter nomes em snake_case minúsculo, consultáveis sem aspas.

#### Scenario: Entidade com propriedades compostas
- **WHEN** um módulo mapeia uma entidade `PedidoItem` com a propriedade `DataCriacao` e gera a migration
- **THEN** o banco MUST conter a tabela `pedido_item` com a coluna `data_criacao`, e `select data_criacao from <schema>.pedido_item` MUST funcionar sem aspas

### Requirement: Datas e horas sem timezone
Propriedades de data e hora MUST ser persistidas como `timestamp without time zone`, e propriedades só de data como `date`, sem configuração por coluna. O tipo `timestamp with time zone` MUST NOT ser usado.

#### Scenario: Propriedade DateTime
- **WHEN** um módulo mapeia uma propriedade `DateTime` ou `DateTime?` sem configuração específica
- **THEN** a coluna gerada MUST ser do tipo `timestamp without time zone`

#### Scenario: Propriedade DateOnly
- **WHEN** um módulo mapeia uma propriedade `DateOnly`
- **THEN** a coluna gerada MUST ser do tipo `date`

#### Scenario: Gravação e leitura de DateTime
- **WHEN** um valor `DateTime` é gravado e lido de volta
- **THEN** o valor lido MUST ser igual ao gravado (mesma data e hora), sem conversão de fuso

### Requirement: Schema próprio por módulo
Os objetos de banco de cada módulo MUST ficar no schema do próprio módulo, com nome em snake_case minúsculo.

#### Scenario: Migration de um módulo
- **WHEN** as migrations de um módulo com schema `vendas` são aplicadas
- **THEN** todas as tabelas desse módulo MUST ser criadas no schema `vendas`

### Requirement: Histórico de migrations por módulo
Cada módulo MUST registrar suas migrations em uma tabela de histórico dentro do próprio schema, de modo que módulos possam ser migrados de forma independente.

#### Scenario: Dois módulos migrados
- **WHEN** as migrations de dois módulos com schemas diferentes são aplicadas no mesmo banco
- **THEN** cada schema MUST conter sua própria tabela `__ef_migrations_history`, com apenas as migrations daquele módulo
