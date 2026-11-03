# Tasks

## 1. Actum.Modules.Claude: estrutura e configuração

- [x] 1.1 Criar o projeto `Actum.Modules.Claude` (referências a `Actum.Shared.*`, pacote `Anthropic`), `ClaudeModule : IModule` com `services.AddValidation()` e o registro no `ModuleCatalog`; adicionar à solution e criar `tests/Actum.Modules.Claude.Tests`. Verificar com `dotnet build`.
- [x] 1.2 Criar `ClaudeOptions` (seção `Claude`: `ApiKey`, `Modelo` = `claude-opus-5-5`, `Esforco` = `medium`, `MaxTokens` = 16000) e a fábrica singleton do `AnthropicClient`, que só cria o cliente com chave configurada. Verificar com `dotnet build`.
- [x] 1.3 Criar `Errors/ClaudeException` (`ClaudeNaoConfiguradoException`, `ClaudeChaveInvalidaException`, `ClaudeSemCreditoException`, `ClaudeRecusaException`, `ClaudeRespostaIncompletaException`, `ClaudeIndisponivelException`, mensagens em pt-BR) e `ClaudeExceptionHandler` (400/502). Verificar com `dotnet build`.

## 2. Actum.Modules.Claude: analisador

- [x] 2.1 Declarar `IAnalisadorDeDemanda` com XML doc completo (`<author>`, `<created>` e `<modified>` = 2026-11-03) e os DTOs `DemandaParaAnalise`, `ComentarioDaDemanda`, `RetrabalhoDaDemanda`, `DemandaEstruturada` em `Contracts/`. Verificar que `dotnet build` passa sem CS1591.
- [x] 2.2 Implementar `PromptDaDemanda` (system em pt-BR, contexto em blocos XML, destaque do retrabalho mais recente) e o JSON Schema da saída. Verificar com `dotnet build`.
- [x] 2.3 Implementar `AnalisadorDeDemanda` com `client.Beta.Messages.Create`: modelo e esforço da configuração, `OutputConfig.Format` com o schema, `server-side-fallback-2026-07-01` + `fallbacks: "default"` (confirmar no SDK o tipo do valor `"default"`; se indisponível, seguir sem fallback e anotar), verificação de `StopReason` antes do conteúdo, desserialização, descarte de itens vazios, tradução das exceções do SDK e log sem chave/prompt/resposta. Verificar com `dotnet build`.
- [x] 2.4 Criar `Endpoints/ClaudeEndpoints` com `GET /claude/situacao` (`ClaudeSituacaoResponse`), tag `Claude` e metadados OpenAPI. Verificar em `/scalar/v1`.

## 3. Actum.Modules.Tarefas: banco

- [x] 3.1 Adicionar a referência ao módulo `Claude`; criar as entidades `Analise` e `AnaliseItem` e o enum `TipoItemAnalise`, com configurações EF (índices únicos `tarefa_id + versao` e `analise_id + tipo + ordem`, FK em cascata dos itens). Verificar com `dotnet build`.
- [x] 3.2 Gerar a migration `20261103_Analise` e aplicá-la no banco local. Verificar com `select * from tarefas.analise_item`, sem aspas, e que as colunas de data são `timestamp without time zone`.

## 4. Actum.Modules.Tarefas: serviço e endpoints

- [x] 4.1 Declarar `IAnaliseService` com XML doc completo e implementar `AnalisarAsync` (contexto completo, chamada fora de transação, nova versão, marcações herdadas, controle de concorrência por tarefa → `409`, arquivada → `DomainException`). Verificar com `dotnet build`.
- [x] 4.2 Implementar `ListarAsync` e `MarcarItemAsync` (só versão atual; data local via `TimeProvider`). Verificar com `dotnet build`.
- [x] 4.3 Criar `Endpoints/AnaliseEndpoints` (`POST`, `GET`, `PATCH /itens/{itemIdentifier}`) e os DTOs `AnaliseResponse`, `AnaliseItemResponse`, `MarcarItemRequest` (mensagens em pt-BR), com metadados OpenAPI. Verificar em `/scalar/v1`.

## 5. Testes

- [x] 5.1 `Actum.Modules.Claude.Tests`: `PromptDaDemanda` (todos os blocos, escape de XML, destaque do retrabalho), mapeamento do motivo de parada (`end_turn`, `refusal`, `max_tokens`), JSON inválido, descarte de itens vazios e chave ausente sem chamada externa. Verificar que `dotnet test` passa.
- [x] 5.2 `Actum.Modules.Tarefas.Tests` com `IAnalisadorDeDemanda` falso: primeira análise, contexto enviado (comentários em ordem, retrabalhos), reanálise com versão 2 e versão 1 intacta, marcações herdadas (maiúsculas/acentos), `404`, arquivada `400`, `409` concorrente, falha da IA sem gravar versão, marcar/desmarcar, item de versão antiga `400`, item de outra tarefa `404`. Verificar que `dotnet test` passa.
- [x] 5.3 `Actum.Api.Tests`: `GET /claude/situacao` com e sem chave (sem expor a chave), Problem Details `400`/`502` das exceções `Claude*` e validação de `concluido` em pt-BR. Verificar que `dotnet test` passa.
- [x] 5.4 Teste manual com a chave real (somente com autorização do usuário, pois a chamada é paga): analisar uma tarefa real e conferir objetivo, checklist, dúvidas, modelo e tokens gravados.

## 6. Ajustes após o primeiro resultado real

- [x] 6.1 Conta sem créditos: `ClaudeSemCreditoException` (`400`, mensagem com orientação), cenário na spec e testes no módulo e na API. Verificar que `dotnet test` passa.
- [x] 6.2 Formato da análise no módulo `Claude`: JSON Schema e prompt com checklist agrupado por área (`grupo`), aviso de trecho vago por item (`vago`) e `testes` separados; `DemandaEstruturada` com `ItemDoChecklist` e `Testes`. Verificar com `dotnet build`.
- [x] 6.3 Módulo `Tarefas`: tipo `teste` em `TipoItemAnalise`, colunas `grupo` e `vago` em `analise_item` com a migration `20261103_AnaliseGrupos` aplicada no banco local, e resposta com `checklist` agrupado (`GrupoDoChecklistResponse`) e `testes`. Verificar com `dotnet build`.
- [x] 6.4 Testes do novo formato (analisador, serviço e API): grupos em ordem, trecho vago, item sem grupo em `Geral`, testes marcáveis e herdados na reanálise. Verificar que `dotnet test` passa.
- [x] 6.5 Reanalisar a tarefa real do IPI com a chave (chamada paga autorizada pelo usuário) e conferir grupos, trechos vagos, testes e dúvidas gravados.

## Workflow follow-up

- Propor a change do front no ActumUI (seção *Análise com IA* no detalhe da tarefa) e regenerar os tipos com `npm run api:tipos`.
- Arquivar a change com `/opsx:archive` após revisão.
