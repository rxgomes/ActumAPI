## Purpose

Define os indicadores de retrabalho por empresa e período (totais, taxa, evolução mensal e ranking de tarefas) e o reprocessamento do histórico do Asana que alimenta esses indicadores.

## ADDED Requirements

### Requirement: Reprocessamento do histórico
A API SHALL expor `POST /retrabalhos/reprocessamento`, que lê do Asana o histórico de todas as tarefas do Actum e recalcula conclusões e retrabalhos de cada uma, devolvendo quantas tarefas foram lidas, quantos retrabalhos existiam antes e quantos existem depois. Uma tarefa que falha no Asana MUST NOT impedir as demais e MUST ser informada na resposta.

#### Scenario: Primeira execução
- **WHEN** o usuário chama `POST /retrabalhos/reprocessamento` com tarefas "(reabrindo)" que têm pares antigos no histórico
- **THEN** a resposta MUST ter status `200` com os totais, e as tarefas MUST passar a ter esses retrabalhos como histórico

#### Scenario: Sincronização em andamento
- **WHEN** uma sincronização ou outro reprocessamento está em andamento
- **THEN** a resposta MUST ter status `409`, sem chamar o Asana

#### Scenario: Tarefa excluída no Asana
- **WHEN** o histórico de uma tarefa não pode ser lido porque ela foi excluída no Asana
- **THEN** a tarefa MUST manter os retrabalhos que tinha e MUST aparecer na lista de falhas da resposta

### Requirement: Indicadores por empresa
A API SHALL expor `GET /retrabalhos/indicadores` com `de` e `ate` (datas obrigatórias, `de` ≤ `ate`, até 24 meses) e `empresa` (opcional), devolvendo por empresa e no total: retrabalhos com reabertura no período, tarefas com retrabalho no período, tarefas concluídas ao menos uma vez no período (pelas conclusões do histórico) e a taxa de retrabalho.

#### Scenario: Duas empresas
- **WHEN** no período a Aureum-ERP teve 10 tarefas concluídas, 3 delas reabertas (5 retrabalhos), e a aremas.com.br teve 4 concluídas e nenhuma reabertura
- **THEN** a Aureum-ERP MUST ter 5 retrabalhos, 3 tarefas com retrabalho, 10 concluídas e taxa `30.0`, e a aremas.com.br MUST ter 0, 0, 4 e taxa `0.0`

#### Scenario: Período inválido
- **WHEN** `de` é posterior a `ate`, falta uma das datas ou o período passa de 24 meses
- **THEN** a resposta MUST ter status `400` com o erro de validação em pt-BR

### Requirement: Taxa de retrabalho
A taxa SHALL ser o percentual, com uma casa decimal, das tarefas concluídas no período que foram reabertas depois de uma conclusão do período, e MUST ser nula quando não houver conclusões no período.

#### Scenario: Empresa sem conclusões
- **WHEN** a empresa não teve nenhuma conclusão no período
- **THEN** a taxa MUST ser nula

### Requirement: Evolução mensal
Os indicadores SHALL trazer, para cada mês do período (inclusive meses sem retrabalho), a quantidade de retrabalhos por empresa, pela data da reabertura.

#### Scenario: Mês sem retrabalho
- **WHEN** o período vai de agosto a outubro e só houve retrabalho em agosto e outubro
- **THEN** a evolução MUST ter os três meses, com setembro zerado

### Requirement: Ranking de tarefas
Os indicadores SHALL trazer as até 10 tarefas com mais retrabalhos no período, com `identifier`, título, empresa, status, quantidade de retrabalhos no período e data da última reabertura, ordenadas pela quantidade e, no empate, pela reabertura mais recente.

#### Scenario: Ranking
- **WHEN** no período a tarefa do IPI teve 3 retrabalhos e a de cenários fiscais teve 2
- **THEN** a tarefa do IPI MUST vir antes, e cada item MUST ter o `identifier` para abrir o detalhe

#### Scenario: Filtro por empresa
- **WHEN** `empresa` é informada
- **THEN** totais, evolução e ranking MUST considerar só as tarefas dessa empresa
