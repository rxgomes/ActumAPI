# Tasks

## 1. Actum.Modules.Tarefas

- [x] 1.1 Em `Contracts/PainelResponses.cs`, adicionar ao final de `CartaoTarefaResponse` os campos `DateTime? UltimoComentarioEm` e `string? UltimoComentarioAutor`, com `<param>` em pt-BR explicando que são nulos sem comentários (e o autor, quando desconhecido). Verificar com `dotnet build` sem avisos novos.
- [x] 1.2 Em `PainelService.LerCartoes`, projetar o comentário mais recente da tarefa (ordem `criado_em` decrescente e depois `id` decrescente, para desempate estável) em `LinhaDoCartao` e repassá-lo em `ParaCartao`, sem alterar o agrupamento nem a ordenação. Sem migration. Verificar com `dotnet build` e conferindo no log do EF que o painel continua em uma única consulta.
- [x] 1.3 Em `tests/Actum.Modules.Tarefas.Tests/PainelTests.cs`, cobrir os cenários de "Dados do cartão": tarefa com dois comentários de autores diferentes (o mais recente vence, lido ou não), tarefa sem comentários (campos nulos), último comentário sem autor e listagem `GET /tarefas` com os mesmos campos. Verificar que `dotnet test` passa.

## 2. Verificação

- [x] 2.1 Com a API rodando, conferir em `/scalar/v1` que `CartaoTarefaResponse` traz `ultimoComentarioEm` e `ultimoComentarioAutor` anuláveis no `openapi/v1.json`, e que `GET /painel` devolve os valores de uma tarefa com comentários.

## Workflow follow-up

- Fazer a change `ultimo-comentario-no-cartao` no ActumUI (regenerar os tipos e exibir os campos no cartão).
- Arquivar a change com `/opsx:archive` após revisão.
