# Design

## Context

O usuário usa o mesmo login nos dois workspaces do Asana (Aureum-ERP e aremas.com.br), então um único Personal Access Token acessa ambos. A API do Asana responde em JSON no envelope `{ "data": ..., "next_page": { "offset": ... } }`, aceita `opt_fields` para escolher campos e limita a taxa de requisições, respondendo `429` com `Retry-After`. Depende de `setup-inicial-da-solucao`.

## Goals / Non-Goals

**Goals:**
- Expor ao módulo `Tarefas` só o necessário para a Fase 1, com tipos próprios (sem vazar o JSON do Asana).
- Ser testável com `HttpMessageHandler` falso, sem rede.

**Non-Goals:**
- Cobrir a API do Asana de forma genérica.
- Cache entre requisições HTTP do Actum.

## Decisions

### 1. Contrato público

```csharp
public interface IAsanaClient
{
    Task<AsanaUsuario> ObterUsuarioAtualAsync(CancellationToken ct);                     // inclui workspaces
    Task<IReadOnlyList<AsanaTarefa>> ListarTarefasAbertasAsync(string workspaceGid, CancellationToken ct);
    Task<AsanaTarefa?> ObterTarefaAsync(string tarefaGid, CancellationToken ct);         // null em 404
    Task<IReadOnlyList<AsanaStory>> ListarStoriesAsync(string tarefaGid, CancellationToken ct);
    Task<AsanaTag> ObterOuCriarTagAsync(string workspaceGid, string nome, CancellationToken ct);
    Task AdicionarTagAsync(string tarefaGid, string tagGid, CancellationToken ct);
    Task ConcluirTarefaAsync(string tarefaGid, CancellationToken ct);
}
```

Records de retorno (em `Contracts/`):

- `AsanaUsuario(Gid, Nome, IReadOnlyList<AsanaWorkspace> Workspaces)`
- `AsanaWorkspace(Gid, Nome)`
- `AsanaTarefa(Gid, Nome, Notas, NotasHtml, PrazoEm: DateOnly?, Concluida, ModificadaEm: DateTime, Link, Projetos: IReadOnlyList<string>, Tags: IReadOnlyList<AsanaTag>, ResponsavelGid: string?)`
- `AsanaStory(Gid, Tipo: AsanaStoryTipo, CriadaEm: DateTime, AutorNome, Texto)`, onde `AsanaStoryTipo` é `Comentario`, `MarcadaIncompleta`, `MarcadaConcluida` ou `Outro`, mapeado de `resource_subtype` (`comment_added`, `marked_incomplete`, `marked_complete`)
- `AsanaTag(Gid, Nome)`

Datas do Asana chegam em UTC (ISO 8601) e são convertidas para horário local com `Kind=Unspecified`, por causa da regra de `timestamp without time zone` da fundação.

### 2. Chamadas à API

| Operação | Requisição |
|---|---|
| Usuário atual | `GET /users/me?opt_fields=name,workspaces.name` |
| Tarefas abertas | `GET /tasks?assignee=me&workspace={gid}&completed_since=now&limit=100&opt_fields=name,notes,html_notes,due_on,completed,modified_at,permalink_url,projects.name,tags.name,assignee` |
| Tarefa | `GET /tasks/{gid}?opt_fields=` (mesmos campos) |
| Stories | `GET /tasks/{gid}/stories?limit=100&opt_fields=resource_subtype,created_at,created_by.name,text` |
| Tags do workspace | `GET /workspaces/{gid}/tags?limit=100&opt_fields=name` |
| Criar tag | `POST /workspaces/{gid}/tags` com `{ "data": { "name": ... } }` |
| Adicionar tag | `POST /tasks/{gid}/addTag` com `{ "data": { "tag": ... } }` |
| Concluir | `PUT /tasks/{gid}` com `{ "data": { "completed": true } }` |

`completed_since=now` faz o Asana devolver só tarefas não concluídas.

### 3. Tag por nome

`ObterOuCriarTagAsync` percorre as tags do workspace e compara o nome **exato** (ordinal, sensível a maiúsculas), porque o nome da tag é o nome da branch digitado pelo usuário, e branches do Git diferenciam maiúsculas. Se não encontrar, cria a tag.

### 4. Resiliência e erros

- `HttpClient` tipado via `IHttpClientFactory`, base `https://app.asana.com/api/1.0/` e timeout de 30 s.
- `429`: aguarda o `Retry-After` (em segundos) e tenta de novo, até 3 vezes.
- Token ausente: `AsanaNaoConfiguradoException`. `401`/`403`: `AsanaAcessoNegadoException`. Rede, timeout, `5xx` e `429` esgotado: `AsanaIndisponivelException`. As três herdam de `AsanaException` e têm mensagem em pt-BR.
- Um handler de exceção do módulo traduz `AsanaNaoConfiguradoException` e `AsanaAcessoNegadoException` em `400` e `AsanaIndisponivelException` em `502`, sempre em Problem Details. A tradução vale para qualquer endpoint, inclusive os do módulo `Tarefas`.
- O token MUST NOT aparecer em logs: o log de requisições do `HttpClient` é configurado para não registrar cabeçalhos.

### 5. Endpoint de verificação

`GET /asana/conexao` → `200 { configurado, valido, usuario, workspaces[] }`. Não lança erro quando o token falta ou é inválido: responde `configurado=false` ou `valido=false`, para o front mostrar o aviso.

## Risks / Trade-offs

- **[Workspaces com muitas tags deixam a busca por nome lenta]** → Aceito na Fase 1 (é só uma busca por conclusão). Se for um problema, usar `GET /workspaces/{gid}/typeahead?resource_type=tag`.
- **[Mudanças na API do Asana]** → Os tipos próprios isolam o resto do sistema; só o mapeamento do módulo precisa mudar.

## Migration Plan

Sem banco. Implantação: merge na `main` e configuração do user-secret.
