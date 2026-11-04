# Proposal

## Why

A Fase 4 (docs/especificacao-fase1.md) traz o dashboard com indicadores de retrabalho por empresa e as anotações privadas da tarefa. Para os indicadores valerem, o retrabalho precisa seguir a definição do usuário: **cada vez que, no histórico do Asana, "concluiu esta tarefa" é seguido depois por "marcou como não concluída"**, podendo ocorrer várias vezes na mesma tarefa.

A detecção atual (Fase 1) não segue essa regra:
- só registra reaberturas depois de uma conclusão feita pelo próprio Actum. Por isso, as tarefas "(reabrindo)" da carga inicial entraram sem nenhum retrabalho, e ciclos de concluir e reabrir feitos por outras pessoas com a tarefa aberta no Actum passam despercebidos;
- cria retrabalho sem reabertura no histórico, na data da sincronização;
- aceita reabertura sem conclusão antes.

As anotações privadas guardam o diário da tarefa (decisões, o que foi testado) sem ir para o Asana.

## What Changes

- **BREAKING (dados de retrabalho):**
  - os retrabalhos de cada tarefa passam a ser **recalculados a partir dos pares do histórico do Asana** (`marked_complete` → `marked_incomplete` posterior), sempre que o histórico da tarefa é lido;
  - os retrabalhos já gravados que não fecham um par são removidos; as branches ligadas a eles perdem só o vínculo;
  - deixa de existir o retrabalho "sem histórico".
- **Pares antigos são só histórico:** já resolvidos na data da conclusão seguinte, ou em aberto se a tarefa não foi concluída de novo, sem mudar status nem prazo da tarefa. Só uma reabertura nova, posterior à conclusão que o Actum conhece (tarefa `concluida` ou arquivada por conclusão no Asana), coloca a tarefa em `retrabalho` com prazo pendente, como hoje.
- **Conclusões guardadas:** as conclusões do histórico (`marked_complete`) passam a ser gravadas, para calcular a taxa de retrabalho.
- **`POST /retrabalhos/reprocessamento`:** relê do Asana o histórico de todas as tarefas e recalcula conclusões e retrabalhos. É o que traz os pares antigos e roda sob o mesmo controle de concorrência da sincronização (`409`).
- **`GET /retrabalhos/indicadores?de=&ate=&empresa=`:** indicadores por empresa no período (pela data da reabertura):
  - totais e taxa: retrabalhos, tarefas com retrabalho, tarefas concluídas e % das concluídas que voltaram;
  - evolução mensal dos retrabalhos;
  - ranking das tarefas com mais retrabalhos.
- **Anotações privadas:** `GET/POST /tarefas/{identifier}/anotacoes` e `PUT/DELETE /tarefas/{identifier}/anotacoes/{anotacaoIdentifier}`. Várias notas por tarefa, com data, editáveis e excluíveis, nunca enviadas ao Asana.

## Capabilities

### New Capabilities

- `rework-dashboard`: indicadores de retrabalho por empresa e período, e reprocessamento do histórico.
- `task-notes`: anotações privadas da tarefa.

### Modified Capabilities

- `rework-tracking`: retrabalho passa a ser o par conclusão → reabertura do histórico do Asana, recalculado; pares antigos só como histórico; sem retrabalho "sem histórico".
- `task-sync`: tarefa nova com conclusões e reaberturas anteriores ao Actum passa a ter esses retrabalhos como histórico.

## Impact

- **Módulos afetados:** só `Actum.Modules.Tarefas` (domínio do retrabalho, sincronização, serviços e endpoints novos). Usa o contrato `IAsanaClient` existente (`ListarStoriesAsync`); nenhuma dependência nova entre módulos.
- **Contrato da API:**
  - endpoints novos: `POST /retrabalhos/reprocessamento`, `GET /retrabalhos/indicadores` e os quatro de anotações;
  - `RetrabalhoResponse` (no detalhe) ganha `concluidaEm`, aditivo;
  - a quantidade de retrabalhos no Painel e no detalhe passa a refletir a regra nova (valores mudam, contrato não).
- **Impacto no front (`..\ActumUI`):** página nova "Retrabalho" (dashboard com filtro de período e empresa, botão de reprocessar) e card "Anotações privadas" no detalhe da tarefa. Será uma change própria no ActumUI, depois desta.
- **Banco** (schema `tarefas`, migration `20261103_RetrabalhoPorPares`):
  - tabela nova `conclusao`;
  - coluna `concluida_em` em `retrabalho`;
  - tabela nova `anotacao`.
- **Asana:** o reprocessamento lê o histórico de todas as tarefas (uma chamada por tarefa, com o limite de requisições já tratado). Só leitura.

### Fora do escopo

- Tempo entre conclusão e reabertura e ranking de quem reabriu (indicadores não escolhidos).
- Exportar os indicadores (CSV/planilha).
- Anotações com formatação rica, anexos ou compartilhamento.
- Reprocessamento automático agendado.
