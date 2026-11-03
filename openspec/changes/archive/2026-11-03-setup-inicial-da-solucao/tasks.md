# Tasks

## 1. Solução e build

- [x] 1.1 Copiar do Clavus `global.json`, `Directory.Build.props`, `Directory.Packages.props` e `.editorconfig`. Verificar com `dotnet --version` na raiz, que deve retornar 10.0.x.
- [x] 1.2 Criar `Actum.slnx` com as pastas `src/Shared`, `src/Modules` (com `.gitkeep`) e `tests`. Verificar com `dotnet sln list`.
- [x] 1.3 Criar `scripts/criar-usuario-dev.sql` (usuário `actum` com `CREATEDB` e banco `actum`) e o `docker-compose.yml` opcional. Verificar conectando com `actum`/`actum` no PostgreSQL local.

## 2. Actum.Shared.Abstractions

- [x] 2.1 Portar `IModule`, `DomainException`, `Entity` e `PagedResult` com o namespace `Actum.Shared.Abstractions`, mantendo o XML doc (`<author>`, `<created>` e `<modified>` com a data da cópia). Verificar que `dotnet build` passa sem CS1591.

## 3. Actum.Shared.Infrastructure

- [x] 3.1 Portar `ModuleDbContext`, `EntityConvention`, `SnakeCaseHistoryRepository` e as extensões de banco, renomeando para `AddActumDatabase` e `ConnectionStrings:Actum`. Verificar com `dotnet build`.
- [x] 3.2 Portar `tests/Actum.Shared.Infrastructure.Tests` com `PostgresFixture` usando `ACTUM_TEST_CONNECTION_STRING` e o prefixo `actum_test_`. Verificar que `dotnet test` passa nos cenários de `database-conventions` e `base-entity`.

## 4. Actum.Api (host)

- [x] 4.1 Criar o host com `ModuleCatalog` vazio, OpenAPI com título `Actum API`, Scalar em `Development`, health checks, Problem Details em pt-BR e CORS (`http://localhost:5173`). Definir em `launchSettings.json` uma porta que não conflite com o Clavus. Verificar com `dotnet run` e `curl` em `/health/live`, `/health/ready` e `/openapi/v1.json`.
- [x] 4.2 Portar `tests/Actum.Api.Tests` com os cenários de `health-check`, `api-documentation`, `error-responses` e `cross-origin-access`. Verificar que `dotnet test` passa.

## 5. Documentação e verificação final

- [x] 5.1 Criar `README.md` (pré-requisitos, banco, user-secrets do Asana, como rodar API e testes, como adicionar um módulo). Verificar que os comandos funcionam como escritos.
- [x] 5.2 Verificação integrada: `dotnet build` sem warnings e `dotnet test` com todos os testes passando.

## Workflow follow-up

- Arquivar a change com `/opsx:archive` após revisão.
