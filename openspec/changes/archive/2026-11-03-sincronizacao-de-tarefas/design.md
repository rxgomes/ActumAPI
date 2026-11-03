# Design

## Context

Depende de `setup-inicial-da-solucao` (entidade base, `ModuleDbContext`) e de `integracao-asana` (`IAsanaClient`). O Asana continua sendo a fonte das tarefas, mas prazo interno, status interno e retrabalho existem só no Actum. Uso local, um único usuário e um único processo.

## Goals / Non-Goals

**Goals:**
- Sincronização idempotente: repetir não duplica tarefas, comentários nem retrabalhos.
- Poucas chamadas ao Asana: stories só para tarefas novas ou modificadas.
- Retrabalho registrado com data, autor e prazos, pronto para indicadores futuros.

**Non-Goals:**
- Refletir no Actum tudo o que acontece no Asana (subtarefas, seguidores, anexos, campos personalizados).
- Resolver conflitos de edição: os campos vindos do Asana são somente leitura no Actum, e os campos internos nunca são enviados.

## Decisions

### 1. Dependência `Tarefas → Asana`

O módulo `Tarefas` usa apenas `IAsanaClient` e os records de `Contracts/` do módulo `Asana`. O módulo `Asana` não conhece `Tarefas`. Justificativa: o Asana é um serviço externo e o módulo `Asana` é só seu adaptador; não há regra de negócio do lado dele.

### 2. Modelo de dados (schema `tarefas`)

Todas as tabelas têm as colunas da entidade base (`id`, `identifier`, `create_in`, `update_in`, `is_active`). As FKs usam `id` (`bigint`), e a API expõe `identifier`.

**`empresa`**

| Coluna | Tipo | Observação |
|---|---|---|
| asana_workspace_gid | varchar(50) | único |
| nome | varchar(200) | |
| cor | varchar(7) | hex; atribuída de uma paleta fixa na criação |
| sincronizar | boolean | default `true` |
| carga_inicial_em | timestamp | null até a primeira sincronização concluída da empresa |

**`tarefa`**

| Coluna | Tipo | Observação |
|---|---|---|
| empresa_id | bigint | FK `empresa` |
| asana_gid | varchar(50) | único |
| titulo | varchar(1024) | |
| descricao | text | `notes` |
| descricao_html | text | `html_notes` |
| projetos | varchar(1000) | nomes separados por `, ` |
| link_asana | varchar(500) | |
| prazo_asana | date | |
| prazo_interno | date | |
| prazo_interno_pendente | boolean | default `false` |
| status | varchar(30) | `entrada`, `em_andamento`, `bloqueada`, `concluida`, `retrabalho`, `arquivada` |
| status_anterior | varchar(30) | usado ao desbloquear e ao desarquivar |
| prioridade | smallint | 1 a 3, null |
| asana_modificada_em | timestamp | |
| concluida_em | timestamp | última conclusão feita pelo Actum |
| arquivada_em | timestamp | |
| motivo_arquivamento | varchar(30) | `concluida_no_asana`, `reatribuida`, `excluida` |
| sincronizada_em | timestamp | |

Índices: (`empresa_id`, `status`), (`prazo_interno`).

**`comentario`**: `tarefa_id`, `asana_story_gid` (único), `autor`, `texto`, `criado_em`, `lido` (boolean).

**`retrabalho`**: `tarefa_id`, `numero` (smallint; único com `tarefa_id`), `reaberta_em`, `reaberta_por`, `asana_story_gid` (único quando não nulo), `prazo_interno_anterior` (date), `prazo_interno_novo` (date), `resolvido_em` (timestamp).

**`sincronizacao`**: `iniciada_em`, `finalizada_em`, `sucesso`, `tarefas_novas`, `tarefas_atualizadas`, `tarefas_arquivadas`, `reaberturas`, `erro` (text).

Migration: `20261103_Inicial` no projeto do módulo.

### 3. Algoritmo da sincronização

```
usuario = asana.ObterUsuarioAtual()
upsert empresas a partir de usuario.Workspaces
para cada empresa com sincronizar = true:
    cargaInicial = empresa.carga_inicial_em is null
    abertas = asana.ListarTarefasAbertas(empresa.gid)
    para cada t em abertas:
        local = buscar por asana_gid
        se local não existe:
            criar (status = cargaInicial ? em_andamento : entrada,
                   prazo_interno = cargaInicial ? t.PrazoEm : null)
            importar comentários (lido = true)
        senão:
            atualizar campos do Asana
            se t.ModificadaEm > local.asana_modificada_em:
                stories = asana.ListarStories(t.Gid)
                gravar comentários novos (lido = false)
                aplicar regra de reabertura (seção 4)
            se local.status = arquivada: aplicar regra de desarquivamento (seção 5)
    para cada local da empresa fora de abertas e com status ∉ {concluida, arquivada}:
        r = asana.ObterTarefa(local.gid)
        arquivar com motivo: null → excluida; r.Concluida → concluida_no_asana;
                             r.ResponsavelGid ≠ usuario.Gid → reatribuida
    se cargaInicial: empresa.carga_inicial_em = agora
gravar sincronizacao com totais
```

- Cada empresa é salva em uma transação própria. Se uma empresa falhar, as anteriores ficam gravadas, a sincronização é registrada com `sucesso=false` e o erro, e a resposta segue a tradução de erros do módulo `Asana`. Por ser idempotente, basta sincronizar de novo.
- Tarefas `concluida` que não vêm na lista estão aguardando teste: não são consultadas uma a uma, para economizar chamadas.
- Concorrência: um `SemaphoreSlim` singleton. Uma segunda chamada simultânea recebe `409`.

### 4. Regra de reabertura

Para uma tarefa local com `status = concluida` que voltou na lista de abertas:

- cada story `MarcadaIncompleta` com `CriadaEm > concluida_em` e `asana_story_gid` ainda não registrado gera um `retrabalho`;
- se nenhuma story assim existir (fallback), gera um `retrabalho` com `reaberta_em = agora`, `reaberta_por = null` e `asana_story_gid = null`;
- em seguida: `status = retrabalho`, `prazo_interno_pendente = true` e `retrabalho.prazo_interno_anterior = prazo_interno`.

Se houver mais de uma story de reabertura desde a última conclusão, cada uma gera um retrabalho, numerados em ordem cronológica.

Uma tarefa concluída **antes** de existir no Actum e reaberta depois não tem registro local, então entra como tarefa nova (`entrada`), sem histórico.

### 5. Desarquivamento

Uma tarefa `arquivada` que volta na lista de abertas:

- se `motivo_arquivamento = concluida_no_asana` e há story `MarcadaIncompleta` posterior a `arquivada_em`: vira `retrabalho` pela regra da seção 4, usando `arquivada_em` como referência;
- nos demais casos: volta para `entrada`.

### 6. Endpoints (módulo `Tarefas`)

| Método | Rota | Resposta |
|---|---|---|
| POST | `/sincronizacoes` | `200` com o resumo; `409` se já houver uma em andamento |
| GET | `/sincronizacoes/ultima` | `200` com a última; `404` se nunca houve |
| GET | `/empresas` | `200` lista |
| PATCH | `/empresas/{identifier}` | `200`; altera `cor` e `sincronizar` |

## Risks / Trade-offs

- **[`modified_at` muda por motivos irrelevantes (ex.: alguém seguiu a tarefa)]** → Só gera uma chamada extra de stories; aceito.
- **[Carga inicial sem branches]** → As tarefas da carga inicial ficam `em_andamento` sem branches. O ciclo de vida permite informá-las depois e exige as duas antes de concluir.
- **[Reabertura feita e desfeita entre duas sincronizações]** → Se a tarefa estiver concluída de novo na sincronização, ela não vem na lista e o retrabalho não é detectado. Aceito: não houve trabalho para o usuário.

## Migration Plan

Primeira migration do módulo (`20261103_Inicial`), aplicada com `dotnet ef database update`. Rollback: `dotnet ef database update 0` no contexto do módulo.
