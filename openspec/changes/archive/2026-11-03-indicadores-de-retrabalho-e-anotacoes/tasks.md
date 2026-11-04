# Tasks

## 1. Actum.Modules.Tarefas: domínio do retrabalho

- [x] 1.1 Criar `HistoricoDeConclusoes` (função pura): conclusões e pares concluiu → não concluída a partir das stories, com resolução pela conclusão seguinte e reabertura sem conclusão ignorada. Testes unitários sem banco: um ciclo, vários ciclos, reabertura sem conclusão, duas reaberturas seguidas, empate de horário. Verificar que `dotnet test` passa.
- [x] 1.2 Criar a entidade `Conclusao` e a coluna `ConcluidaEm` em `Retrabalho`. Implementar `Tarefa.RecalcularRetrabalhos` no lugar de `RegistrarReaberturas`:
  - conclusões novas;
  - associação por `asana_story_gid`, criação e remoção (desvinculando branches);
  - renumeração (índice `tarefa_id + numero` sem unicidade);
  - `resolvido_em` preservado;
  - efeitos só para pares novos após a referência;
  - `concluida` sem par volta para `em_andamento` com prazo pendente.

  Verificar com `dotnet build`.
- [x] 1.3 Ajustar `SincronizacaoService` para chamar o recálculo sempre que lê o histórico (tarefas novas, modificadas, que voltaram para a lista e arquivadas por conclusão), mantendo o total de reaberturas do resumo só com os pares novos. Verificar que os testes existentes de sincronização passam, ajustando apenas os cenários que a spec mudou ("sem histórico" e "concluída antes do Actum").

## 2. Actum.Modules.Tarefas: banco

- [x] 2.1 Criar a entidade `Anotacao`, a coluna `tipo` em `Sincronizacao` (com `GET /sincronizacoes/ultima` filtrando `sincronizacao`) e as configurações EF, incluindo os índices `conclusao (tarefa_id, concluida_em)` e `retrabalho (reaberta_em)`. Verificar com `dotnet build`.
- [x] 2.2 Gerar a migration `20261103_RetrabalhoPorPares` e aplicá-la no banco local. Verificar com `select * from tarefas.conclusao` e `select * from tarefas.anotacao`, sem aspas, datas `timestamp without time zone`.

## 3. Actum.Modules.Tarefas: serviços e endpoints

- [x] 3.1 `IRetrabalhoService.ReprocessarAsync` (XML doc completo):
  - mesmo semáforo da sincronização (`409`);
  - recálculo por tarefa, com falhas por tarefa;
  - registro em `sincronizacao` com `tipo = reprocessamento`.

  Verificar com `dotnet build`.
- [x] 3.2 `IRetrabalhoService.ObterIndicadoresAsync`: totais e taxa por empresa e no total, evolução mensal com meses zerados, ranking top 10 e `UltimoReprocessamentoEm`. Verificar com `dotnet build`.
- [x] 3.3 `IAnotacaoService` (listar, criar, alterar, excluir), com XML doc completo. Verificar com `dotnet build`.
- [x] 3.4 Endpoints:
  - `RetrabalhoEndpoints`: `POST /retrabalhos/reprocessamento`, `GET /retrabalhos/indicadores` com filtro validado em pt-BR e limite de 24 meses;
  - `AnotacaoEndpoints`: quatro rotas;
  - `RetrabalhoResponse` com `concluidaEm`;
  - metadados OpenAPI.

  Verificar em `/scalar/v1`.

## 4. Testes

- [x] 4.1 `Actum.Modules.Tarefas.Tests` (rework-tracking):
  - pares no detalhe da sincronização (um ciclo, dois ciclos, reabertura sem conclusão);
  - par antigo em tarefa `em_andamento` sem mudar status;
  - reabertura nova de tarefa `concluida` com efeitos;
  - concluída sem par volta `em_andamento` com prazo pendente;
  - retrabalho sem par removido com a branch desvinculada;
  - retrabalho preservado (identifier, prazos, resolução);
  - renumeração;
  - conclusões sem duplicar;
  - tarefa nova com histórico fica `entrada`.

  Verificar que `dotnet test` passa.
- [x] 4.2 Testes de reprocessamento e indicadores:
  - `409` concorrente; falha de uma tarefa não interrompe;
  - totais e taxa das duas empresas do cenário da spec;
  - meses zerados; ranking com desempate; filtro por empresa;
  - período inválido `400`.

  Verificar que `dotnet test` passa.
- [x] 4.3 Testes de anotações (serviço e API):
  - ordem cronológica; criar `201`; alterar com `alteradaEm`;
  - excluir `204`; texto vazio `400` em pt-BR no campo `texto`;
  - outra tarefa `404`; tarefa arquivada aceita;
  - nenhuma chamada ao Asana.

  Verificar que `dotnet test` passa.
- [x] 4.4 Reprocessamento real no banco local (só leitura no Asana): conferir os retrabalhos das tarefas "(reabrindo)" e os indicadores do último mês.

## Workflow follow-up

- Propor a change do front no ActumUI (página Retrabalho com indicadores e botão de reprocessar; card Anotações privadas no detalhe) e regenerar os tipos.
- Arquivar a change com `/opsx:archive` após revisão e enviar ao GitHub.
