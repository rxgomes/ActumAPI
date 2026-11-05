# task-board Specification

## Purpose

Define as consultas que o front usa para mostrar as tarefas: o painel agrupado por prioridade de ação, a listagem com filtros e o detalhe de uma tarefa com branches, retrabalhos e comentários.

## Requirements

### Requirement: Painel agrupado
A API SHALL expor `GET /tarefas/painel`, que devolve as tarefas não arquivadas nos grupos `retrabalho`, `entrada`, `atrasadas`, `hoje`, `esta_semana`, `proximas`, `sem_prazo` e `concluidas`, nessa ordem, com cada tarefa em um único grupo.

#### Scenario: Tarefa reaberta com prazo interno vencido
- **WHEN** uma tarefa em `retrabalho` tem prazo interno anterior a hoje
- **THEN** ela MUST aparecer somente no grupo `retrabalho`

#### Scenario: Tarefa da entrada com prazo no Asana
- **WHEN** uma tarefa em `entrada` tem prazo no Asana, mas não tem prazo interno
- **THEN** ela MUST aparecer no grupo `entrada`

#### Scenario: Tarefas por prazo interno
- **WHEN** hoje é quarta-feira `2026-11-04` e há tarefas `em_andamento` com prazo interno `2026-11-03`, `2026-11-04`, `2026-11-08` e `2026-11-09`
- **THEN** elas MUST aparecer, respectivamente, em `atrasadas`, `hoje`, `esta_semana` e `proximas`

#### Scenario: Tarefa sem prazo interno
- **WHEN** uma tarefa `em_andamento` não tem prazo interno
- **THEN** ela MUST aparecer em `sem_prazo`

#### Scenario: Tarefa bloqueada durante retrabalho
- **WHEN** uma tarefa reaberta é bloqueada
- **THEN** ela MUST continuar no grupo `retrabalho`, indicada como bloqueada

#### Scenario: Tarefas concluídas pelo Actum
- **WHEN** existem tarefas com status `concluida`
- **THEN** elas MUST aparecer somente no grupo `concluidas`, o último, da concluída mais recentemente para a mais antiga

#### Scenario: Tarefas arquivadas
- **WHEN** existem tarefas `arquivada` (inclusive as concluídas direto no Asana)
- **THEN** elas MUST NOT aparecer no painel

#### Scenario: Grupo vazio
- **WHEN** não há tarefas para um grupo
- **THEN** o grupo MUST ser devolvido com a lista vazia

### Requirement: Filtros do painel
O painel SHALL aceitar filtro por empresa, por status (`entrada`, `em_andamento`, `bloqueada`, `retrabalho` ou `concluida`) e por texto (título ou descrição, sem diferenciar maiúsculas e acentos). Sem filtro de empresa, MUST considerar todas as empresas.

#### Scenario: Filtro por status
- **WHEN** um cliente chama `GET /tarefas/painel?status=bloqueada`
- **THEN** somente tarefas `bloqueada` MUST ser devolvidas, com todos os grupos presentes

#### Scenario: Status fora do painel
- **WHEN** um cliente chama `GET /tarefas/painel?status=arquivada`
- **THEN** a resposta MUST ter status `400` com o campo `Status` em `errors`

#### Scenario: Filtro por empresa
- **WHEN** um cliente chama `GET /tarefas/painel?empresa={identifier da Aureum-ERP}`
- **THEN** somente tarefas da Aureum-ERP MUST ser devolvidas

#### Scenario: Busca sem acento
- **WHEN** um cliente busca `devolucao` e uma tarefa tem o título `Devolução de IPI`
- **THEN** a tarefa MUST ser devolvida

### Requirement: Dados do cartão
Cada tarefa do painel SHALL conter identificador, título, empresa (com nome e cor), projetos, status, prazo interno, prazo do Asana, indicador de prazo pendente, prioridade, quantidade de retrabalhos, quantidade de comentários não lidos e a data (`ultimoComentarioEm`, data e hora local) e o autor (`ultimoComentarioAutor`) do comentário mais recente da tarefa.

#### Scenario: Tarefa reaberta duas vezes com comentários novos
- **WHEN** uma tarefa tem dois retrabalhos e três comentários não lidos
- **THEN** o cartão MUST ter `quantidadeRetrabalhos = 2` e `comentariosNaoLidos = 3`

#### Scenario: Tarefa com comentários
- **WHEN** uma tarefa tem um comentário de Ana em `2026-11-03T09:00:00` e outro de Bruno em `2026-11-04T14:30:00`
- **THEN** o cartão MUST ter `ultimoComentarioEm = 2026-11-04T14:30:00` e `ultimoComentarioAutor = "Bruno"`, estejam os comentários lidos ou não

#### Scenario: Tarefa sem comentários
- **WHEN** uma tarefa não tem comentários
- **THEN** o cartão MUST ter `ultimoComentarioEm` e `ultimoComentarioAutor` nulos

#### Scenario: Último comentário com autor desconhecido
- **WHEN** o comentário mais recente de uma tarefa não tem autor
- **THEN** o cartão MUST ter `ultimoComentarioEm` com a data desse comentário e `ultimoComentarioAutor` nulo

#### Scenario: Comentário enviado pelo Actum
- **WHEN** o usuário envia um comentário pelo Actum e ele é o mais recente da tarefa
- **THEN** o cartão MUST trazer a data e o autor desse comentário

#### Scenario: Listagem paginada
- **WHEN** um cliente lista as tarefas em `GET /tarefas`
- **THEN** cada item MUST trazer `ultimoComentarioEm` e `ultimoComentarioAutor` com as mesmas regras do painel

### Requirement: Listagem paginada
A API SHALL expor `GET /tarefas` com filtros por empresa, status e texto, paginada (tamanho padrão 20, máximo 100), incluindo tarefas concluídas e arquivadas quando o filtro de status pedir.

#### Scenario: Consultar arquivadas
- **WHEN** um cliente chama `GET /tarefas?status=arquivada`
- **THEN** somente tarefas arquivadas MUST ser devolvidas, com o total de registros

#### Scenario: Tamanho acima do máximo
- **WHEN** um cliente pede `tamanho=500`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

### Requirement: Detalhe da tarefa
A API SHALL expor `GET /tarefas/{identifier}` com os dados da tarefa, as branches (indicando se já foram enviadas ao Asana), os retrabalhos em ordem e os comentários. Cada retrabalho MUST trazer os comentários feitos entre a sua reabertura e a reabertura seguinte.

#### Scenario: Motivo da reabertura
- **WHEN** uma tarefa foi reaberta em `2026-11-01` e o testador comentou em `2026-11-01` e `2026-11-02`
- **THEN** o retrabalho MUST trazer os dois comentários

#### Scenario: Tarefa inexistente
- **WHEN** um cliente chama `GET /tarefas/{identifier}` com um identificador que não existe
- **THEN** a resposta MUST ter status `404` no formato Problem Details

### Requirement: Marcar comentários como lidos
A API SHALL expor `POST /tarefas/{identifier}/comentarios/marcar-lidos`, que marca como lidos todos os comentários da tarefa.

#### Scenario: Leitura dos comentários
- **WHEN** uma tarefa com três comentários não lidos tem os comentários marcados como lidos
- **THEN** o cartão da tarefa no painel MUST ter `comentariosNaoLidos = 0`
