# Tasks

## 1. Actum.Modules.Asana

- [x] 1.1 Criar o projeto `Actum.Modules.Asana` (referência a `Actum.Shared.Abstractions`), implementar `AsanaModule : IModule` e registrá-lo no `ModuleCatalog` do host. Verificar com `dotnet build`.
- [x] 1.2 Criar os records de `Contracts/` (`AsanaUsuario`, `AsanaWorkspace`, `AsanaTarefa`, `AsanaStory`, `AsanaStoryTipo`, `AsanaTag`) e a hierarquia `AsanaException` (`AsanaNaoConfiguradoException`, `AsanaAcessoNegadoException`, `AsanaIndisponivelException`) com mensagens em pt-BR. Verificar com `dotnet build`.
- [x] 1.3 Declarar `IAsanaClient` com XML doc completo (`<author>Ricardo Gomes</author>`, `<created>`, `<modified>`). Verificar que `dotnet build` passa sem CS1591.
- [x] 1.4 Implementar `AsanaClient` (HttpClient tipado com base e timeout, cabeçalho Bearer a partir de `Asana:Token`, envelope `data`/`next_page`, paginação, conversão UTC → local `Unspecified`, mapeamento de `resource_subtype`). Verificar com `dotnet build`.
- [x] 1.5 Implementar as novas tentativas em `429` com `Retry-After` (máx. 3) e a tradução de status para as exceções do módulo. Desligar o log de cabeçalhos do `HttpClient`. Verificar com `dotnet build`.
- [x] 1.6 Implementar `ObterOuCriarTagAsync` (comparação ordinal e criação quando não existe). Verificar com `dotnet build`.
- [x] 1.7 Registrar o handler de exceção que traduz `AsanaException` em Problem Details (`400`/`502`). Verificar com `dotnet build`.
- [x] 1.8 Criar `Endpoints/AsanaEndpoints` com `GET /asana/conexao` (tag OpenAPI `Asana`, `WithName`, `WithSummary` em pt-BR). Verificar com `curl` com o token configurado e sem ele.

## 2. Testes

- [x] 2.1 Criar `tests/Actum.Modules.Asana.Tests` com um `HttpMessageHandler` falso que registra as requisições e devolve respostas roteirizadas. Verificar com `dotnet test`.
- [x] 2.2 Testar os cenários de `asana-client`: Bearer presente, token ausente sem chamada HTTP, paginação, `404` → nulo, mapeamento de stories, tag existente/inexistente/maiúsculas, `PUT` de conclusão, `429` temporário e persistente. Verificar que `dotnet test` passa.
- [x] 2.3 Testar no `Actum.Api.Tests` a tradução para Problem Details (`503` do Asana → `502`; `401` → `400`; corpo sem o token) e os três estados de `GET /asana/conexao`. Verificar que `dotnet test` passa.

## 3. Documentação

- [x] 3.1 Incluir no `README.md` como gerar o Personal Access Token no Asana e configurar `dotnet user-secrets set "Asana:Token" ...`. Verificar seguindo o passo a passo.

## Workflow follow-up

- Arquivar a change com `/opsx:archive` após revisão.
