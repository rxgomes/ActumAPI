# task-lifecycle Specification

## Purpose

Define o ciclo de vida da tarefa dentro do Actum: iniciar com as branches e o prazo interno, bloquear, ajustar prazo e prioridade, registrar branches e concluir, enviando ao Asana as tags de branch e a conclusão.

## Requirements

### Requirement: Iniciar tarefa
A API SHALL permitir iniciar uma tarefa com status `entrada` informando branch base, branch de implementação e prazo interno, e MUST NOT enviar nada ao Asana nesse momento.

#### Scenario: Início válido
- **WHEN** um cliente chama `POST /tarefas/{identifier}/iniciar` com base `main`, implementação `feature/ipi-devolvido` e prazo `2026-11-10` para uma tarefa em `entrada`
- **THEN** a tarefa MUST ficar `em_andamento`, com `emAndamento = true` e `prazo_interno = 2026-11-10`, as duas branches MUST ser registradas como não enviadas e nenhuma requisição MUST ser feita ao Asana

#### Scenario: Branches iguais
- **WHEN** a branch base e a de implementação têm o mesmo nome
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

#### Scenario: Campo ausente
- **WHEN** o prazo interno ou uma das branches não é informado
- **THEN** a resposta MUST ter status `400` com o campo em `errors`

#### Scenario: Tarefa que não está na entrada
- **WHEN** um cliente tenta iniciar uma tarefa `em_andamento`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR citando o status atual

### Requirement: Em andamento e pausada
Cada tarefa SHALL ter o indicador `emAndamento`, independente do status: `true` é "Em andamento" e `false` é "Pausada". Somente iniciar pela entrada e `POST /tarefas/{identifier}/retomar` MUST ligá-lo; `POST /tarefas/{identifier}/pausar`, bloquear, concluir e arquivar MUST desligá-lo. As demais transições (carga inicial, desbloqueio, reabertura e volta para a lista do Asana) MUST deixar a tarefa pausada. Nada é enviado ao Asana. Várias tarefas podem estar em andamento ao mesmo tempo.

#### Scenario: Pausar e retomar
- **WHEN** uma tarefa `em_andamento` em andamento é pausada e depois retomada
- **THEN** `emAndamento` MUST ficar `false` e depois `true`, sem mudar o status

#### Scenario: Retrabalho retomado
- **WHEN** uma tarefa em `retrabalho` pausada é retomada
- **THEN** ela MUST continuar em `retrabalho`, com `emAndamento = true`

#### Scenario: Carga inicial
- **WHEN** uma tarefa entra pela carga inicial da empresa
- **THEN** ela MUST ficar `em_andamento` com `emAndamento = false`

#### Scenario: Ação que não se aplica
- **WHEN** um cliente retoma uma tarefa já em andamento, bloqueada ou na entrada, ou pausa uma tarefa pausada
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

### Requirement: Bloquear e desbloquear
A API SHALL permitir bloquear uma tarefa `em_andamento` ou `retrabalho` e desbloqueá-la, retornando ao status que tinha antes do bloqueio. O bloqueio MUST pausar a tarefa, e o desbloqueio MUST mantê-la pausada.

#### Scenario: Bloqueio durante retrabalho
- **WHEN** uma tarefa em `retrabalho` é bloqueada e depois desbloqueada
- **THEN** ela MUST ficar `bloqueada` e, ao desbloquear, MUST voltar para `retrabalho`

### Requirement: Prazo interno e prioridade
A API SHALL permitir alterar o prazo interno e a prioridade (1 a 3) de qualquer tarefa que não esteja arquivada, sem enviar nada ao Asana.

#### Scenario: Alterar prazo interno
- **WHEN** um cliente chama `PATCH /tarefas/{identifier}` com `prazoInterno = 2026-11-12`
- **THEN** `prazo_interno` MUST ser `2026-11-12`, `prazo_asana` MUST permanecer igual e nenhuma requisição MUST ser feita ao Asana

#### Scenario: Prioridade inválida
- **WHEN** um cliente envia `prioridade = 5`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

#### Scenario: Tarefa arquivada
- **WHEN** um cliente tenta alterar o prazo de uma tarefa arquivada
- **THEN** a resposta MUST ter status `400`

### Requirement: Registro de branches
A API SHALL permitir registrar branches adicionais em tarefas `em_andamento`, `bloqueada` ou `retrabalho`, sem repetir nomes na mesma tarefa, e excluir apenas branches ainda não enviadas ao Asana.

#### Scenario: Branches na tarefa da carga inicial
- **WHEN** uma tarefa da carga inicial (`em_andamento`, sem branches) recebe uma branch `base` e uma `implementacao`
- **THEN** as duas MUST ser registradas como não enviadas

#### Scenario: Nova branch no retrabalho
- **WHEN** uma tarefa em `retrabalho` recebe a branch `fix/ipi-devolvido-pagamento`
- **THEN** a branch MUST ser registrada vinculada ao retrabalho aberto, e as branches anteriores MUST permanecer

#### Scenario: Nome repetido
- **WHEN** uma branch com nome já existente na tarefa é registrada
- **THEN** a resposta MUST ter status `409`

#### Scenario: Excluir branch já enviada
- **WHEN** um cliente tenta excluir uma branch já enviada ao Asana
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

### Requirement: Prévia da conclusão
A API SHALL expor `GET /tarefas/{identifier}/conclusao/previa`, que informa as tags que serão adicionadas no Asana e se a conclusão é permitida, com o motivo quando não for.

#### Scenario: Prévia sem branch de implementação
- **WHEN** uma tarefa `em_andamento` só tem a branch base
- **THEN** a prévia MUST indicar que a conclusão não é permitida, com motivo em pt-BR

### Requirement: Concluir tarefa
A API SHALL concluir uma tarefa `em_andamento`, `bloqueada` ou `retrabalho` que tenha ao menos uma branch base e uma de implementação. A conclusão MUST adicionar no Asana, como tags com o nome exato de cada branch, só as branches ainda não enviadas, e depois marcar a tarefa como concluída no Asana e no Actum.

#### Scenario: Primeira conclusão
- **WHEN** uma tarefa com as branches não enviadas `main` e `feature/ipi-devolvido` é concluída
- **THEN** as tags `main` e `feature/ipi-devolvido` MUST ser adicionadas à tarefa no Asana, a tarefa MUST ser concluída no Asana e, no Actum, MUST ficar `concluida` com `concluida_em` preenchido

#### Scenario: Retrabalho na mesma branch
- **WHEN** uma tarefa em `retrabalho` cujas branches já foram todas enviadas é concluída
- **THEN** nenhuma tag MUST ser adicionada, e a tarefa MUST ser concluída no Asana e no Actum

#### Scenario: Retrabalho com branch nova
- **WHEN** uma tarefa em `retrabalho` com `main` e `feature/ipi-devolvido` já enviadas e `fix/ipi-devolvido-pagamento` não enviada é concluída
- **THEN** somente a tag `fix/ipi-devolvido-pagamento` MUST ser adicionada, e as tags anteriores MUST permanecer na tarefa

#### Scenario: Tag já presente na tarefa
- **WHEN** uma branch não enviada tem o mesmo nome de uma tag que já está na tarefa no Asana
- **THEN** a tag MUST NOT ser adicionada de novo, e a branch MUST ser marcada como enviada

#### Scenario: Falha no meio da conclusão
- **WHEN** o Asana falha depois de a primeira tag ser adicionada e antes da conclusão
- **THEN** a tarefa MUST manter o status anterior, a primeira branch MUST ficar marcada como enviada e uma nova tentativa MUST adicionar só as tags restantes e concluir

#### Scenario: Sem branches obrigatórias
- **WHEN** uma tarefa sem branch de implementação é concluída
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, e nenhuma requisição de escrita MUST ser feita ao Asana

#### Scenario: Tarefa excluída no Asana
- **WHEN** a tarefa não existe mais no Asana no momento da conclusão
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, e o status local MUST permanecer igual
