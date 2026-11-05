# Proposal

## Why

No painel, o usuário só sabe se uma tarefa tem comentários não lidos; para saber quando foi a última movimentação e quem escreveu, precisa abrir o detalhe. Mostrar no cartão a data e o autor do último comentário permite priorizar sem abrir cada tarefa.

## What Changes

- O cartão da tarefa (`CartaoTarefaResponse`), usado por `GET /painel` e `GET /tarefas`, passa a trazer `ultimoComentarioEm` (data e hora local) e `ultimoComentarioAutor` do comentário mais recente da tarefa.
- Os dois campos são `null` quando a tarefa não tem comentários; `ultimoComentarioAutor` também é `null` quando o autor do comentário mais recente é desconhecido.
- Os valores são lidos da tabela `tarefas.comentario`, que já guarda autor e data de cada comentário copiado do Asana ou enviado pelo Actum. Não há coluna nova nem migration.

## Capabilities

### New Capabilities

Nenhuma.

### Modified Capabilities

- `task-board`: o requisito "Dados do cartão" passa a incluir a data e o autor do último comentário.

## Impact

- **Módulos afetados:** `Actum.Modules.Tarefas` (`PainelService`, `Contracts/PainelResponses.cs`).
- **Contrato da API:** dois campos novos e anuláveis em `CartaoTarefaResponse` (não breaking). Impacto no front (`..\ActumUI`): regenerar os tipos do OpenAPI e exibir os campos no `CartaoTarefa.vue` (change própria no ActumUI).
- **Banco:** sem alteração; a consulta do cartão passa a ler o comentário mais recente de cada tarefa em `tarefas.comentario`.

### Fora do escopo

- Exibir o texto do último comentário no cartão.
- Ordenar ou filtrar o painel pela data do último comentário.
- Alterar como os comentários são sincronizados, enviados ou marcados como lidos.
- Colunas desnormalizadas em `tarefas.tarefa`.
