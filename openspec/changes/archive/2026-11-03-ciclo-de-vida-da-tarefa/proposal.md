# Proposal

## Why

Hoje o usuário insere no Asana, à mão, as tags com a branch base e a branch de implementação, e o prazo das tarefas reabertas continua o antigo. Com o ciclo de vida no Actum, ele informa as branches ao iniciar e define o próprio prazo interno. O Actum envia as tags somente na conclusão, sem duplicar as que já estão na tarefa, e conclui a tarefa no Asana.

## What Changes

- Iniciar tarefa: informa branch base, branch de implementação e prazo interno, sem enviar nada ao Asana.
- Bloquear e desbloquear (aguardando resposta de alguém).
- Prazo interno e prioridade editáveis. Definir o prazo resolve a pendência criada pela reabertura.
- Registro de branches adicionais, inclusive uma branch nova durante o retrabalho, mantendo as anteriores.
- Prévia e execução da conclusão: adiciona no Asana só as tags ainda não enviadas e marca a tarefa como concluída. Se falhar no meio, a nova tentativa continua de onde parou.
- Nova tabela `tarefa_branch` no schema `tarefas`.

## Capabilities

### New Capabilities

- `task-lifecycle`: transições de status da tarefa no Actum (iniciar, bloquear, desbloquear, concluir), prazo interno, prioridade e branches.

### Modified Capabilities

- `rework-tracking`: definir o novo prazo interno resolve a pendência do retrabalho, e concluir a tarefa resolve o retrabalho aberto.

## Impact

- **Módulos afetados:** `Actum.Modules.Tarefas`. Usa `AdicionarTagAsync`, `ObterOuCriarTagAsync`, `ObterTarefaAsync` e `ConcluirTarefaAsync` do módulo `Asana`.
- **Contrato da API:** novos endpoints em `/tarefas/{identifier}` (não breaking). Impacto no front (`..\ActumUI`): diálogos Iniciar, Novo prazo, Nova branch e Concluir, e ações de bloquear e desbloquear.
- **Banco:** tabela `tarefa_branch` (migration nova no módulo `Tarefas`).

### Fora do escopo

- Alterar prazo, responsável, descrição ou tags no Asana fora da conclusão.
- Remover tags no Asana ao excluir uma branch (branch enviada não pode ser excluída).
- Ler a branch automaticamente do repositório Git (decisão: o usuário digita).
- Comentários e anexos (Fase 3).
