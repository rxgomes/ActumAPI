## Purpose

Define as anotações privadas da tarefa: notas com data, editáveis e excluíveis, guardadas só no Actum e nunca enviadas ao Asana.

## ADDED Requirements

### Requirement: Listar anotações
A API SHALL expor `GET /tarefas/{identifier}/anotacoes`, que devolve as anotações da tarefa em ordem cronológica de criação, cada uma com `identifier`, `texto`, `criadaEm` e `alteradaEm` (nulo quando nunca alterada).

#### Scenario: Tarefa com anotações
- **WHEN** a tarefa tem duas anotações
- **THEN** a resposta MUST ter status `200` com as duas, da mais antiga para a mais recente

#### Scenario: Tarefa inexistente
- **WHEN** não existe tarefa com o `identifier`
- **THEN** a resposta MUST ter status `404`

### Requirement: Criar, alterar e excluir anotação
A API SHALL expor `POST /tarefas/{identifier}/anotacoes` (`201`), `PUT /tarefas/{identifier}/anotacoes/{anotacaoIdentifier}` (`200`) e `DELETE /tarefas/{identifier}/anotacoes/{anotacaoIdentifier}` (`204`), com `texto` obrigatório de até 10.000 caracteres (aparado). As anotações MUST ser aceitas em qualquer status da tarefa, inclusive arquivada, e MUST NOT ser enviadas ao Asana.

#### Scenario: Criar anotação
- **WHEN** o usuário cria a anotação "Testado com a NF 2958, ok"
- **THEN** a resposta MUST ter status `201` com a anotação, `criadaEm` na hora local e `alteradaEm` nulo, sem nenhuma chamada ao Asana

#### Scenario: Alterar anotação
- **WHEN** o usuário altera o texto de uma anotação
- **THEN** a resposta MUST ter status `200` com o texto novo e `alteradaEm` preenchido

#### Scenario: Texto vazio
- **WHEN** o texto é vazio ou só espaços
- **THEN** a resposta MUST ter status `400` com a mensagem em pt-BR no campo `texto`

#### Scenario: Anotação de outra tarefa
- **WHEN** a anotação não existe ou pertence a outra tarefa
- **THEN** `PUT` e `DELETE` MUST responder `404`
