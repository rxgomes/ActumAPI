# Design

## Context

- `AnaliseService.AnalisarAsync` lê a tarefa com `AsNoTracking`, incluindo comentários e retrabalhos, monta `DemandaParaAnalise` e chama `IAnalisadorDeDemanda` fora de transação. Depois grava `Analise` e os `AnaliseItem`.
- Os comentários vêm de `tarefas.comentario`, preenchida pela sincronização. `SincronizacaoService.AtualizarTarefaAsync` só chama `IAsanaClient.ListarStoriesAsync` quando a tarefa foi modificada no Asana ou voltou à lista.
- `Tarefa.AdicionarComentarios(stories, lidos)` já ignora stories que não são comentários e as que já estão gravadas (pelo `asana_story_gid`).
- `AnaliseItem` guarda o tipo (`checklist`, `teste`, `duvida`) como texto em snake_case, com o índice único `(analise_id, tipo, ordem)`. A herança de marcações em `Analise.Criar` compara tipo e texto normalizado.
- Os erros do Asana (`AsanaException`) já viram Problem Details pelo `AsanaExceptionHandler`. O endpoint de análise já declara `502`.

## Goals / Non-Goals

**Goals:**
- A IA recebe sempre os comentários atuais do Asana.
- Regras e sugestões dos comentários ficam visíveis e rastreáveis até a origem.

**Non-Goals:**
- Reaproveitar a sincronização inteira (retrabalhos, campos da tarefa) dentro da análise.
- Guardar a origem de forma estruturada (id do comentário). O texto basta para o usuário localizar o comentário.

## Decisions

### 1. Atualizar só os comentários, no `AnaliseService`

Antes de montar a demanda, a análise chama `asana.ListarStoriesAsync(tarefa.AsanaGid)`, depois `tarefa.AdicionarComentarios(stories, lidos: false)` e `SaveChangesAsync`. A tarefa passa a ser lida com tracking, só com `Comentarios` e `Retrabalhos`. Os comentários novos entram não lidos, como na sincronização, porque são novidade para o usuário.

Isso acontece depois das verificações de arquivada e de análise em andamento, dentro do mesmo `try/finally` que libera `AnalisesEmAndamento`.

- **Alternativa: rodar a sincronização da tarefa.** Recalcularia retrabalhos e status no meio de uma análise, que hoje não muda o ciclo de vida. Rejeitada por misturar responsabilidades.
- **Alternativa: usar o Asana sem gravar.** O comentário seria analisado, mas não apareceria no detalhe nem no cartão. Rejeitada.

### 2. Falha do Asana interrompe a análise

Se `ListarStoriesAsync` lançar `AsanaException`, a exceção sobe e o handler devolve o status do Asana (`502` ou outro). Nada é gravado e a Anthropic não é chamada.

- **Alternativa: seguir com os comentários do banco.** Esconderia justamente o problema que esta change resolve: a IA analisaria sem os comentários recentes sem o usuário saber. Rejeitada. O usuário pode tentar de novo.

### 3. Regras como novo tipo de `AnaliseItem`, com `origem`

- Novo valor `TipoItemAnalise.Regra` (texto `regra`, cabe em `tipo` varchar(20)). As regras reaproveitam ordem, marcação, herança de marcação e o `PATCH` de itens, sem tabela nova.
- Nova coluna `origem varchar(200) null` em `tarefas.analise_item`. Só as regras a preenchem e o texto é truncado no limite.
- Migration `20261105_AnaliseItemOrigem`, só com `add column`. Versões antigas ficam sem regras.

- **Alternativa: tabela `analise_regra`.** Duplicaria marcação e herança. Rejeitada.
- **Alternativa: usar `grupo` para a origem.** O campo tem outro significado (área do checklist). Rejeitada.

### 4. Origem preenchida pela IA, em formato fixo

O JSON Schema ganha `regras: [{ texto, origem }]`. O prompt manda a origem ser exatamente `descrição` ou `comentário de <autor> em <dd/MM/yyyy HH:mm>`, copiando o autor e a data do atributo do comentário. Para isso, o atributo `data` dos comentários na mensagem passa a vir em `dd/MM/yyyy HH:mm`.

- **Alternativa: a IA devolver um índice do comentário, e a API montar o texto.** Seria mais preciso, mas exigiria numerar os comentários no prompt e validar índices inválidos. Fica como melhoria se a origem vier errada com frequência.

### 5. Instruções para os comentários

No `Sistema`, o parágrafo dos comentários passa a dizer:
- os comentários podem trazer pedidos, respostas, decisões, regras, restrições e sugestões, e todos devem ser considerados;
- o que foi combinado depois prevalece;
- pedidos viram checklist, dados de referência viram testes, regras e sugestões viram `regras`;
- só se ignoram mensagens automáticas e mensagens sem conteúdo (agradecimentos, "ok", avisos de que vai fazer).

## Risks / Trade-offs

- [A análise fica mais lenta, com uma chamada a mais ao Asana] → `ListarStoriesAsync` já pagina e costuma levar menos de um segundo, contra dezenas de segundos da IA.
- [A IA pode errar o texto da origem] → O formato é instruído e o autor e a data vêm prontos no atributo. Se acontecer com frequência, adotar a alternativa do índice (decisão 4).
- [Repetição entre regra e checklist] → O prompt diferencia: ação a fazer vai para o checklist, condição a respeitar vai para regras.
- [Mais tokens de saída] → A seção costuma ser curta. O `MaxTokens` configurado continua valendo, e uma resposta cortada já é tratada como falha da IA.

## Migration Plan

1. Aplicar a migration `20261105_AnaliseItemOrigem` (`dotnet ef database update` ou na subida da API, como as anteriores).
2. Publicar a API. O front atual ignora `regras` e `origem` até a change do ActumUI.
3. Rollback: reverter o código. A coluna anulável pode ficar ou ser removida com a migration de down.
