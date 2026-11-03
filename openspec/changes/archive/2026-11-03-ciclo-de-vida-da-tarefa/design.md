# Design

## Context

Depende de `sincronizacao-de-tarefas` (tabelas `tarefa` e `retrabalho`, status) e de `integracao-asana`. Decisões do usuário: o nome da tag é exatamente o nome da branch digitado; as tags só vão para o Asana na conclusão; no retrabalho, normalmente se usa a mesma branch (concluir sem duplicar) e, às vezes, uma branch nova (adicionar a nova e manter a antiga).

## Goals / Non-Goals

**Goals:**
- Transições de status explícitas e validadas no domínio.
- Conclusão segura para repetir: falhas parciais não duplicam tags nem deixam o status local inconsistente.

**Non-Goals:**
- Desfazer uma conclusão pelo Actum (a reabertura acontece no Asana e é detectada na sincronização).

## Decisions

### 1. Tabela `tarefa_branch`

Colunas da entidade base, mais:

| Coluna | Tipo | Observação |
|---|---|---|
| tarefa_id | bigint | FK `tarefa` |
| tipo | varchar(20) | `base` ou `implementacao` |
| nome | varchar(250) | sem espaços nas pontas; vira o nome da tag |
| retrabalho_id | bigint | FK `retrabalho`, null quando não criada em retrabalho |
| asana_tag_gid | varchar(50) | preenchido no envio |
| enviada_asana_em | timestamp | null até ser adicionada no Asana |

Índice único (`tarefa_id`, `nome`). Migration `yyyyMMdd_TarefaBranch`, com a data da implementação.

### 2. Transições

| Ação | Origem permitida | Destino | Regras |
|---|---|---|---|
| Iniciar | `entrada` | `em_andamento` | base, implementação e prazo interno obrigatórios; base ≠ implementação |
| Bloquear | `em_andamento`, `retrabalho` | `bloqueada` | guarda `status_anterior` |
| Desbloquear | `bloqueada` | `status_anterior` | |
| Concluir | `em_andamento`, `bloqueada`, `retrabalho` | `concluida` | ao menos uma branch `base` e uma `implementacao` |

Transição inválida → `DomainException` (`400`) com mensagem em pt-BR que cita o status atual. A tarefa da carga inicial (`em_andamento` sem branches) informa as branches pelo endpoint de branches antes de concluir.

### 3. Branches

`POST /tarefas/{identifier}/branches { tipo, nome }`:

- permitido em `em_andamento`, `bloqueada` e `retrabalho`;
- nome repetido na tarefa → `409`;
- se existir retrabalho aberto (`resolvido_em` nulo), grava o `retrabalho_id`;
- `DELETE /tarefas/{identifier}/branches/{branchIdentifier}` só é permitido para branches ainda não enviadas (`enviada_asana_em` nulo), para corrigir erros de digitação.

### 4. Prazo interno e prioridade

`PATCH /tarefas/{identifier} { prazoInterno?, prioridade? }` em qualquer status, exceto `arquivada`. Quando `prazo_interno_pendente = true` e um prazo é informado:

- `retrabalho.prazo_interno_novo` (do retrabalho aberto) ← prazo;
- `prazo_interno_pendente` ← `false`.

### 5. Conclusão

`GET /tarefas/{identifier}/conclusao/previa` → lista as branches com `enviada_asana_em` nulo (as tags que serão adicionadas) e se a conclusão é permitida (com o motivo, se não for).

`POST /tarefas/{identifier}/concluir`:

```
validar transição e branches obrigatórias
remota = asana.ObterTarefa(gid)            → null: DomainException "a tarefa não existe mais no Asana"
para cada branch pendente (ordem: base, implementação, data):
    se remota.Tags contém nome exato → marcar enviada (sem chamada)
    senão tag = asana.ObterOuCriarTag(workspace, nome); asana.AdicionarTag(gid, tag.Gid); marcar enviada
    SaveChanges                             ← gravado a cada tag
asana.ConcluirTarefa(gid)
status = concluida; concluida_em = agora; retrabalho aberto.resolvido_em = agora; SaveChanges
```

- Salvar a cada tag faz a nova tentativa, depois de uma falha, retomar sem duplicar.
- Se o Asana falhar antes do `ConcluirTarefa`, o status local não muda e o erro segue a tradução do módulo `Asana` (`502`/`400`).
- Retrabalho na mesma branch: não há branch pendente, então só conclui.

## Risks / Trade-offs

- **[Conclusão no Asana bem-sucedida, mas falha ao gravar o status local]** → Na nova tentativa, as tags já estão marcadas e `ConcluirTarefa` é idempotente no Asana, então o resultado final é correto.
- **[Usuário remove a tag no Asana manualmente]** → O Actum considera a branch como enviada e não a reenvia. Aceito.

## Migration Plan

Migration `yyyyMMdd_TarefaBranch`, aditiva. Rollback: reverter para `20261103_Inicial`.
