# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

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
