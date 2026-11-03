# Proposal

## Why

O principal incômodo do usuário é a tarefa reaberta depois do teste: ela volta com o prazo antigo, aparece como atrasada e ele precisa procurar o que mudou nos e-mails do Asana. Para resolver isso, o Actum precisa de uma cópia local das tarefas atribuídas ao usuário nas duas empresas, atualizada sob demanda, que detecta as reaberturas e guarda o histórico de retrabalho.

## What Changes

- Novo módulo `Actum.Modules.Tarefas` (schema `tarefas`) com empresas, tarefas, comentários, retrabalhos e registro das sincronizações.
- Sincronização sob demanda (`POST /sincronizacoes`) com todos os workspaces ativos, usando `IAsanaClient`.
- **Carga inicial:** na primeira sincronização de cada empresa, as tarefas abertas entram já em andamento, com o prazo interno igual ao prazo do Asana, para aparecerem como próximas ou atrasadas no painel.
- Nas sincronizações seguintes, tarefas novas entram na caixa de entrada.
- Detecção de reabertura: cria o registro de retrabalho e marca o prazo interno como pendente.
- Arquivamento das tarefas que saíram da lista do usuário no Asana.
- Consulta e ajuste das empresas (cor e se entram na sincronização).

## Capabilities

### New Capabilities

- `task-sync`: cópia local das tarefas do Asana atribuídas ao usuário, por empresa, com carga inicial, novas tarefas, atualização, comentários e arquivamento.
- `rework-tracking`: detecção de tarefas reabertas e histórico de retrabalho.

### Modified Capabilities

Nenhuma.

## Impact

- **Módulos afetados:** novo `Actum.Modules.Tarefas`, que passa a depender do contrato público `IAsanaClient` do módulo `Asana`.
- **Contrato da API:** novos endpoints `POST /sincronizacoes`, `GET /sincronizacoes/ultima`, `GET /empresas` e `PATCH /empresas/{identifier}` (não breaking). Impacto no front (`..\ActumUI`): botão *Sincronizar* com resumo do resultado e configuração das empresas.
- **Banco:** schema `tarefas` com as tabelas `empresa`, `tarefa`, `comentario`, `retrabalho` e `sincronizacao`.

### Fora do escopo

- Iniciar, bloquear e concluir tarefas, branches e definição do novo prazo após reabertura (`ciclo-de-vida-da-tarefa`).
- Painel, listagem e detalhe da tarefa (`painel-de-tarefas`).
- Sincronização automática ou em segundo plano.
- Indicadores de retrabalho (dashboard, fase posterior); nesta change os dados são apenas registrados.
