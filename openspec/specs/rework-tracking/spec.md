# rework-tracking Specification

## Purpose

Define como o Actum detecta que uma tarefa concluída foi reaberta no Asana e mantém o histórico de retrabalho, para que a tarefa não fique "atrasada" pelo prazo antigo e o usuário saiba quem reabriu, quando e por quê.

## Requirements

### Requirement: Detecção de reabertura
Ao ler o histórico de uma tarefa no Asana (sincronização ou reprocessamento), o Actum SHALL recalcular os retrabalhos: cada "marcou como não concluída" (`marked_incomplete`) precedido por um "concluiu esta tarefa" (`marked_complete`) desde a reabertura anterior é um retrabalho, com `concluida_em`, `reaberta_em`, `reaberta_por` e o `gid` da reabertura, numerado por `reaberta_em` a partir de 1. Reabertura sem conclusão antes MUST NOT ser retrabalho.

#### Scenario: Tarefa reaberta pelo testador
- **WHEN** o histórico de uma tarefa tem "concluiu" por Ricardo e depois "marcou como não concluída" por "Maria", e a sincronização é executada
- **THEN** MUST existir um retrabalho com `numero = 1`, `concluida_em` igual à data da conclusão, `reaberta_por = "Maria"` e `reaberta_em` igual à data da reabertura no Asana

#### Scenario: Segunda reabertura
- **WHEN** o histórico tem concluiu → não concluída → concluiu → não concluída
- **THEN** MUST existir dois retrabalhos, numerados 1 e 2 na ordem das reaberturas, e o primeiro MUST ter `resolvido_em` igual à data da segunda conclusão

#### Scenario: Reabertura sem conclusão antes
- **WHEN** o histórico tem "marcou como não concluída" sem nenhum "concluiu" antes dele (ou dois "não concluída" seguidos)
- **THEN** só a reabertura precedida por uma conclusão MUST contar como retrabalho

#### Scenario: Reabertura já registrada
- **WHEN** a sincronização é executada de novo sem novas stories
- **THEN** os retrabalhos MUST continuar os mesmos (mesmo `identifier`, número e dados), sem duplicar

#### Scenario: Reabertura sem registro no histórico
- **WHEN** uma tarefa `concluida` volta na lista de abertas e o histórico não tem reabertura posterior à conclusão
- **THEN** nenhum retrabalho MUST ser criado, e a tarefa MUST voltar para `em_andamento` com `prazo_interno_pendente = true`

### Requirement: Efeitos da reabertura na tarefa
Só uma reabertura nova (retrabalho que não existia antes do recálculo) com `reaberta_em` posterior à conclusão que o Actum conhece da tarefa (status `concluida`, pela `concluida_em` local, ou arquivada como `concluida_no_asana`, pela data de arquivamento) SHALL colocar a tarefa no status `retrabalho`, com `prazo_interno_pendente = true`, guardando o prazo interno vigente no retrabalho como prazo anterior. Os demais retrabalhos são histórico e MUST NOT mudar status nem prazo da tarefa.

#### Scenario: Prazo interno vencido
- **WHEN** uma tarefa `concluida` pelo Actum, com prazo interno `2026-10-20`, é reaberta no Asana e sincronizada em `2026-11-03`
- **THEN** a tarefa MUST ficar com status `retrabalho` e `prazo_interno_pendente = true`, e o retrabalho MUST ter `prazo_interno_anterior = 2026-10-20`

#### Scenario: Ciclo antigo em tarefa em andamento
- **WHEN** o reprocessamento encontra, numa tarefa `em_andamento`, um par concluiu → não concluída de antes da carga inicial
- **THEN** o retrabalho MUST ser registrado e a tarefa MUST continuar `em_andamento`, com o mesmo prazo interno

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

### Requirement: Retrabalhos sem par removidos
No recálculo, um retrabalho gravado que não corresponde a um par do histórico (inclusive os criados sem story de reabertura) SHALL ser removido; branches ligadas a ele MUST continuar existindo, sem o vínculo com o retrabalho.

#### Scenario: Retrabalho criado sem histórico
- **WHEN** a tarefa tem um retrabalho sem `asana_story_gid`, com uma branch ligada a ele, e o histórico é reprocessado
- **THEN** o retrabalho MUST ser removido e a branch MUST continuar na tarefa, com `retrabalho_id` nulo

#### Scenario: Retrabalho preservado
- **WHEN** um retrabalho gravado corresponde a um par do histórico
- **THEN** ele MUST ser mantido com o mesmo `identifier`, `prazo_interno_anterior`, `prazo_interno_novo` e `resolvido_em` definido pelo Actum, recebendo `concluida_em` e o número na ordem nova

### Requirement: Conclusões do histórico
O recálculo SHALL gravar cada story "concluiu esta tarefa" (`marked_complete`) da tarefa como uma conclusão (`gid` da story, data e autor), sem duplicar, para o cálculo da taxa de retrabalho.

#### Scenario: Conclusões gravadas
- **WHEN** o histórico tem três conclusões
- **THEN** a tarefa MUST ter três conclusões gravadas, e uma nova leitura do histórico MUST NOT duplicá-las
