# Design

## Context

O módulo `Tarefas` já guarda tudo o que a IA precisa ler: título, descrição em texto (`descricao`), projetos, empresa, comentários (`comentario`) e retrabalhos (`retrabalho`). A integração externa existente (`Asana`) é um módulo sem banco que expõe um contrato público (`IAsanaClient`) e traduz as próprias exceções em Problem Details por um `IExceptionHandler`. A Fase 2 segue o mesmo desenho para a Anthropic.

Decisões do usuário: a análise é **sob demanda** (botão), o resultado é **gravado com versões e itens marcáveis**, e o contexto enviado é **completo** (inclui comentários e retrabalhos).

## Goals / Non-Goals

**Goals**
- Uma chamada à Claude API por análise, com saída estruturada (JSON Schema), sem parsing frágil de texto.
- Análises versionadas por tarefa, com itens marcáveis e marcações herdadas na reanálise.
- Falhas claras em pt-BR, sem gravar resultado parcial.

**Non-Goals**
- Conversa com a IA, streaming para o front, edição manual dos itens, envio ao Asana.

## Decisions

### 1. Novo módulo `Actum.Modules.Claude` (sem banco)

- Projeto `Actum.Modules.Claude` com `ClaudeModule : IModule`, registrado no `ModuleCatalog`.
- Pacote NuGet `Anthropic` (SDK oficial em C#). Alternativa descartada: `HttpClient` manual — o SDK já entrega tipos, novas tentativas com espera (2 por padrão) e exceções por status.
- `ClaudeOptions` (seção `Claude`): `ApiKey`, `Modelo` (padrão `claude-opus-5-5`), `Esforco` (padrão `medium`), `MaxTokens` (padrão `16000`).
- `AnthropicClient` criado por uma fábrica singleton a partir de `ClaudeOptions` (`new AnthropicClient { ApiKey = ... }`), só quando a chave existe; sem chave, o analisador lança `ClaudeNaoConfiguradoException` antes de qualquer chamada. A chave nunca entra em log, banco ou resposta.
- Contrato público (pasta `Contracts/` + interface em `Application/`):
  - `IAnalisadorDeDemanda.AnalisarAsync(DemandaParaAnalise, CancellationToken) → DemandaEstruturada`
  - `DemandaParaAnalise(Empresa, Titulo, Descricao, Projetos, IReadOnlyList<ComentarioDaDemanda>, IReadOnlyList<RetrabalhoDaDemanda>)`
  - `DemandaEstruturada(Objetivo, IReadOnlyList<string> Checklist, IReadOnlyList<string> Duvidas, Modelo, TokensEntrada, TokensSaida)`
  - `IClaudeSituacao` (ou método no analisador) para `GET /claude/situacao`.
- Endpoint `GET /claude/situacao` em `Endpoints/ClaudeEndpoints` (tag `Claude`), resposta `ClaudeSituacaoResponse(Configurado, Modelo)`.

**Dependência entre módulos:** `Tarefas → Claude` (contrato público), como `Tarefas → Asana`. `Claude` não conhece `Tarefas`: recebe um DTO próprio e devolve outro.

### 2. Chamada à Claude API

- `client.Beta.Messages.Create(...)` (namespace `Anthropic.Models.Beta.Messages`), não streaming: a saída esperada é curta (alguns parágrafos e listas) e `MaxTokens` 16000 fica dentro do limite recomendado para chamadas sem streaming. O timeout do SDK (10 min) cobre o tempo de pensamento.
- **Modelo e pensamento:** `claude-opus-5-5`, que pensa sempre (adaptive); `Thinking` é omitido e a profundidade é controlada por `OutputConfig.Effort = medium`, enviado explicitamente.
- **Saída estruturada:** `OutputConfig.Format = JsonOutputFormat { Schema = ... }` com:
  ```json
  { "type": "object", "additionalProperties": false,
    "required": ["objetivo", "checklist", "testes", "duvidas"],
    "properties": {
      "objetivo": { "type": "string" },
      "checklist": { "type": "array", "items": {
        "type": "object", "additionalProperties": false,
        "required": ["grupo", "texto", "vago"],
        "properties": { "grupo": { "type": "string" }, "texto": { "type": "string" }, "vago": { "type": "string" } } } },
      "testes":   { "type": "array", "items": { "type": "string" } },
      "duvidas":  { "type": "array", "items": { "type": "string" } } } }
  ```
  O JSON vem no `TextBlock` da resposta e é desserializado para um record interno; itens vazios são descartados e textos aparados; item sem `grupo` vai para `Geral` e `vago` vazio vira ausente.
- **Fallback do servidor (padrão adotado):** `Betas = ["server-side-fallback-2026-07-01"]` e `fallbacks: "default"`. Se o modelo recusar por política, a própria Anthropic refaz a chamada com o modelo de fallback recomendado na mesma requisição; o `Modelo` gravado é o `model` da resposta (quem de fato respondeu). Na implementação, confirmar no SDK o tipo que representa o valor `"default"`; se o SDK instalado não o expuser, seguir sem fallback e registrar a pendência (a recusa continua tratada abaixo).
- **Motivo de parada, verificado antes do conteúdo:** `end_turn` → usa o JSON; `refusal` → `ClaudeRecusaException` (400); `max_tokens` ou JSON inválido → `ClaudeRespostaIncompletaException` (502).
- **Prompt:**
  - *System* (pt-BR): papel de assistente que organiza demandas de desenvolvimento de software recebidas pelo Asana; regras de saída — objetivo em 1 a 3 frases do resultado esperado; checklist com ações verificáveis e curtas, cada uma com o `grupo` (área ou camada com nome tirado do contexto, como `Front`, `Back: cálculo`, `Back: integração TecnoSpeed`; um único `Geral` quando não há áreas distintas), grupos e itens na ordem de execução, e `vago` explicando o que está vago no pedido daquela ação (sem inventar requisitos ausentes); testes com os cenários e dados de referência citados (notas, filiais, ambientes), sem repeti-los no checklist; dúvidas apenas sobre lacunas ou contradições reais no texto, formuladas como perguntas a quem pediu; tudo em pt-BR; não repetir o título.
  - *User*: contexto em blocos XML — `<empresa>`, `<titulo>`, `<projetos>`, `<descricao>`, `<comentarios>` (cada `<comentario autor data>`), `<retrabalhos>` (cada `<retrabalho numero reaberta_em reaberta_por>`). Com retrabalho, uma instrução final pede para priorizar o que foi solicitado a partir da reabertura mais recente (comentários desde essa data), mantendo no checklist só o que ainda falta.
  - O prompt fica em uma classe `PromptDaDemanda` testável sem rede.
- **Erros do SDK** (`Anthropic.Exceptions`), do mais específico ao mais genérico: `AnthropicUnauthorizedException`/`AnthropicForbiddenException` → `ClaudeChaveInvalidaException` (400); `AnthropicRateLimitException`, `Anthropic5xxException`, `AnthropicIOException` → `ClaudeIndisponivelException` (502); `AnthropicApiException` cuja mensagem (o SDK inclui o corpo da resposta) indica saldo de créditos insuficiente → `ClaudeSemCreditoException` (400); `AnthropicBadRequestException` e demais `AnthropicApiException` → `ClaudeIndisponivelException` com log do status (sem corpo da requisição). As novas tentativas ficam a cargo do SDK (2, padrão).
- `ClaudeExceptionHandler : IExceptionHandler` traduz `ClaudeException` em Problem Details (`400`/`502`), como `AsanaExceptionHandler`.
- Log: modelo, tokens e duração de cada análise; nunca chave, prompt ou resposta.

### 3. Persistência no módulo `Tarefas` (schema `tarefas`)

Entidades herdando de `Entity` (id, identifier, create_in, update_in, is_active por convenção):

**`tarefas.analise`**
| coluna | tipo | observação |
|---|---|---|
| `tarefa_id` | `bigint` FK `tarefa` | |
| `versao` | `smallint` | único com `tarefa_id` |
| `objetivo` | `text` | |
| `modelo` | `varchar(100)` | modelo que respondeu |
| `tokens_entrada` | `integer` | |
| `tokens_saida` | `integer` | |
| `analisada_em` | `timestamp without time zone` | hora local |

**`tarefas.analise_item`**
| coluna | tipo | observação |
|---|---|---|
| `analise_id` | `bigint` FK `analise` (cascade) | |
| `tipo` | `varchar(20)` | `checklist`, `teste` ou `duvida` (enum `TipoItemAnalise` em snake_case, `EnumSnakeCaseConverter`) |
| `ordem` | `smallint` | único com `analise_id` + `tipo` |
| `texto` | `text` | |
| `grupo` | `varchar(100)` null | área do item do checklist; nulo em testes e dúvidas |
| `vago` | `text` null | o que está vago no pedido do item do checklist; nulo quando está claro |
| `concluido_em` | `timestamp without time zone` null | marcado quando preenchido |

- Navegação `Tarefa → Analises` não é necessária; `Analise` tem `TarefaId` e a coleção privada de `Itens`.
- Migrations `20261103_Analise` (tabelas) e `20261103_AnaliseGrupos` (colunas `grupo` e `vago`, acrescentadas depois de ver o primeiro resultado real), sem `timestamptz`, nomes pelo `UseSnakeCaseNamingConvention`.
- Resposta: `checklist` é a lista de grupos (`GrupoDoChecklistResponse(Nome, Itens)`) na ordem da primeira ocorrência de cada grupo; itens antigos sem grupo aparecem em `Geral`. `testes` e `duvidas` são listas de itens.

### 4. Serviço e endpoints em `Tarefas`

- `IAnaliseService` (XML doc com `<author>`, `<created>`, `<modified>`):
  - `AnalisarAsync(Guid tarefaIdentifier)` → resultado `NaoEncontrada | EmAndamento | Criada(AnaliseResponse)`; tarefa arquivada lança `DomainException`.
  - `ListarAsync(Guid tarefaIdentifier)` → `NaoEncontrada | Lista`.
  - `MarcarItemAsync(Guid tarefaIdentifier, Guid itemIdentifier, bool concluido)` → `NaoEncontrado | Item`; item de versão antiga lança `DomainException`.
- Fluxo de `AnalisarAsync`: lê tarefa, empresa, comentários e retrabalhos (sem rastreamento) → monta `DemandaParaAnalise` → chama o analisador **fora de transação** (a chamada pode levar dezenas de segundos) → grava a nova versão com `versao = max + 1` e as marcações herdadas da versão anterior. O índice único `tarefa_id + versao` protege contra gravação dupla.
- Concorrência: `ConcurrentDictionary<long, byte>` singleton com as tarefas em análise; `TryAdd` falhou → `409`. Liberado no `finally`.
- Marcação herdada: comparação por `tipo` + texto normalizado (trim, minúsculas, sem acentos via `NormalizationForm.FormD`), herdando `concluido_em`.
- Hora local via `TimeProvider` (já usado em `PainelService`).
- `Endpoints/AnaliseEndpoints` (grupo `/tarefas/{identifier:guid}/analises`, tag `Análises`): `POST /` (`201`, `400`, `404`, `409`, `502`), `GET /` (`200`, `404`), `PATCH /itens/{itemIdentifier:guid}` (`200`, `400`, `404`), com `WithName`, `WithSummary` em pt-BR e metadados de resposta.
- Contracts: `AnaliseResponse(Identifier, Versao, Atual, Objetivo, Checklist, Duvidas, Modelo, TokensEntrada, TokensSaida, AnalisadaEm)`, `AnaliseItemResponse(Identifier, Tipo, Ordem, Texto, Concluido, ConcluidoEm)`, `MarcarItemRequest([Required(ErrorMessage = "Informe se o item está concluído.")] bool? Concluido)`.

## Risks / Trade-offs

- **Custo por chamada** (Opus, esforço `medium`): mitigado por ser sob demanda; tokens gravados por análise; modelo e esforço configuráveis (ex.: `claude-sonnet-5-5` para baratear).
- **Dados de terceiros enviados à Anthropic** (comentários de colegas): decisão explícita do usuário; documentado na proposta.
- **Tempo de resposta** (dezenas de segundos): front mostra estado de carregamento; sem transação aberta durante a chamada.
- **Tipo do `fallbacks: "default"` no SDK C#** pode não estar exposto na versão instalada: verificado na implementação, com saída documentada.
- **Testes sem rede:** o analisador real não roda nos testes automáticos (custaria dinheiro). Cobertura por testes do `PromptDaDemanda`, do mapeamento de motivo de parada/JSON e do serviço com `IAnalisadorDeDemanda` falso. Um teste manual com a chave real fica condicionado à autorização do usuário.

## Migration Plan

1. Adicionar o módulo `Claude` e o pacote `Anthropic`.
2. Gerar e aplicar `20261103_Analise`.
3. Configurar a chave: `dotnet user-secrets set "Claude:ApiKey" "<chave>" --project src/Actum.Api`.
Rollback: remover a migration (`dotnet ef migrations remove`/`database update 20261103_Unaccent`) e o módulo do `ModuleCatalog`.

## Open Questions

Nenhuma bloqueante.
