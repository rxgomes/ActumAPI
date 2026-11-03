# Tasks

## 1. Actum.Modules.Tarefas: banco

- [x] 1.1 Gerar a migration `yyyyMMdd_Unaccent` (`create extension if not exists unaccent`) e incluir a criação da extensão em `scripts/criar-usuario-dev.sql`. Verificar com `select unaccent('Devolução')`.

## 2. Actum.Modules.Tarefas: consultas

- [x] 2.1 Declarar `IPainelService` com XML doc completo. Verificar que `dotnet build` passa sem CS1591.
- [x] 2.2 Implementar o agrupamento e a ordenação do painel (seção 1 do design), com a data de hoje vinda de `TimeProvider`, para os testes. Verificar com `dotnet build`.
- [x] 2.3 Implementar a listagem paginada com `PagedResult` e a busca com `ILIKE` + `unaccent`. Verificar com `dotnet build`.
- [x] 2.4 Implementar o detalhe com branches, retrabalhos (com os comentários de cada janela de reabertura) e comentários, e a marcação de lidos. Verificar com `dotnet build`.

## 3. Endpoints

- [x] 3.1 Criar em `Endpoints/PainelEndpoints` e `Endpoints/TarefaEndpoints`: `GET /tarefas/painel`, `GET /tarefas`, `GET /tarefas/{identifier}` e `POST /tarefas/{identifier}/comentarios/marcar-lidos`, com DTOs em `Contracts/`, validação em pt-BR e metadados OpenAPI. Verificar em `/scalar/v1`.

## 4. Testes

- [x] 4.1 Testar os cenários de `task-board` com `TimeProvider` fixo em `2026-11-04`: todos os grupos, prioridade entre grupos, bloqueada em retrabalho, exclusão de concluídas e arquivadas, grupo vazio, filtros (empresa, busca sem acento), dados do cartão, listagem (arquivadas, limite de tamanho), detalhe (comentários por retrabalho, `404`) e marcar lidos. Verificar que `dotnet test` passa.

## Workflow follow-up

- Arquivar a change com `/opsx:archive` após revisão (depois de `ciclo-de-vida-da-tarefa`).
