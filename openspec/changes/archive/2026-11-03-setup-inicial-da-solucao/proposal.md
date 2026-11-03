# Proposal

## Why

O repositório do Actum ainda não tem código. A fundação de que ele precisa (monolito modular, convenções de banco, entidade base, Problem Details, CORS, health check e OpenAPI) já foi especificada, implementada e testada no Clavus (`E:\Ixact\Clavus\ClavusAPI`). Replicar essa fundação, em vez de redesenhá-la, deixa os dois projetos com o mesmo padrão e permite que as changes seguintes do Actum tratem só das regras de negócio.

## What Changes

- Criação da solução `Actum.slnx` com `global.json`, `Directory.Build.props`, `Directory.Packages.props` e `.editorconfig` equivalentes aos do Clavus.
- Projetos `Actum.Shared.Abstractions` (`IModule`, `DomainException`, `Entity`, `PagedResult`) e `Actum.Shared.Infrastructure` (`ModuleDbContext`, `EntityConvention`, `SnakeCaseHistoryRepository`, extensões de registro de banco), portados do Clavus com o namespace `Actum`.
- Host `Actum.Api` com OpenAPI + Scalar, health checks, Problem Details em pt-BR e CORS para o front (`http://localhost:5173`).
- Connection string `ConnectionStrings:Actum` para um PostgreSQL local (usuário e banco `actum`), com `docker-compose.yml` opcional.
- Projetos de testes do host e da infraestrutura compartilhada.

## Capabilities

### New Capabilities

- `health-check`: endpoints de liveness e readiness, esta incluindo a conectividade com o PostgreSQL.
- `api-documentation`: documento OpenAPI consumido pelo front.
- `database-conventions`: nomes em snake_case, datas sem timezone, schema e histórico de migrations por módulo.
- `base-entity`: colunas comuns (`id`, `identifier`, `create_in`, `update_in`, `is_active`) em toda tabela de negócio.
- `error-responses`: respostas de erro em Problem Details com títulos em pt-BR.
- `cross-origin-access`: CORS restrito às origens configuradas.

### Modified Capabilities

Nenhuma.

## Impact

- **Módulos afetados:** nenhum módulo de negócio. São criados o host `Actum.Api` e os projetos `Actum.Shared.*`.
- **Contrato da API:** não há contrato anterior, então não há breaking change. Surgem `/health/live`, `/health/ready` e `/openapi/v1.json`. Impacto no front (`..\ActumUI`): pode gerar tipos a partir do OpenAPI e chamar a API a partir de `http://localhost:5173`.
- **Dependências novas:** as mesmas do Clavus (Npgsql/EF Core, `EFCore.NamingConventions`, `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, xUnit v3, `Microsoft.AspNetCore.Mvc.Testing`), nas mesmas versões de `Directory.Packages.props` do Clavus.
- **Ambiente:** exige um PostgreSQL acessível com o usuário `actum`.

### Fora do escopo

- Módulos de negócio (`Asana`, `Tarefas`): changes seguintes.
- Autenticação (uso local por um único usuário).
- Pacote NuGet compartilhado entre Clavus e Actum: o código é copiado, não referenciado.
- Logging estruturado, observabilidade, CI/CD e deploy.
