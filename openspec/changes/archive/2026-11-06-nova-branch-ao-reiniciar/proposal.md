# Proposal

## Why

Quando uma tarefa é reaberta no Asana, ela volta ao Actum em `retrabalho` com as branches da primeira implementação. Ao iniciá-la de novo, o Actum só faz `retomar` e não cria branch nenhuma: o usuário precisa usar "Nova branch" à parte, e o retrabalho acaba sendo feito na branch antiga. A mudança deixa o início de uma tarefa reaberta criar, opcionalmente, uma nova branch de implementação no GitHub (como já acontece no primeiro início, change `criar-branch-ao-iniciar`), para que cada ciclo da tarefa possa ter a sua branch.

Além disso, uma tarefa arquivada que volta para a entrada mantém as branches anteriores. Iniciá-la de novo com a mesma base hoje tenta gravar uma segunda branch com o mesmo nome e esbarra no índice único de `tarefa_branch`.

## What Changes

- `POST /tarefas/{identifier}/retomar` passa a aceitar um corpo opcional com `branchBase`, `branchImplementacao` e `partes`. Sem corpo, o comportamento é o de hoje. Com corpo, a API faz, numa única operação:
  - confere a base no repositório de cada parte;
  - cria nele a branch de implementação a partir do último commit da base (uma branch que já exista é aceita);
  - registra as branches vinculadas ao retrabalho aberto;
  - põe a tarefa em andamento.

  Se o GitHub recusar, nada é gravado e a tarefa continua pausada.
- A base informada pode ser uma base que a tarefa já tem. Nesse caso ela é só conferida no GitHub e não é registrada de novo.
- O início pela entrada (`POST /tarefas/{identifier}/iniciar`) também reaproveita uma base já registrada com o mesmo nome, o que corrige o início de uma tarefa desarquivada.
- `GET /tarefas/{identifier}` ganha `novaBranchSugerida`. O campo é `true` quando a tarefa está em `retrabalho` e o retrabalho aberto ainda não tem branch de implementação. O front usa esse campo para oferecer a nova branch ao iniciar.
- A conclusão não muda: as branches novas são enviadas como tags, como as demais branches ainda não enviadas.

Nenhuma mudança é breaking: o corpo de `retomar` é opcional e o campo novo do detalhe é aditivo.

## Capabilities

### New Capabilities
<!-- nenhuma -->

### Modified Capabilities
- `task-lifecycle`:
  - o retomar ganha a nova branch opcional;
  - o início pela entrada reaproveita a base já registrada;
  - o detalhe indica quando a nova branch é sugerida.

## Impact

- **Módulos**: só `Actum.Modules.Tarefas` (domínio `Tarefa`, `TarefaService`, `BranchesNoGitHub`, contratos e `TarefaEndpoints`). O `Actum.Modules.GitHub` é usado como está, sem dependência nova entre módulos.
- **Banco**: sem migration. As branches novas usam as colunas que já existem, inclusive `retrabalho_id`.
- **Contrato** (não breaking):
  - novo `RetomarTarefaRequest`, opcional, em `POST /tarefas/{identifier}/retomar`;
  - novo `novaBranchSugerida` em `TarefaDetalheResponse`.
- **Front (..\ActumUI)**: precisa regerar os tipos e fazer o Iniciar de uma tarefa em retrabalho abrir um diálogo para escolher entre "Continuar na branch atual" e "Criar nova branch" (change `nova-branch-ao-reiniciar` no ActumUI).
- **Dependência de change**: depende da `criar-branch-ao-iniciar` (cliente do GitHub, partes, `BranchesNoGitHub`). Esta change deve ser implementada e arquivada depois dela.

## Fora do escopo

- Tornar a nova branch obrigatória ao retomar.
- Pedir o novo prazo interno no mesmo diálogo. Ele continua sendo definido por "Novo prazo".
- Sugerir a nova branch fora do `retrabalho`, por exemplo numa tarefa concluída que volta para a lista sem reabertura no histórico ou numa tarefa pausada comum. Nesses casos o corpo de `retomar` é aceito, mas o detalhe não sugere a branch.
- Excluir ou arquivar no GitHub a branch do ciclo anterior.
- Fazer merge ou rebase da branch anterior na nova.
