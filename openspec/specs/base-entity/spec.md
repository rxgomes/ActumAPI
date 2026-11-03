# base-entity Specification

## Purpose

Define os campos de controle comuns a toda tabela de negócio (chave, identificador público, datas de criação e alteração e indicador de ativo), com tipos, defaults e preenchimento automático iguais em todos os módulos.

## Requirements

### Requirement: Colunas comuns em toda tabela de entidade
Toda tabela que representa uma entidade de negócio MUST conter as colunas `id`, `identifier`, `create_in`, `update_in` e `is_active`, sem que o módulo precise configurá-las.

#### Scenario: Tabela de uma entidade nova
- **WHEN** um módulo mapeia uma entidade de negócio sem configurar os campos comuns e gera a migration
- **THEN** a tabela MUST conter as colunas `id` (`bigint`), `identifier` (`uuid`), `create_in` (`timestamp without time zone`), `update_in` (`timestamp without time zone`) e `is_active` (`boolean`), todas `not null`

### Requirement: Chave primária identity
A coluna `id` MUST ser a chave primária da tabela e ter seu valor gerado pelo banco como identity, sequencial por tabela.

#### Scenario: Inclusão de registros
- **WHEN** dois registros são incluídos na mesma tabela sem informar `id`
- **THEN** cada um MUST receber um `id` gerado pelo banco, distinto e crescente na ordem de inclusão

### Requirement: Identificador UUID versão 7
A coluna `identifier` MUST conter um UUID versão 7, gerado na criação da entidade, e MUST ser única na tabela.

#### Scenario: Entidade criada pela aplicação
- **WHEN** uma entidade é criada e salva pela aplicação
- **THEN** o `identifier` gravado MUST ser um UUID versão 7 e MUST ser o mesmo valor que a entidade tinha antes de ser salva

#### Scenario: Registro incluído direto no banco
- **WHEN** um registro é incluído por SQL sem informar `identifier`
- **THEN** o banco MUST preencher `identifier` com um UUID versão 7

#### Scenario: Identificador duplicado
- **WHEN** um registro é incluído com um `identifier` já existente na tabela
- **THEN** o banco MUST rejeitar a inclusão

### Requirement: Data de criação
A coluna `create_in` MUST receber a data e hora local da inclusão e MUST NOT mudar em alterações posteriores do registro.

#### Scenario: Inclusão pela aplicação
- **WHEN** uma entidade é incluída pela aplicação
- **THEN** `create_in` MUST conter a data e hora do momento em que foi salva

#### Scenario: Alteração pela aplicação
- **WHEN** uma entidade existente é alterada e salva pela aplicação, mesmo que o código tente mudar `create_in`
- **THEN** `create_in` MUST manter o valor da inclusão

#### Scenario: Inclusão direto no banco
- **WHEN** um registro é incluído por SQL sem informar `create_in`
- **THEN** o banco MUST preencher `create_in` com a data e hora atuais

### Requirement: Data da última alteração
A coluna `update_in` MUST receber a data e hora local da inclusão e MUST ser atualizada para a data e hora atuais sempre que a entidade for alterada pela aplicação.

#### Scenario: Inclusão pela aplicação
- **WHEN** uma entidade é incluída pela aplicação
- **THEN** `update_in` MUST ser igual a `create_in`

#### Scenario: Alteração pela aplicação
- **WHEN** uma entidade existente tem qualquer campo alterado e é salva pela aplicação
- **THEN** `update_in` MUST conter a data e hora do momento da alteração, posterior a `create_in`

#### Scenario: Entidade lida sem alteração
- **WHEN** uma entidade é lida e salva sem nenhum campo alterado
- **THEN** `update_in` MUST NOT mudar

#### Scenario: Inclusão direto no banco
- **WHEN** um registro é incluído por SQL sem informar `update_in`
- **THEN** o banco MUST preencher `update_in` com a data e hora atuais

### Requirement: Indicador de ativo
A coluna `is_active` MUST ter valor padrão `true` e MUST aceitar e preservar o valor `false`.

#### Scenario: Entidade nova
- **WHEN** uma entidade é criada e salva pela aplicação sem informar o indicador de ativo
- **THEN** `is_active` MUST ser `true`

#### Scenario: Entidade inativa
- **WHEN** uma entidade é salva com o indicador de ativo `false` e lida de volta
- **THEN** o valor lido MUST ser `false`

#### Scenario: Inclusão direto no banco
- **WHEN** um registro é incluído por SQL sem informar `is_active`
- **THEN** o banco MUST preencher `is_active` com `true`
