# Actum API

Monolito modular em .NET 10 (C#) com PostgreSQL + EF Core. Visão geral da arquitetura e das regras de especificação em `openspec/config.yaml`.

## Code style

**Blocos de controle de fluxo**
- Sempre use chaves `{ }` em `if`, `else`, `for`, `foreach`, `while`, mesmo com corpo de uma linha, em código novo ou em trechos que você estiver modificando.
- Não reformate `if`/`else`/`for`/`foreach`/`while` de uma linha já existentes em código que não faz parte da mudança atual — deixe o código legado como está.

**Documentação de interfaces**
- Toda interface pública e seus membros devem ter XML doc `<summary>` descrevendo o contrato (o que o método faz do ponto de vista de quem consome), não a implementação, conforme exemplo:

```csharp
/// <summary>
/// Check if any installment exists for the given invoice
/// </summary>
/// <param name="invoiceId">Invoice identifier</param>
/// <returns>True if any installment exists</returns>
/// <author>Ricardo Gomes</author>
/// <created>2026-03-19</created>
/// <modified>2026-03-19</modified>
```

- `<author>`, `<created>` e `<modified>` são obrigatórios em toda assinatura de interface nova. Ao alterar uma assinatura existente, atualize `<modified>` para a data da alteração — mantenha `<author>` e `<created>` originais.

**Nomenclatura**

| Elemento | Convenção | Exemplo |
|---|---|---|
| Classes, interfaces, records, structs | PascalCase | `PedidoService`, `IUsuarioRepository` |
| Interfaces | prefixo `I` | `IPedidoService` |
| Métodos, propriedades públicas | PascalCase | `CalcularTotal()`, `NomeCompleto` |
| Parâmetros, variáveis locais | camelCase | `usuarioId`, `valorTotal` |
| Campos privados | camelCase com `_` | `_logger`, `_contexto` |
| Constantes | PascalCase | `TaxaPadrao` |
| Métodos assíncronos | sufixo `Async` | `SalvarAsync()`, `ObterPorIdAsync()` |

- Manter consistência com o que já existe no arquivo/módulo sendo editado; não misturar idiomas de nomenclatura no mesmo arquivo.
- Evitar abreviações não óbvias.
- O nome da migration deve iniciar com o prefixo da data atual no seguinte formato `yyyyMMdd_`.

**Validação de entrada**
- Todo atributo de validação (`[Required]`, `[Range]`, `[StringLength]` etc.) em DTO de endpoint deve ter `ErrorMessage` em pt-BR. A mensagem padrão do DataAnnotations sai em inglês e chega ao usuário no campo `errors` da resposta 400.

```csharp
public sealed record NovoPedido(
    [property: Required(ErrorMessage = "O cliente é obrigatório.")] string? Cliente,
    [property: Range(1, 100, ErrorMessage = "A quantidade deve estar entre {1} e {2}.")] int Quantidade);
```

## Endpoints

- Endpoints em Minimal API (não usar controllers MVC). Cada módulo declara seus endpoints em classes estáticas próprias na pasta `Endpoints/` do módulo, uma por recurso (ex.: `PartnerEndpoints`), com um método de extensão `Map<Recurso>Endpoints(this IEndpointRouteBuilder)`.
- Cada classe cria o grupo do recurso com `MapGroup("/<recurso>")` e `WithTags("<Nome em pt-BR>")`, e cada endpoint declara `WithName`, `WithSummary` em pt-BR e os metadados de resposta (`Produces`, `ProducesProblem`, `ProducesValidationProblem`) para o OpenAPI.
- `IModule.MapEndpoints` só delega para essas classes. O host (`Actum.Api`) não declara endpoints de negócio.
- DTOs de requisição e resposta ficam em `Contracts/` do módulo. Cada módulo chama `services.AddValidation()` no seu `RegisterServices`, pois a validação é gerada por compilação.
- Regras de negócio violadas lançam `DomainException` (vira `400` com a mensagem no `detail`). "Não encontrado" e "conflito" são resultados do serviço, traduzidos pelo endpoint em `404`/`409`, não exceções.

## Banco de dados (PostgreSQL)

**Nomes sem aspas**
- Schemas, tabelas, colunas, índices e constraints devem ser criados em `snake_case` minúsculo (`pedido_item`, `data_criacao`), para que possam ser consultados sem aspas: `select data_criacao from vendas.pedido`.
- Usar o pacote `EFCore.NamingConventions` com `UseSnakeCaseNamingConvention()` em todos os `DbContext`. Não definir nomes com maiúsculas via `HasColumnName`/`ToTable`.

**Datas sem timezone**
- Colunas de data e hora usam `timestamp without time zone`; colunas só de data usam `date` (`DateOnly` no C#). Não usar `timestamp with time zone` (`timestamptz`).
- Configurar por convenção em cada `DbContext` (`ConfigureConventions`), mapeando `DateTime` para `timestamp without time zone`, em vez de configurar coluna a coluna.
