# Tasks

## 1. Actum.Modules.Tarefas: estrutura e banco

- [x] 1.1 Criar o projeto `Actum.Modules.Tarefas` (referências a `Actum.Shared.*` e ao módulo `Asana`), `TarefasModule : IModule` e o registro no `ModuleCatalog`. Verificar com `dotnet build`.
- [x] 1.2 Criar as entidades `Empresa`, `Tarefa`, `Comentario`, `Retrabalho` e `Sincronizacao` (herdando de `Entity`), os enums `StatusTarefa` e `MotivoArquivamento` (gravados como texto em snake_case) e as configurações EF com índices únicos (`asana_workspace_gid`, `asana_gid`, `asana_story_gid`, `tarefa_id + numero`). Verificar com `dotnet build`.
- [x] 1.3 Criar `TarefasDbContext : ModuleDbContext` (schema `tarefas`) e a design-time factory. Verificar com `dotnet build`.
- [x] 1.4 Gerar a migration `20261103_Inicial` e aplicá-la no banco local. Verificar com `select * from tarefas.tarefa`, sem aspas.

## 2. Sincronização

- [x] 2.1 Declarar `ISincronizacaoService` com XML doc completo. Verificar que `dotnet build` passa sem CS1591.
- [x] 2.2 Implementar o upsert de empresas a partir de `ObterUsuarioAtualAsync` (paleta fixa de cores). Verificar com `dotnet build`.
- [x] 2.3 Implementar a sincronização por empresa (seção 3 do design): carga inicial, criação de tarefas `entrada`, atualização dos campos do Asana, comentários, arquivamento e desarquivamento, com uma transação por empresa. Verificar com `dotnet build`.
- [x] 2.4 Implementar a regra de reabertura (seção 4 do design), incluindo o fallback e a numeração. Verificar com `dotnet build`.
- [x] 2.5 Implementar o controle de concorrência (`SemaphoreSlim` singleton → resultado de conflito) e o registro em `sincronizacao`. Verificar com `dotnet build`.

## 3. Endpoints

- [x] 3.1 Criar `Endpoints/SincronizacaoEndpoints` (`POST /sincronizacoes`, `GET /sincronizacoes/ultima`) e `Endpoints/EmpresaEndpoints` (`GET /empresas`, `PATCH /empresas/{identifier}`), com DTOs em `Contracts/`, validação com mensagens em pt-BR e metadados OpenAPI. Verificar em `/scalar/v1`.

## 4. Testes

- [x] 4.1 Criar `tests/Actum.Modules.Tarefas.Tests` com `PostgresFixture` e um `IAsanaClient` falso configurável por cenário. Verificar com `dotnet test`.
- [x] 4.2 Testar os cenários de `task-sync`: empresas, empresa desligada, carga inicial (com prazo, sem prazo, comentários lidos, só uma vez), tarefa nova `entrada`, tarefa concluída antes do Actum, atualização sem tocar campos internos, idempotência, comentários novos não lidos, os três motivos de arquivamento, tarefa `concluida` não consultada, desarquivamento, falha parcial e `409`. Verificar que `dotnet test` passa.
- [x] 4.3 Testar os cenários de `rework-tracking`: primeira e segunda reabertura, idempotência, fallback, efeitos na tarefa (`prazo_interno_anterior`, pendência) e reabertura de arquivada por conclusão no Asana. Verificar que `dotnet test` passa.

## Workflow follow-up

- Arquivar a change com `/opsx:archive` após revisão.
