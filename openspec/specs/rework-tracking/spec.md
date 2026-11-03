# rework-tracking Specification

## Purpose

Define como o Actum detecta que uma tarefa concluída foi reaberta no Asana e mantém o histórico de retrabalho, para que a tarefa não fique "atrasada" pelo prazo antigo e o usuário saiba quem reabriu, quando e por quê.

## Requirements

### Requirement: Detecção de reabertura
Quando uma tarefa com status `concluida` voltar na lista de tarefas abertas, a sincronização SHALL criar um registro de retrabalho para cada reabertura do histórico do Asana posterior à última conclusão feita pelo Actum.

#### Scenario: Tarefa reaberta pelo testador
- **WHEN** uma tarefa concluída pelo Actum é marcada como incompleta no Asana por "Maria" e a sincronização é executada
- **THEN** MUST existir um retrabalho com `numero = 1`, `reaberta_por = "Maria"` e `reaberta_em` igual à data da reabertura no Asana

#### Scenario: Segunda reabertura
- **WHEN** a mesma tarefa é concluída de novo pelo Actum, reaberta outra vez e sincronizada
- **THEN** MUST existir um novo retrabalho com `numero = 2`, e o primeiro MUST permanecer no histórico

#### Scenario: Reabertura já registrada
- **WHEN** a sincronização é executada de novo sem novas reaberturas
- **THEN** nenhum retrabalho novo MUST ser criado

#### Scenario: Reabertura sem registro no histórico
- **WHEN** uma tarefa `concluida` volta na lista de abertas e nenhuma story de reabertura posterior à conclusão é encontrada
- **THEN** MUST ser criado um retrabalho com `reaberta_em` igual à data da sincronização e `reaberta_por` nulo

### Requirement: Efeitos da reabertura na tarefa
Ao registrar um retrabalho, a tarefa SHALL passar para o status `retrabalho`, com `prazo_interno_pendente = true`, e o prazo interno vigente MUST ser guardado no retrabalho como prazo anterior.

#### Scenario: Prazo interno vencido
- **WHEN** uma tarefa com prazo interno `2026-10-20` é reaberta e sincronizada em `2026-11-03`
- **THEN** a tarefa MUST ficar com status `retrabalho` e `prazo_interno_pendente = true`, e o retrabalho MUST ter `prazo_interno_anterior = 2026-10-20`

### Requirement: Reabertura de tarefa arquivada por conclusão no Asana
Uma tarefa arquivada com motivo `concluida_no_asana` que for reaberta no Asana SHALL ser tratada como retrabalho, considerando a data de arquivamento no lugar da data de conclusão.

#### Scenario: Concluída direto no Asana e reaberta
- **WHEN** uma tarefa arquivada como `concluida_no_asana` é marcada como incompleta no Asana depois do arquivamento e a sincronização é executada
- **THEN** a tarefa MUST ficar com status `retrabalho` e MUST existir um retrabalho para essa reabertura

### Requirement: Novo prazo após reabertura
Quando uma tarefa com `prazo_interno_pendente = true` recebe um prazo interno, o prazo SHALL ser gravado também como `prazo_interno_novo` do retrabalho aberto, e a pendência MUST ser removida.

#### Scenario: Definir novo prazo
- **WHEN** uma tarefa reaberta, com prazo pendente, recebe `prazoInterno = 2026-11-07`
- **THEN** a tarefa MUST ficar com `prazo_interno = 2026-11-07` e `prazo_interno_pendente = false`, e o retrabalho aberto MUST ter `prazo_interno_novo = 2026-11-07`

### Requirement: Conclusão resolve o retrabalho
Ao concluir uma tarefa, o retrabalho aberto da tarefa, se houver, SHALL receber a data de resolução.

#### Scenario: Conclusão após retrabalho
- **WHEN** uma tarefa em `retrabalho` é concluída
- **THEN** o retrabalho aberto MUST ter `resolvido_em` preenchido com a data e hora da conclusão
