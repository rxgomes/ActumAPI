# Design

## Context

A detecção de retrabalho da Fase 1 está em `Tarefa.RegistrarReaberturas(stories, referencia, registrarSemHistorico, agora)`. A `SincronizacaoService` só a chama quando a tarefa local estava `concluida` ou arquivada por conclusão no Asana, e só depois de ler o histórico (`ListarStoriesAsync`), que é lido apenas para tarefas modificadas desde a última sincronização. `retrabalho` guarda `numero` (único com `tarefa_id`), `reaberta_em`, `reaberta_por`, `asana_story_gid` (único, nulo no caso "sem histórico"), `prazo_interno_anterior`, `prazo_interno_novo` e `resolvido_em`. `tarefa_branch.retrabalho_id` aponta para o retrabalho (FK `Restrict`).

Decisões do usuário:
- retrabalho = par "concluiu" → "marcou como não concluída" no histórico, cada ocorrência conta;
- pares antigos são só histórico;
- retrabalhos sem par são removidos e recalculados;
- indicadores: totais e taxa, evolução mensal e ranking de tarefas;
- anotações: lista de notas com data.

## Goals / Non-Goals

**Goals**
- Uma única regra de retrabalho, derivada do histórico do Asana, usada pela sincronização, pelo reprocessamento e pelos indicadores.
- Preservar os dados que o Actum acrescenta ao retrabalho (prazos, resolução, vínculo de branches) quando o par continua existindo.
- Indicadores calculados no banco, sem chamar o Asana.

**Non-Goals**
- Indicadores de quem reabriu e do tempo até a reabertura, exportação e agendamento.

## Decisions

### 1. Cálculo dos pares (`HistoricoDeConclusoes`, função pura no domínio)

Percorre as stories da tarefa por `CriadaEm` (e `Gid` no empate):
- `marked_complete` → registra uma conclusão e guarda-a como "conclusão pendente". Se já houver um par anterior sem resolução, esta conclusão o resolve (`resolvido_em`).
- `marked_incomplete` com conclusão pendente → par `(concluidaEm, reabertaEm, reabertaPor, storeGidDaReabertura)` e limpa a pendente.
- `marked_incomplete` sem conclusão pendente → ignorado.

O resultado é a lista de conclusões e a lista ordenada de pares, cada um com a resolução vinda do histórico (ou nula).

### 2. Recálculo na tarefa (`Tarefa.RecalcularRetrabalhos(stories, agora)`, substitui `RegistrarReaberturas`)

1. Grava as conclusões novas (por `asana_story_gid`).
2. Associa cada par ao retrabalho existente com o mesmo `asana_story_gid`, ou cria um retrabalho novo.
3. Remove os retrabalhos sem par. Antes, limpa `retrabalho_id` das branches ligadas, pela coleção de branches da própria tarefa.
4. Renumera por `reaberta_em`. O índice `(tarefa_id, numero)` deixa de ser único (a renumeração acontece na mesma gravação e a ordem dos `UPDATE` do EF não é garantida); a numeração sequencial é garantida pelo recálculo, que é o único lugar que a define.
5. `resolvido_em`: mantém o valor posto pelo Actum (conclusão pelo Actum); se estiver nulo, usa a resolução do histórico.
6. Efeitos na tarefa, só para pares **novos** com `reaberta_em` posterior à referência (`ConcluidaEm` se `concluida`; `ArquivadaEm` se arquivada como `concluida_no_asana`): status `retrabalho`, `prazo_interno_pendente = true` e `prazo_interno_anterior` = prazo vigente. Sem esse par, uma tarefa `concluida` que voltou para a lista vira `em_andamento` com prazo pendente (o caso "sem histórico" deixa de criar retrabalho).

A sincronização chama `RecalcularRetrabalhos` sempre que lê o histórico, para qualquer status. Hoje ela só chama a detecção quando a tarefa estava concluída ou arquivada.

### 3. Reprocessamento (`IRetrabalhoService.ReprocessarAsync`)

- Usa `SincronizacaoEmAndamento` (o mesmo semáforo): se estiver ocupado, `409`.
- Para cada tarefa do Actum, em série: `ListarStoriesAsync` → `RecalcularRetrabalhos` → `SaveChanges` por tarefa. Uma `AsanaException` numa tarefa entra na lista de falhas (título e mensagem) e o processamento segue.
- Resposta `ReprocessamentoResponse(TarefasLidas, RetrabalhosAntes, RetrabalhosDepois, Falhas[])`.
- Leva cerca de 1 s por tarefa no Asana. Não tem tempo limite próprio além dos do `AsanaClient`.

### 4. Indicadores (`IRetrabalhoService.ObterIndicadoresAsync(de, ate, empresa)`)

Consultas no banco, com o período fechado em dias locais (`de 00:00` até `ate 23:59:59`):
- **Retrabalhos no período:** `retrabalho.reaberta_em` no período, com empresa via `tarefa.empresa_id`.
- **Concluídas no período:** tarefas distintas com `conclusao.concluida_em` no período.
- **Taxa:** tarefas com alguma conclusão no período que tenham um retrabalho cuja `concluida_em` (a conclusão do par) cai no período, sobre as concluídas no período, × 100, com 1 casa (`Math.Round(…, 1, AwayFromZero)`).
- **Evolução:** agrupamento por mês de `reaberta_em` × empresa, completado com zeros para todos os meses do período e todas as empresas do filtro.
- **Ranking:** top 10 por quantidade de retrabalhos no período, desempate pela última reabertura.
- Resposta `IndicadoresResponse(De, Ate, Empresas: IndicadorDaEmpresa[], Total: IndicadorDaEmpresa, Evolucao: MesDeRetrabalho[], Ranking: TarefaDoRanking[])`. `IndicadorDaEmpresa(Identifier?, Nome, Cor, Retrabalhos, TarefasComRetrabalho, TarefasConcluidas, Taxa?)` e `MesDeRetrabalho(Mes "yyyy-MM", PorEmpresa: [{Identifier, Quantidade}])`.
- Validação em `Contracts` (`IndicadoresFiltro` com `[AsParameters]`, mensagens em pt-BR, regra de 24 meses no endpoint, como `ValidacaoDeConsulta`).

### 5. Anotações privadas

- Entidade `Anotacao` (`tarefa_id`, `texto text`, `alterada_em timestamp without time zone null`). A data de criação é a `create_in` da `Entity`, exposta como `criadaEm`.
- `IAnotacaoService`: `ListarAsync`, `CriarAsync`, `AlterarAsync`, `ExcluirAsync`. "Não encontrado" é resultado do serviço, que o endpoint traduz em `404`.
- DTO `AnotacaoRequest([Required(ErrorMessage = "Escreva a anotação.")] [StringLength(10000, ErrorMessage = "A anotação deve ter no máximo {1} caracteres.")] string? Texto)`; texto só de espaços → `DomainException` com a mesma mensagem do `Required`, mapeada para o campo pelo endpoint (`ValidationProblem` com `Texto`).
- `Endpoints/AnotacaoEndpoints` (tag `Anotações`), `Endpoints/RetrabalhoEndpoints` (tag `Retrabalho`).

### 6. Banco (schema `tarefas`, migration `20261103_RetrabalhoPorPares`)

| Tabela | Colunas novas |
|---|---|
| `conclusao` (nova) | `tarefa_id` FK cascade, `asana_story_gid varchar(50)` único, `concluida_em timestamp without time zone`, `concluida_por varchar(200) null` |
| `retrabalho` | `concluida_em timestamp without time zone null` (preenchida no recálculo) |
| `anotacao` (nova) | `tarefa_id` FK cascade, `texto text`, `alterada_em timestamp without time zone null` |
| `sincronizacao` | `tipo varchar(20)` (`sincronizacao` ou `reprocessamento`, padrão `sincronizacao` para as linhas existentes); `GET /sincronizacoes/ultima` passa a considerar só `sincronizacao` |

- Índices: `conclusao (tarefa_id, concluida_em)` e `retrabalho (reaberta_em)` para os indicadores.
- Nenhum dado é migrado pela migration. O recálculo acontece no reprocessamento, disparado pelo usuário, e na sincronização.

## Risks / Trade-offs

- **Remoção de retrabalhos (breaking em dados):** retrabalhos "sem histórico" e sem par somem no reprocessamento. As branches ficam, sem o vínculo. O detalhe e o Painel passam a mostrar a contagem nova.
- **Renumeração:** os números de retrabalho de uma tarefa podem mudar quando pares antigos aparecem (ex.: o retrabalho 1 vira 3). O front já mostra o número que a API devolve.
- **Custo do reprocessamento:** uma chamada ao Asana por tarefa. É manual (botão no dashboard), roda em série e respeita o `429`.
- **Taxa:** depende das conclusões do histórico. Antes do primeiro reprocessamento, ela só considera as tarefas cujo histórico foi lido desde então. O dashboard avisa quando nunca houve reprocessamento (campo `UltimoReprocessamentoEm` na resposta dos indicadores, gravado na tabela `sincronizacao` com o tipo `reprocessamento`).

## Migration Plan

1. Aplicar `20261103_RetrabalhoPorPares`.
2. Publicar a API e o front.
3. Rodar o reprocessamento uma vez pelo dashboard.

Rollback: reverter a migration. Os retrabalhos removidos no reprocessamento não voltam; seriam recriados com a regra antiga só na sincronização de tarefas que voltem de concluídas.

## Open Questions

Nenhuma.
