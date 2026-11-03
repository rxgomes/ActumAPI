# Tasks

## 1. Actum.Modules.Tarefas: banco

- [x] 1.1 Criar a entidade `TarefaBranch` (enum `TipoBranch`) com configuração EF e índice único (`tarefa_id`, `nome`). Verificar com `dotnet build`.
- [x] 1.2 Gerar a migration `yyyyMMdd_TarefaBranch` (data da implementação) e aplicá-la. Verificar com `select * from tarefas.tarefa_branch`.

## 2. Actum.Modules.Tarefas: domínio

- [x] 2.1 Implementar na entidade `Tarefa` os métodos de transição (`Iniciar`, `Bloquear`, `Desbloquear`, `MarcarConcluida`), `DefinirPrazoInterno` (resolvendo a pendência do retrabalho) e `DefinirPrioridade`, lançando `DomainException` em pt-BR. Verificar com `dotnet build`.
- [x] 2.2 Implementar `AdicionarBranch`/`RemoverBranch` (vínculo com o retrabalho aberto, nome repetido → conflito, remoção só de não enviadas). Verificar com `dotnet build`.
- [x] 2.3 Declarar `ITarefaService` e `IConclusaoService` com XML doc completo. Verificar que `dotnet build` passa sem CS1591.
- [x] 2.4 Implementar `ConclusaoService` (prévia e conclusão conforme a seção 5 do design, salvando a cada tag). Verificar com `dotnet build`.

## 3. Endpoints

- [x] 3.1 Criar em `Endpoints/TarefaEndpoints`: `PATCH /tarefas/{identifier}`, `POST /tarefas/{identifier}/iniciar`, `/bloquear` e `/desbloquear`, `POST` e `DELETE` de `/tarefas/{identifier}/branches`, `GET /tarefas/{identifier}/conclusao/previa` e `POST /tarefas/{identifier}/concluir`, com DTOs em `Contracts/`, mensagens em pt-BR e metadados OpenAPI. Verificar em `/scalar/v1`.

## 4. Testes

- [x] 4.1 Testar os cenários de `task-lifecycle` com o `IAsanaClient` falso: início válido e inválido, bloqueio e desbloqueio, prazo e prioridade, branches (carga inicial, retrabalho, repetida, exclusão de enviada), prévia, primeira conclusão, retrabalho com mesma branch e com branch nova, tag já presente, falha no meio com retomada, sem branches obrigatórias e tarefa excluída no Asana. Verificar que `dotnet test` passa.
- [x] 4.2 Testar os cenários de `rework-tracking` desta change (novo prazo resolve a pendência; conclusão resolve o retrabalho). Verificar que `dotnet test` passa.

## Workflow follow-up

- Arquivar a change com `/opsx:archive` após revisão (depois de `sincronizacao-de-tarefas`).
