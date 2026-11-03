# Design

## Context

Depende de `sincronizacao-de-tarefas` e de `ciclo-de-vida-da-tarefa`. O volume é pequeno (dezenas a poucas centenas de tarefas abertas), então o painel pode ser montado em uma consulta sem paginação.

## Goals / Non-Goals

**Goals:**
- Um único endpoint entrega o painel inteiro, já agrupado, para o front só renderizar.
- Regras de agrupamento no back, testáveis e iguais para qualquer cliente.

**Non-Goals:**
- Cache ou leitura em tempo real.

## Decisions

### 1. Agrupamento

Considera as tarefas com status diferente de `concluida` e `arquivada`. Cada tarefa entra no primeiro grupo cuja regra atender; "hoje" é a data local do servidor e a semana termina no domingo.

| Ordem | Grupo | Regra | Ordenação dentro do grupo |
|---|---|---|---|
| 1 | `retrabalho` | status `retrabalho`, ou `bloqueada` com `status_anterior = retrabalho` | prazo pendente primeiro, depois `reaberta_em` mais antiga |
| 2 | `entrada` | status `entrada` | `prazo_asana` (nulos por último), depois `create_in` |
| 3 | `atrasadas` | `prazo_interno` < hoje | `prazo_interno`, prioridade |
| 4 | `hoje` | `prazo_interno` = hoje | prioridade |
| 5 | `esta_semana` | `prazo_interno` ≤ domingo da semana atual | `prazo_interno`, prioridade |
| 6 | `proximas` | `prazo_interno` > domingo | `prazo_interno`, prioridade |
| 7 | `sem_prazo` | demais | `create_in` |

Prioridade nula é ordenada depois de 3. Grupos vazios são devolvidos com lista vazia, para o front manter o layout.

### 2. Cartão do painel

`{ identifier, titulo, empresa { identifier, nome, cor }, projetos, status, prazoInterno, prazoAsana, prazoInternoPendente, prioridade, quantidadeRetrabalhos, comentariosNaoLidos, bloqueada }`

`prazoAsana` vai sempre, e o front só o mostra quando difere do prazo interno.

### 3. Detalhe

`GET /tarefas/{identifier}` devolve os dados da tarefa, as branches (com `enviada`), os retrabalhos em ordem e os comentários. Cada retrabalho traz os comentários com `criado_em` entre a sua `reaberta_em` e a próxima reabertura (ou até hoje), que costumam explicar o motivo.

### 4. Listagem

`GET /tarefas?empresa=&status=&busca=&pagina=&tamanho=` usa o `PagedResult` da fundação. `busca` procura sem diferenciar maiúsculas e acentos no título e na descrição (`ILIKE` com `unaccent`, extensão criada em uma migration), com tamanho padrão 20 e máximo 100.

## Risks / Trade-offs

- **[Extensão `unaccent` exige permissão no banco]** → O usuário `actum` de desenvolvimento cria a extensão na migration. Se não tiver permissão, o script `criar-usuario-dev.sql` cria a extensão como superusuário.

## Migration Plan

Migration `yyyyMMdd_Unaccent` (apenas `create extension if not exists unaccent`). Rollback: reverter a migration.
