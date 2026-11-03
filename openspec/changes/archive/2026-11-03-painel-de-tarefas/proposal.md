# Proposal

## Why

Com as tarefas sincronizadas e o ciclo de vida no Actum, o usuário precisa ver numa só tela o que fazer primeiro: as reabertas, a caixa de entrada e as tarefas por prazo interno, das duas empresas ou de uma só. Também precisa de um detalhe da tarefa com branches, histórico de retrabalho e comentários, para entender o que mudou sem procurar nos e-mails do Asana.

## What Changes

- `GET /tarefas/painel`: tarefas agrupadas em Retrabalho, Entrada, Atrasadas, Hoje, Esta semana, Próximas e Sem prazo, com filtro por empresa e texto.
- `GET /tarefas`: listagem paginada com filtros (inclui concluídas e arquivadas).
- `GET /tarefas/{identifier}`: detalhe com branches, retrabalhos (com os comentários feitos após cada reabertura) e comentários.
- `POST /tarefas/{identifier}/comentarios/marcar-lidos`.

## Capabilities

### New Capabilities

- `task-board`: consulta das tarefas para o painel, a listagem e o detalhe.

### Modified Capabilities

Nenhuma.

## Impact

- **Módulos afetados:** `Actum.Modules.Tarefas` (somente leitura, exceto marcar comentários como lidos).
- **Contrato da API:** novos endpoints (não breaking). Impacto no front (`..\ActumUI`): telas Painel e Detalhe da tarefa. O layout de referência é o do ERP Aureum: menu lateral, cabeçalho azul-escuro e cartões brancos com ícone e contadores coloridos.
- **Banco:** sem alterações.

### Fora do escopo

- Dashboard e indicadores de retrabalho (fase posterior).
- Ordenação personalizada e arrastar cartões.
- Análise da demanda com IA (Fase 2).
