# Tasks

## 1. Actum.Modules.Asana

- [x] 1.1 Passar o tempo limite para cada chamada (`CancellationTokenSource` com 30 s nas operações atuais e 5 min no upload), com `HttpClient.Timeout` infinito, sem mudar o comportamento das operações existentes. Verificar que os testes atuais de `Actum.Modules.Asana.Tests` passam.
- [x] 1.2 Declarar em `IAsanaClient` (XML doc completo, datas 2026-11-03) `ListarParticipantesAsync`, `AnexarArquivoAsync` e `ComentarAsync`, com os contratos `AsanaUsuarioResumo` e `AsanaAnexo`, e criar `AsanaRequisicaoRecusadaException`. Verificar que `dotnet build` passa sem CS1591.
- [x] 1.3 Implementar as três operações em `AsanaClient`:
  - seguidores com `404` → `null`;
  - upload `multipart/form-data` com `parent`, nome e tipo;
  - `html_text` com menções, texto escapado e quebras de linha;
  - `400`/`413`/`422` em escrita → requisição recusada.

  Verificar com `dotnet build`.
- [x] 1.4 Testes com `HttpMessageHandlerFalso`:
  - participantes (lista e `404`);
  - corpo multipart do upload (`parent`, nome, `Content-Type`);
  - `html_text` com menções, escape (`<`, `&`, aspas) e quebras de linha;
  - recusa `413` → `AsanaRequisicaoRecusadaException`;
  - `429` repetido no upload;
  - token nunca no log.

  Verificar que `dotnet test` passa.

## 2. Actum.Modules.Tarefas

- [x] 2.1 Declarar `IComentarioService` (XML doc completo) e implementar `ListarParticipantesAsync`:
  - sugestão pelo autor da reabertura mais recente ou do comentário mais recente de outra pessoa, comparando nomes sem maiúsculas e acentos;
  - tarefa inexistente → `404`;
  - tarefa excluída no Asana → `DomainException`.

  Verificar com `dotnet build`.
- [x] 2.2 Implementar `EnviarAsync`:
  - validações (texto ou arquivo, limites, menções entre os participantes, tarefa arquivada);
  - anexos em ordem;
  - comentário com menções, texto e linha "Anexos:";
  - gravação local do comentário como lido;
  - `EnvioParcialException` com `anexosEnviados`.

  Verificar com `dotnet build`.
- [x] 2.3 Criar `Endpoints/ComentarioEndpoints`:
  - `GET /tarefas/{identifier}/participantes` e `POST /tarefas/{identifier}/comentarios` (multipart, `DisableAntiforgery`, limite de 52 MB no corpo);
  - DTOs `ParticipanteResponse` e `ComentarioEnviadoResponse`;
  - handler de `EnvioParcialException` com a extensão `anexosEnviados`;
  - metadados OpenAPI.

  Verificar em `/scalar/v1`.

## 3. Testes

- [x] 3.1 `Actum.Modules.Tarefas.Tests` com `AsanaClientFalso` estendido:
  - participantes e sugestão (reabertura, comentário de outra pessoa, ninguém);
  - envio com texto, menção e anexos (ordem e linha "Anexos:");
  - só anexos; nada para enviar;
  - menção fora dos participantes; limites de quantidade, tamanho e vazio;
  - tarefa arquivada; tarefa excluída no Asana;
  - comentário gravado como lido e não duplicado pela sincronização seguinte;
  - falha parcial com `anexosEnviados`.

  Verificar que `dotnet test` passa.
- [x] 3.2 `Actum.Api.Tests`:
  - `POST` multipart real pelo pipeline (`201` com JSON em camelCase);
  - `400` sem conteúdo e com arquivo grande demais, mensagens em pt-BR;
  - `404`;
  - Problem Details `502` com `anexosEnviados` na falha parcial;
  - `GET /participantes`.

  Verificar que `dotnet test` passa.
- [ ] 3.3 Teste manual no Asana real (escrita visível a terceiros), somente com autorização do usuário e numa tarefa escolhida por ele: comentário com menção e um print. Conferir no Asana e no detalhe do Actum.

## Workflow follow-up

- Propor a change do front no ActumUI (caixa de comentário com anexos, Ctrl+V, menções e "Enviar dúvidas ao Asana") e regenerar os tipos.
- Arquivar a change com `/opsx:archive` após revisão e enviar ao GitHub.
