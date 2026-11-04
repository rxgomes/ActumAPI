# Design

## Context

O módulo `Asana` faz todas as chamadas por `AsanaClient.EnviarAsync` (JSON com envelope `data`, token do `Asana:Token`, nova tentativa em `429`, `AsanaException` → `AsanaExceptionHandler` em `400`/`502`, tempo limite de 30 s no `HttpClient`). O módulo `Tarefas` guarda os comentários (`comentario`, único por `asana_story_gid`) e os retrabalhos (`reaberta_por`), só com o nome dos autores, sem o `gid` do Asana.

Decisões do usuário: anexos vão junto com o comentário; o comentário menciona quem pediu; as dúvidas da IA preenchem a caixa para revisão (front).

## Goals / Non-Goals

**Goals**
- Um envio único (texto + menções + arquivos) a partir do detalhe da tarefa.
- Menções que de fato notificam no Asana.
- Comentário visível no Actum imediatamente, sem duplicar na sincronização.
- Não reenviar anexos quando um passo posterior falha.

**Non-Goals**
- Edição/exclusão de comentários, download de anexos, formatação rica, menção a qualquer pessoa do workspace.

## Decisions

### 1. Menções a partir dos seguidores, lidos na hora

- O `gid` do usuário não é guardado pela sincronização. Em vez de mudar a sincronização e migrar dados, a lista de quem pode ser mencionado vem dos **seguidores da tarefa** (`GET /tasks/{gid}?opt_fields=followers.name`). No Asana, quem cria a tarefa, quem é atribuído e quem comenta passam a segui-la, então isso cobre quem pediu e quem reabriu.
- A sugestão compara nomes, sem diferenciar maiúsculas e acentos: o autor da reabertura mais recente (`retrabalho.reaberta_por`) ou, sem reabertura, o do comentário mais recente cujo autor não é o usuário do token (`GET /users/me`).
- Alternativa descartada: gravar `autor_gid` em `comentario` e `retrabalho`. Exigiria migration, reprocessar o histórico e ainda não cobriria quem só criou a tarefa.

### 2. Novas operações em `IAsanaClient` (XML doc com `<author>`, `<created>` e `<modified>` = 2026-11-03)

- `ListarParticipantesAsync(tarefaGid)` → `IReadOnlyList<AsanaUsuarioResumo>(Gid, Nome)`, ou `null` quando o Asana responde `404`.
- `AnexarArquivoAsync(tarefaGid, nome, tipo, Stream conteudo)` → `AsanaAnexo(Gid, Nome)`:
  - `MultipartFormDataContent` com `parent` e `file` (nome e `Content-Type`);
  - tempo limite próprio de 5 minutos (`CancellationTokenSource` por chamada; o `HttpClient` passa a ter `Timeout` infinito e as demais operações mantêm 30 s pelo mesmo mecanismo);
  - nova tentativa só em `429` antes do envio do corpo. Na prática, `429` volta antes de o Asana aceitar o arquivo, então é seguro repetir; outras falhas não são repetidas.
- `ComentarAsync(tarefaGid, texto, mencoes)` → `AsanaStory` (`gid`, `created_at`, `text`):
  - `POST /tasks/{gid}/stories` com `{ "data": { "html_text": "<body>…</body>" } }`;
  - menções `<a data-asana-gid="gid"/>` separadas por espaço no início;
  - texto com `SecurityElement.Escape` e `\n` preservado;
  - a linha de anexos é montada pelo serviço do módulo `Tarefas`.
- Erro novo `AsanaRequisicaoRecusadaException(mensagem)` (`400`) para `400`, `413` e `422` do Asana em escritas. O `AsanaExceptionHandler` já devolve `400` para toda `AsanaException` que não é indisponibilidade.

### 3. Serviço `IComentarioService` no módulo `Tarefas`

- `ListarParticipantesAsync(tarefaIdentifier)` → `NaoEncontrada | Lista(ParticipanteResponse(Gid, Nome, Sugerido))`.
- `EnviarAsync(tarefaIdentifier, texto, mencoes, arquivos)`:
  1. Valida: tarefa existe (`404`), não arquivada (`DomainException`), texto ou arquivo, limites dos arquivos (até 10, 25 MB cada, 50 MB no total, nenhum vazio).
  2. Lê os participantes e valida que cada menção está entre eles (`DomainException`).
  3. Anexa os arquivos em ordem, guardando os nomes enviados.
  4. Publica o comentário: menções + texto + `Anexos: a, b` (quando há).
  5. Grava `Comentario(story.Gid, autor = nome do usuário do token, story.Text, story.CriadaEm local, lido = true)` na tarefa.
  - Falha nos passos 3 ou 4 com anexos já enviados: lança `EnvioParcialException(anexosEnviados, inner)`. Um `IExceptionHandler` do módulo responde com o status do erro original (`400`/`502`), uma mensagem que cita a parte enviada e a extensão `anexosEnviados`.
- O nome do usuário vem de `ObterUsuarioAtualAsync` (já existente); uma chamada por envio.

### 4. Endpoints (`Endpoints/ComentarioEndpoints`, grupo `/tarefas/{identifier:guid}`, tag `Comentários`)

- `GET /participantes` → `200 ParticipanteResponse[]`, `404`, `502`.
- `POST /comentarios` → `201 ComentarioEnviadoResponse(Comentario: ComentarioResponse, Anexos: string[])`, `400`, `404`, `502`.
  - Parâmetros `[FromForm] string? texto`, `[FromForm] string[]? mencoes`, `IFormFileCollection arquivos`.
  - `.DisableAntiforgery()`: API local sem cookies, chamada só pelo front.
  - `.WithMetadata(new RequestSizeLimitAttribute(52 MB))`: corpo do formulário com folga sobre os 50 MB de arquivos.
  - Validação dos limites no serviço (mensagens em pt-BR com o nome do arquivo); um `StringLength` em pt-BR no texto.
- Nenhuma alteração de banco: `comentario` já tem tudo o que é preciso (o índice único em `asana_story_gid` impede a duplicação na sincronização).

## Risks / Trade-offs

- **Escritas visíveis a terceiros:** comentários notificam pessoas. Os testes automáticos usam `AsanaClientFalso` e `HttpMessageHandlerFalso`. O teste real só acontece numa tarefa escolhida pelo usuário e com autorização dele.
- **Seguidor ausente:** se quem pediu deixou de seguir a tarefa, não haverá sugestão. O usuário ainda pode mencionar outro participante ou enviar sem menção.
- **Comparação por nome:** nomes iguais entre pessoas diferentes podem gerar sugestão errada. A sugestão é só um padrão editável no front.
- **Timeout do upload:** arquivos grandes em rede lenta. São 5 minutos por arquivo, e o limite de 25 MB mantém isso razoável.

## Migration Plan

Sem migration. Implantar a API e depois o front. Rollback: remover os endpoints. Os comentários já enviados ficam no Asana e no Actum.

## Open Questions

Nenhuma bloqueante.
