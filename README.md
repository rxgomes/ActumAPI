# Actum API

Backend do Actum, organizador pessoal das demandas recebidas pelo Asana. É um monolito modular em .NET 10 com PostgreSQL e EF Core, para uso local por um único usuário (sem autenticação). O front-end fica em um repositório separado (`..\ActumUI`).

Os padrões de código estão em [`CLAUDE.md`](CLAUDE.md), as regras de arquitetura e especificação em [`openspec/config.yaml`](openspec/config.yaml) e a visão do produto em [`docs/especificacao-fase1.md`](docs/especificacao-fase1.md).

A fundação (projetos `Shared`, convenções de banco, entidade base, Problem Details, CORS e health checks) foi copiada do Clavus (`E:\Ixact\Clavus\ClavusAPI`). Ao corrigir algo nela em um dos projetos, avalie replicar no outro.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (versão fixada em `global.json`)
- PostgreSQL 18 ou superior (o default de `identifier` usa `uuidv7()`), instalado na máquina **ou** via Docker
- Ferramenta do EF Core: `dotnet tool install --global dotnet-ef`

## Banco de dados

A API e os testes usam o usuário `actum` com senha `actum` em `localhost:5432`. Essas credenciais valem apenas para desenvolvimento.

**PostgreSQL instalado na máquina.** Execute uma vez, como superusuário:

```bash
psql -h localhost -U postgres -f scripts/criar-usuario-dev.sql
```

No Windows, se o `psql` não estiver no `PATH`: `& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U postgres -f scripts/criar-usuario-dev.sql`.

O script cria o usuário `actum`, com a permissão `CREATEDB` usada pelos testes, e o banco `actum`.

**Docker.** Alternativa equivalente:

```bash
docker compose up -d
```

O container usa a porta `5432`. Não suba junto com um PostgreSQL local (ou com o container do Clavus) na mesma porta.

**Migrations.** A API não aplica migrations ao subir. Aplique as de cada módulo:

```bash
dotnet ef database update --project src/Modules/Actum.Modules.Tarefas --startup-project src/Actum.Api --context TarefasDbContext
```

## Token do Asana

O Actum acessa o Asana com um Personal Access Token (PAT) do seu usuário. Como o mesmo login é usado nos workspaces Aureum-ERP e aremas.com.br, um único token acessa os dois.

**1. Gerar o token no Asana**

1. Abra o console de desenvolvedor do Asana em <https://app.asana.com/0/my-apps>. Outro caminho é clicar na foto do perfil, ir em **Configurações** → **Apps** e escolher **Gerenciar apps de desenvolvedor**.
2. Em **Tokens de acesso pessoal**, clique em **Criar novo token**, dê um nome (ex.: `Actum`), aceite os termos e confirme.
3. Copie o token exibido. Ele só aparece uma vez; se perder, gere outro e revogue o anterior.

**2. Configurar no Actum**

O token fica em `dotnet user-secrets`, fora do repositório, na chave `Asana:Token`:

```bash
dotnet user-secrets set "Asana:Token" "<seu-token>" --project src/Actum.Api
dotnet user-secrets list --project src/Actum.Api
```

**3. Verificar**

Com a API rodando, chame `GET /asana/conexao`:

```bash
curl http://localhost:5180/asana/conexao
```

| Resposta | Significado |
|---|---|
| `"configurado": true, "valido": true` e os workspaces | Token aceito. |
| `"configurado": true, "valido": false` | O Asana recusou o token (revogado ou digitado errado). Gere outro e configure de novo. |
| `"configurado": false` | `Asana:Token` não está configurado ou a API não subiu em `Development`. |

Os user-secrets são carregados apenas em `Development`. O token nunca deve ser versionado, registrado em log nem devolvido pela API. Os logs do `HttpClient` do Asana omitem o valor de todos os cabeçalhos.

Quando um endpoint depende do Asana, as falhas viram Problem Details: token ausente ou recusado gera `400`, e Asana fora do ar, tempo esgotado ou limite de requisições esgotado (após 3 novas tentativas em `429`) gera `502`.

## Configurar a Claude API (análise com IA)

A análise da demanda (`POST /tarefas/{identifier}/analises`) usa a Claude API da Anthropic. Crie uma chave no Console da Anthropic (<https://platform.claude.com>, em **API keys**) e guarde-a em `dotnet user-secrets`, na chave `Claude:ApiKey`:

```bash
dotnet user-secrets set "Claude:ApiKey" "<sua-chave>" --project src/Actum.Api
```

Confira com `GET /claude/situacao` (`"configurado": true`). A chave nunca é versionada, registrada em log nem devolvida pela API.

| Configuração | Padrão | Uso |
|---|---|---|
| `Claude:Modelo` | `claude-opus-5-5` | Modelo das análises (ex.: `claude-sonnet-5-5` para baratear). |
| `Claude:Esforco` | `medium` | Profundidade do raciocínio: `low`, `medium`, `high` ou `max`. |
| `Claude:MaxTokens` | `16000` | Limite de tokens da resposta. |
| `Claude:NovasTentativas` | `2` | Novas tentativas do SDK em `429`, `529` e `5xx`. |

Cada análise é uma chamada paga e envia à Anthropic o conteúdo da tarefa (título, descrição, projetos, comentários e retrabalhos); ela só acontece quando o usuário pede. Se o modelo recusar por política, a Anthropic refaz a chamada com o modelo de fallback recomendado (`fallbacks: "default"`), e o modelo gravado na análise é o que respondeu. Chave ausente ou recusada, conta sem créditos e recusa do modelo geram `400`; Anthropic fora do ar, limite de uso e resposta incompleta geram `502`.

## Executar a API

```bash
dotnet run --project src/Actum.Api
```

No Rider, use a run configuration **`Actum.Api: http`**, que vem do `launchSettings.json`. Sem esse perfil, a API sobe como Production: a documentação não é exposta e a connection string de desenvolvimento não é carregada. No console deve aparecer `Hosting environment: Development`.

A porta é `5180`, diferente da do Clavus (`5080`), para que os dois possam rodar juntos.

| Endpoint | Descrição |
|---|---|
| `http://localhost:5180/scalar/v1` | Documentação navegável (somente em Development) |
| `http://localhost:5180/openapi/v1.json` | Documento OpenAPI (somente em Development) |
| `http://localhost:5180/health/live` | Processo no ar |
| `http://localhost:5180/health/ready` | Pronto para atender (inclui a conexão com o PostgreSQL) |

## CORS

O front chama a API direto do navegador, então a origem dele precisa estar liberada em `Cors:AllowedOrigins`. Em desenvolvimento, `appsettings.Development.json` já libera `http://localhost:5173`, a porta padrão do Vite. Para outra porta, ajuste a lista:

```json
{
  "Cors": {
    "AllowedOrigins": [ "http://localhost:5173" ]
  }
}
```

Sem essa configuração, nenhuma origem é liberada. O curinga `*` não é aceito e impede a API de subir. Requisições com credenciais (cookies) não são suportadas.

## Respostas de erro

Todo erro 4xx/5xx gerado pela API sai como `application/problem+json` (RFC 9457):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Recurso não encontrado.",
  "status": 404,
  "traceId": "00-033e64997bacc736c0aef759a550c880-e029f68082a32d2f-00"
}
```

- O `title` vem em pt-BR. Para decidir o que fazer com o erro, use `type` e `status`, não o texto.
- O `traceId` contém o `TraceId` registrado nos logs da requisição, o que permite achar o erro nos logs.
- Uma exceção não tratada gera `500`. A mensagem da exceção só vem em `detail` em Development.
- Uma `DomainException` (regra de negócio violada) gera `400` com a mensagem da regra em `detail`, em qualquer ambiente.
- Um erro de validação gera `400` com `errors`, um objeto que mapeia cada campo para a lista de mensagens:

  ```json
  { "title": "Um ou mais campos são inválidos.", "status": 400, "errors": { "Cliente": ["O cliente é obrigatório."] } }
  ```

Os health checks são a exceção: respondem em texto (`Healthy`/`Unhealthy`).

## Testes

```bash
dotnet test
```

Os testes de integração criam um banco temporário `actum_test_<data>_<guid>` e o removem no fim. Por padrão, conectam com o usuário `actum` no banco `postgres`. Para usar outro servidor ou usuário (que precisa de `CREATEDB`), defina:

```powershell
$env:ACTUM_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=postgres;Username=...;Password=..."
```

## Estrutura

```
src/
  Actum.Api/                       # host: compõe os módulos, OpenAPI, health checks, CORS, erros
  Shared/
    Actum.Shared.Abstractions/     # contratos públicos (IModule, Entity, DomainException, PagedResult)
    Actum.Shared.Infrastructure/   # base de persistência (ModuleDbContext, convenções de banco)
  Modules/                         # um projeto por módulo de negócio
    Actum.Modules.Asana/           # acesso à API do Asana (IAsanaClient), sem banco
    Actum.Modules.Tarefas/         # empresas, tarefas, comentários, retrabalho e sincronização (schema tarefas)
tests/
  Actum.Api.Tests/                 # health check, OpenAPI, erros, CORS e Asana no host
  Actum.Modules.Asana.Tests/       # cliente do Asana com HttpMessageHandler falso (sem rede)
  Actum.Modules.Tarefas.Tests/     # sincronização e retrabalho, com IAsanaClient falso e banco temporário
  Actum.Shared.Infrastructure.Tests/  # convenções de banco e entidade base
```

## Adicionar um módulo

1. Crie o projeto e adicione-o à solução:

   ```bash
   dotnet new classlib -o src/Modules/Actum.Modules.Relatorios
   dotnet sln add src/Modules/Actum.Modules.Relatorios --solution-folder src/Modules
   dotnet add src/Modules/Actum.Modules.Relatorios reference src/Shared/Actum.Shared.Abstractions src/Shared/Actum.Shared.Infrastructure
   dotnet add src/Actum.Api reference src/Modules/Actum.Modules.Relatorios
   ```

   Apague o `Class1.cs` gerado e remova `TargetFramework`, `ImplicitUsings` e `Nullable` do `.csproj`, porque eles vêm do `Directory.Build.props`.

2. Crie o DbContext herdando de `ModuleDbContext`, com schema próprio em snake_case:

   ```csharp
   public sealed class RelatoriosDbContext(DbContextOptions<RelatoriosDbContext> options) : ModuleDbContext(options)
   {
       public const string SchemaName = "relatorios";

       protected override string Schema => SchemaName;
   }
   ```

   Os nomes de tabelas e colunas saem em snake_case, e `DateTime` vira `timestamp without time zone` automaticamente. Não use `HasColumnName`/`ToTable` com maiúsculas nem `DateTime` com `Kind=Utc`.

3. Implemente `IModule`, registre o DbContext e delegue os endpoints para as classes de `Endpoints/`:

   ```csharp
   using Actum.Shared.Abstractions;
   using Actum.Shared.Infrastructure.Persistence;
   using Microsoft.AspNetCore.Routing;
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.DependencyInjection;

   public sealed class RelatoriosModule : IModule
   {
       public string Name => "Relatorios";

       public void RegisterServices(IServiceCollection services, IConfiguration configuration)
       {
           services.AddModuleDbContext<RelatoriosDbContext>(RelatoriosDbContext.SchemaName);
           services.AddValidation();
       }

       public void MapEndpoints(IEndpointRouteBuilder endpoints)
       {
           endpoints.MapRelatorioEndpoints();
       }
   }
   ```

   Cada recurso tem a sua classe estática em `Endpoints/`, com `MapGroup("/<recurso>")`, `WithTags` em pt-BR e, em cada endpoint, `WithName`, `WithSummary` e os metadados de resposta (veja `CLAUDE.md`).

4. Inclua `new RelatoriosModule()` em `src/Actum.Api/Modules/ModuleCatalog.cs`.

5. Gere a migration com o prefixo de data `yyyyMMdd_` e aplique-a:

   ```bash
   dotnet ef migrations add 20261103_Inicial --project src/Modules/Actum.Modules.Relatorios --startup-project src/Actum.Api --context RelatoriosDbContext
   dotnet ef database update --project src/Modules/Actum.Modules.Relatorios --startup-project src/Actum.Api --context RelatoriosDbContext
   ```

   O histórico de migrations do módulo fica em `relatorios.__ef_migrations_history`.

### Entidades

Toda classe que representa uma tabela herda de `Entity` (`Actum.Shared.Abstractions.Domain`):

```csharp
public sealed class Tarefa : Entity
{
    public string Titulo { get; set; } = string.Empty;
}
```

A tabela recebe as colunas abaixo sem nenhuma configuração no módulo:

| Propriedade | Coluna | Tipo | Comportamento |
|---|---|---|---|
| `Id` | `id` | `bigint` identity | chave primária gerada pelo banco |
| `Identifier` | `identifier` | `uuid` | UUID v7 gerado ao criar o objeto, com índice único; default `uuidv7()` |
| `CreateIn` | `create_in` | `timestamp without time zone` | hora local da inclusão, nunca muda; default `localtimestamp` |
| `UpdateIn` | `update_in` | `timestamp without time zone` | hora local da última alteração; default `localtimestamp` |
| `IsActive` | `is_active` | `boolean` | default `true` |

`CreateIn` e `UpdateIn` são preenchidos no `SaveChanges`: na inclusão, os dois recebem a mesma hora, e cada alteração atualiza só `UpdateIn`. Os defaults do banco valem para inclusões feitas por SQL.

> `ExecuteUpdate`/`ExecuteDelete` não passam pelo `SaveChanges`, então não atualizam `UpdateIn`. Defina o campo na própria expressão: `.ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false).SetProperty(p => p.UpdateIn, DateTime.Now))`.

### Regras de domínio

As validações ficam nas próprias entidades e objetos de valor (construtores e métodos), e não nos endpoints. Um dado inválido lança `DomainException` (`Actum.Shared.Abstractions.Domain`), com mensagem em pt-BR para o usuário, que chega ao front como `400`.

"Não encontrado" e "conflito" não são exceções: o serviço devolve esses resultados e o endpoint os traduz em `404`/`409`.

### Validação de entrada

A validação usa DataAnnotations nos DTOs (`Contracts/`) e roda antes do handler. Quando falha, a resposta é `400` no formato de erro descrito acima.

```csharp
public sealed record NovaTarefa(
    [property: Required(ErrorMessage = "O título é obrigatório.")] string? Titulo,
    [property: Range(1, 5, ErrorMessage = "A prioridade deve estar entre {1} e {2}.")] int Prioridade);
```

Sempre defina `ErrorMessage` em pt-BR, porque a mensagem padrão do DataAnnotations sai em inglês. Os marcadores `{0}` (nome do campo), `{1}` e `{2}` (limites) podem ser usados no texto.

> **Obrigatório:** chame `services.AddValidation()` no `RegisterServices` do módulo. A validação é gerada em tempo de compilação, por projeto. Sem essa chamada, os DTOs do módulo **não são validados**, e nenhum erro avisa disso.
