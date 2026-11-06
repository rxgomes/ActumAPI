# task-lifecycle Specification

## Purpose

Define o ciclo de vida da tarefa dentro do Actum: iniciar com as branches e o prazo interno, bloquear, ajustar prazo e prioridade, registrar branches e concluir, enviando ao Asana as tags de branch e a conclusão.

## Requirements

### Requirement: Iniciar tarefa
A API SHALL permitir iniciar uma tarefa com status `entrada` informando branch base, branch de implementação, prazo interno e as partes (`front` e/ou `back`). Para cada parte, a API MUST conferir se a base existe no repositório da parte na empresa e criar nele a branch de implementação a partir do último commit da base. Nada MUST ser enviado ao Asana nesse momento.

#### Scenario: Início válido
- **WHEN** um cliente chama `POST /tarefas/{identifier}/iniciar` com base `main`, implementação `261105-consumidor-final-nf`, prazo `2026-11-10` e partes `front` e `back` para uma tarefa em `entrada` de uma empresa com os dois repositórios definidos
- **THEN** a branch `261105-consumidor-final-nf` MUST ser criada nos dois repositórios a partir do último commit de `main`, a tarefa MUST ficar `em_andamento`, com `emAndamento = true` e `prazo_interno = 2026-11-10`, as duas branches MUST ser registradas como não enviadas e com as partes `front` e `back`, e nenhuma requisição MUST ser feita ao Asana

#### Scenario: Só uma parte
- **WHEN** a tarefa é iniciada só com a parte `back`
- **THEN** a branch de implementação MUST ser criada só no repositório de back, e as duas branches MUST ser registradas só com a parte `back`

#### Scenario: Branch de implementação já existente no repositório
- **WHEN** a branch de implementação já existe no repositório de uma parte, por exemplo numa nova tentativa depois de uma falha
- **THEN** a API MUST aceitar a branch existente e seguir com o início

#### Scenario: Base já registrada na tarefa
- **WHEN** uma tarefa desarquivada, de volta à `entrada` com as branches `main` (base) e `261020-ajuste-nf` (implementação) já registradas, é iniciada com base `main` e implementação `261106-ajuste-nf`
- **THEN** a API MUST conferir `main` no GitHub sem registrá-la de novo, MUST criar e registrar só `261106-ajuste-nf`, e as branches anteriores MUST permanecer

#### Scenario: Implementação já registrada na tarefa
- **WHEN** a branch de implementação informada tem o mesmo nome de uma branch já registrada na tarefa
- **THEN** a resposta MUST ter status `409`, sem chamar o GitHub, e a tarefa MUST permanecer na `entrada`

#### Scenario: Base inexistente
- **WHEN** a base `release/2026-12` não existe no repositório de back
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR citando a base e o repositório, nenhuma branch MUST ser criada em nenhum repositório e a tarefa MUST permanecer na `entrada`

#### Scenario: Parte sem repositório
- **WHEN** a parte `front` é informada e a empresa não tem `repositorioFront`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR dizendo que a empresa não tem repositório de front, sem chamar o GitHub

#### Scenario: Falha do GitHub ao criar
- **WHEN** a branch é criada no repositório de front e o GitHub falha ao criá-la no de back
- **THEN** a resposta MUST seguir as regras de falha do GitHub, a tarefa MUST permanecer na `entrada` sem branches registradas, e uma nova tentativa MUST aceitar a branch já criada no front

#### Scenario: Branches iguais
- **WHEN** a branch base e a de implementação têm o mesmo nome
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem chamar o GitHub

#### Scenario: Campo ausente
- **WHEN** o prazo interno, uma das branches ou as partes (lista vazia) não são informados
- **THEN** a resposta MUST ter status `400` com o campo em `errors`

#### Scenario: Tarefa que não está na entrada
- **WHEN** um cliente tenta iniciar uma tarefa `em_andamento`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR citando o status atual, sem chamar o GitHub

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
A API SHALL permitir registrar branches adicionais em tarefas `em_andamento`, `bloqueada` ou `retrabalho`, informando o tipo, o nome e as partes, sem repetir nomes na mesma tarefa, e excluir apenas branches ainda não enviadas ao Asana. Uma branch `base` MUST existir no repositório de cada parte. Uma branch `implementacao` MUST ser criada no repositório de cada parte a partir do último commit da base registrada mais recentemente na tarefa, aceitando uma branch que já exista. Excluir a branch da tarefa MUST NOT excluí-la no GitHub.

#### Scenario: Branches na tarefa da carga inicial
- **WHEN** uma tarefa da carga inicial (`em_andamento`, sem branches) recebe uma branch `base` `main` e depois uma `implementacao`, as duas com a parte `back`
- **THEN** a API MUST conferir que `main` existe no repositório de back, criar nele a branch de implementação a partir de `main`, e as duas MUST ser registradas como não enviadas

#### Scenario: Nova branch no retrabalho
- **WHEN** uma tarefa em `retrabalho` com base `main` recebe a branch `implementacao` `fix/ipi-devolvido-pagamento` com a parte `front`
- **THEN** a branch MUST ser criada no repositório de front a partir de `main`, MUST ser registrada vinculada ao retrabalho aberto e com a parte `front`, e as branches anteriores MUST permanecer

#### Scenario: Implementação sem base
- **WHEN** uma tarefa sem nenhuma branch `base` recebe uma branch `implementacao`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR pedindo a branch base antes, sem chamar o GitHub

#### Scenario: Base inexistente no registro
- **WHEN** a branch `base` informada não existe no repositório de uma das partes
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR citando a branch e o repositório, e a branch MUST NOT ser registrada

#### Scenario: Partes ausentes
- **WHEN** a branch é registrada sem partes
- **THEN** a resposta MUST ter status `400` com `Partes` em `errors`

#### Scenario: Nome repetido
- **WHEN** uma branch com nome já existente na tarefa é registrada
- **THEN** a resposta MUST ter status `409`, sem chamar o GitHub

#### Scenario: Excluir branch já enviada
- **WHEN** um cliente tenta excluir uma branch já enviada ao Asana
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

### Requirement: Prévia da conclusão
A API SHALL expor `GET /tarefas/{identifier}/conclusao/previa`, que informa as tags que serão adicionadas no Asana e se a conclusão é permitida, com o motivo quando não for.

#### Scenario: Prévia sem branch de implementação
- **WHEN** uma tarefa `em_andamento` só tem a branch base
- **THEN** a prévia MUST indicar que a conclusão não é permitida, com motivo em pt-BR

### Requirement: Concluir tarefa
A API SHALL concluir uma tarefa `em_andamento`, `bloqueada` ou `retrabalho` que tenha ao menos uma branch base e uma de implementação. A conclusão MUST adicionar no Asana, como tags com o texto da tag de cada branch, só as branches ainda não enviadas, e depois marcar a tarefa como concluída no Asana e no Actum.

#### Scenario: Primeira conclusão
- **WHEN** uma tarefa com as branches não enviadas `main` e `261105-consumidor-final-nf`, as duas com as partes `front` e `back`, é concluída
- **THEN** as tags `(Front, Back) => main` e `(Front, Back) => 261105-consumidor-final-nf` MUST ser adicionadas à tarefa no Asana, a tarefa MUST ser concluída no Asana e, no Actum, MUST ficar `concluida` com `concluida_em` preenchido

#### Scenario: Branches sem partes
- **WHEN** uma tarefa com as branches não enviadas `main` e `feature/ipi-devolvido`, registradas sem partes antes desta mudança, é concluída
- **THEN** as tags `main` e `feature/ipi-devolvido` MUST ser adicionadas à tarefa no Asana

#### Scenario: Retrabalho na mesma branch
- **WHEN** uma tarefa em `retrabalho` cujas branches já foram todas enviadas é concluída
- **THEN** nenhuma tag MUST ser adicionada, e a tarefa MUST ser concluída no Asana e no Actum

#### Scenario: Retrabalho com branch nova
- **WHEN** uma tarefa em `retrabalho` com `main` e `261105-consumidor-final-nf` já enviadas e `fix/ipi-devolvido-pagamento`, com a parte `front`, não enviada é concluída
- **THEN** somente a tag `(Front) => fix/ipi-devolvido-pagamento` MUST ser adicionada, e as tags anteriores MUST permanecer na tarefa

#### Scenario: Tag já presente na tarefa
- **WHEN** uma branch não enviada tem o texto da tag igual ao de uma tag que já está na tarefa no Asana
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

### Requirement: Partes e tag da branch
Cada branch SHALL guardar as suas partes (`front`, `back`), devolvidas em `partes`, e o texto da sua tag no Asana, devolvido em `tag`. Com partes, a tag MUST ser `(` + as partes como `Front` e `Back`, nessa ordem e separadas por `, ` + `) => ` + o nome. Sem partes, a tag MUST ser o nome.

#### Scenario: Duas partes
- **WHEN** a branch `main` tem as partes `back` e `front`
- **THEN** `partes` MUST ser `["front", "back"]` e `tag` MUST ser `(Front, Back) => main`

#### Scenario: Uma parte
- **WHEN** a branch `261105-consumidor-final-nf` tem só a parte `back`
- **THEN** `tag` MUST ser `(Back) => 261105-consumidor-final-nf`

#### Scenario: Branch anterior sem partes
- **WHEN** a branch `feature/ipi-devolvido` foi registrada sem partes
- **THEN** `partes` MUST ser `[]` e `tag` MUST ser `feature/ipi-devolvido`

#### Scenario: Prévia com as tags novas
- **WHEN** um cliente chama `GET /tarefas/{identifier}/conclusao/previa` para uma tarefa com as branches não enviadas `main` e `261105-consumidor-final-nf`, com as partes `front` e `back`
- **THEN** a prévia MUST listar `(Front, Back) => main` e `(Front, Back) => 261105-consumidor-final-nf`

### Requirement: Nome válido para o Git
Ao iniciar uma tarefa ou registrar uma branch, a API SHALL recusar com `400` e mensagem em pt-BR um nome que o Git não aceita como branch: com espaço, `~`, `^`, `:`, `?`, `*`, `[`, `\`, `..` ou `@{`, começando por `-` ou `/`, ou terminando em `/`, `.` ou `.lock`. Nesse caso o GitHub MUST NOT ser chamado.

#### Scenario: Nome com espaço
- **WHEN** um cliente inicia uma tarefa com a implementação `consumidor final nf`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR sobre o nome da branch, sem chamar o GitHub

#### Scenario: Nome com barra
- **WHEN** um cliente registra a branch `feature/ipi-devolvido`
- **THEN** o nome MUST ser aceito

### Requirement: Bases recentes da empresa
A API SHALL expor `GET /tarefas/{identifier}/branches-base-recentes`, que devolve até 3 nomes distintos de branches `base` das tarefas da mesma empresa da tarefa informada, da registrada mais recentemente para a mais antiga.

#### Scenario: Várias bases usadas
- **WHEN** as tarefas da empresa registraram as bases `main`, `release/2026-11`, `main`, `develop` e `hotfix`, nessa ordem
- **THEN** a resposta MUST ser `["hotfix", "develop", "main"]`

#### Scenario: Outra empresa
- **WHEN** só tarefas de outra empresa registraram a base `master`
- **THEN** `master` MUST NOT aparecer na resposta

#### Scenario: Nenhuma base usada
- **WHEN** nenhuma tarefa da empresa tem branch base
- **THEN** a resposta MUST ser uma lista vazia com status `200`

#### Scenario: Tarefa inexistente
- **WHEN** o identificador não corresponde a nenhuma tarefa
- **THEN** a resposta MUST ter status `404`

### Requirement: Nova branch ao retomar
`POST /tarefas/{identifier}/retomar` SHALL aceitar um corpo opcional com `branchBase`, `branchImplementacao` e `partes`. Com corpo, a API MUST conferir a base e criar a implementação no repositório de cada parte, como no início, registrar as branches vinculadas ao retrabalho aberto e pôr a tarefa em andamento, tudo ou nada. Sem corpo, o retomar MUST continuar como antes.

#### Scenario: Retrabalho com nova branch
- **WHEN** uma tarefa em `retrabalho` pausada, com a base `main` já registrada, é retomada com base `main`, implementação `261106-ajuste-ipi` e a parte `back`
- **THEN** `261106-ajuste-ipi` MUST ser criada no repositório de back a partir do último commit de `main`, MUST ser registrada como não enviada, com a parte `back` e vinculada ao retrabalho aberto, `main` MUST NOT ser registrada de novo, e a tarefa MUST continuar em `retrabalho` com `emAndamento = true`

#### Scenario: Base nova no retrabalho
- **WHEN** a tarefa é retomada com a base `release/2026-11`, que ainda não está registrada nela
- **THEN** a base MUST ser conferida no GitHub e registrada vinculada ao retrabalho aberto, junto com a implementação

#### Scenario: Retomar sem corpo
- **WHEN** um cliente chama `POST /tarefas/{identifier}/retomar` sem corpo
- **THEN** a tarefa MUST ser posta em andamento sem chamar o GitHub e sem registrar branches

#### Scenario: Base inexistente ao retomar
- **WHEN** a base informada não existe no repositório de uma das partes
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR citando a base e o repositório, nenhuma branch MUST ser criada nem registrada e a tarefa MUST continuar pausada

#### Scenario: Falha do GitHub ao retomar
- **WHEN** o GitHub falha ao criar a branch de implementação
- **THEN** a resposta MUST seguir as regras de falha do GitHub, nada MUST ser gravado e a tarefa MUST continuar pausada

#### Scenario: Implementação repetida ao retomar
- **WHEN** a branch de implementação informada já está registrada na tarefa
- **THEN** a resposta MUST ter status `409`, sem chamar o GitHub, e a tarefa MUST continuar pausada

#### Scenario: Base registrada como implementação
- **WHEN** a base informada é o nome de uma branch de implementação da tarefa
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem chamar o GitHub

#### Scenario: Corpo incompleto
- **WHEN** o corpo é enviado sem a branch de implementação, sem a base ou com as partes vazias
- **THEN** a resposta MUST ter status `400` com o campo em `errors`

#### Scenario: Retomar com corpo fora do retomar
- **WHEN** um cliente retoma com corpo uma tarefa já em andamento, bloqueada ou na entrada
- **THEN** a resposta MUST ter status `400` com a mesma mensagem do retomar sem corpo, sem chamar o GitHub

### Requirement: Nova branch sugerida no detalhe
`GET /tarefas/{identifier}` SHALL devolver `novaBranchSugerida`. O campo MUST ser `true` quando a tarefa está em `retrabalho` e o retrabalho aberto mais recente não tem nenhuma branch de implementação vinculada, e `false` nos demais casos.

#### Scenario: Retrabalho recém-aberto
- **WHEN** a sincronização põe em `retrabalho` uma tarefa concluída que foi reaberta no Asana
- **THEN** o detalhe MUST devolver `novaBranchSugerida = true`

#### Scenario: Retrabalho com branch nova
- **WHEN** a tarefa em `retrabalho` já tem uma branch de implementação vinculada ao retrabalho aberto, registrada pelo retomar ou por "Nova branch"
- **THEN** o detalhe MUST devolver `novaBranchSugerida = false`

#### Scenario: Tarefa fora do retrabalho
- **WHEN** a tarefa está `em_andamento`, `bloqueada`, na `entrada` ou `concluida`
- **THEN** o detalhe MUST devolver `novaBranchSugerida = false`
