# Tasks

## 1. Módulo Tarefas: domínio

- [x] 1.1 Extrair de `Tarefa.Iniciar` o método privado `RegistrarBranchesDoCiclo`, que valida os nomes e as partes, devolve `null` para a implementação repetida, recusa a base registrada como implementação e reaproveita a base já registrada, vinculando as branches novas ao retrabalho aberto. Fazer `Iniciar` usá-lo. Verificar com testes em `CicloDeVidaTests`: tarefa desarquivada com `main` iniciada de novo com `main` registra só a implementação, implementação repetida devolve `null`, e os testes de início existentes continuam passando
- [x] 1.2 Criar `Tarefa.RetomarComBranch(base, impl, partes)` com as validações de `Retomar()` e `RegistrarBranchesDoCiclo`, ligando `EmAndamento`. Verificar com testes em `CicloDeVidaTests`:
  - retrabalho com a base `main` reaproveitada e a implementação vinculada ao retrabalho aberto;
  - base nova vinculada ao retrabalho;
  - base registrada como implementação recusada;
  - tarefa já em andamento, bloqueada ou na entrada recusada com a mesma mensagem do `Retomar`.
- [x] 1.3 Criar a propriedade calculada `Tarefa.NovaBranchSugerida`. Verificar com testes em `CicloDeVidaTests` (que já tem os auxiliares de retrabalho com branches): `true` logo depois da reabertura, `false` depois de uma implementação vinculada ao retrabalho aberto (por `RetomarComBranch` ou `AdicionarBranch`), e `false` fora do `retrabalho`

## 2. Módulo Tarefas: serviço, contratos e endpoint

- [x] 2.1 Criar `RetomarTarefaRequest` em `TarefaRequests.cs`, com as anotações e o XML doc no padrão de `IniciarTarefaRequest`. Alterar `ITarefaService.RetomarAsync` para receber `RetomarTarefaRequest?`, atualizando `<modified>`, e o `TarefaEndpoints` para receber o corpo opcional (`[FromBody] RetomarTarefaRequest?`), com a descrição do OpenAPI explicando a nova branch opcional
- [x] 2.2 Implementar o caminho com corpo em `TarefaService.RetomarAsync` (`RetomarComBranch`, então `BranchesNoGitHub.CriarImplementacaoAsync`, então `SaveChangesAsync`; `null` vira `BranchRepetida`) e tratar o `null` de `Iniciar` em `IniciarAsync` como `BranchRepetida`. Verificar com testes usando o `GitHubClientFalso`:
  - retrabalho com nova branch criada no repositório de back a partir de `main`;
  - retomar sem corpo sem chamar o GitHub;
  - base inexistente sem criar nem gravar, com a tarefa pausada;
  - falha do GitHub sem gravar;
  - implementação repetida com `409` sem chamar o GitHub;
  - início de tarefa desarquivada com a base reaproveitada.
- [x] 2.3 Incluir `NovaBranchSugerida` em `TarefaDetalheResponse` (com XML doc) e preenchê-lo no `PainelService`. Verificar com teste de API (`TarefaApiTests`) que `GET /tarefas/{identifier}` devolve `novaBranchSugerida = true` para uma tarefa em `retrabalho` sem implementação vinculada
- [x] 2.4 Verificar com testes de API que `POST /tarefas/{identifier}/retomar` sem corpo continua com `200`, e que com corpo incompleto responde `400` com `BranchImplementacao` e `Partes` em `errors`

## 3. Documentação e integração

- [x] 3.1 Conferir em `http://localhost:5180/openapi/v1.json` que `RetomarTarefaRequest` aparece como corpo opcional de `/tarefas/{identifier}/retomar` e que `novaBranchSugerida` aparece em `TarefaDetalheResponse`
- [ ] 3.2 Verificação integrada: com o token do GitHub configurado, reabrir no Asana uma tarefa concluída, sincronizar, retomar com uma nova branch de implementação e confirmar no GitHub que ela foi criada a partir da base, e na prévia da conclusão que só a tag nova será adicionada. Rodar `dotnet build` e `dotnet test` sem falhas

## Workflow follow-up

- Arquivar a `criar-branch-ao-iniciar` antes desta, porque o delta de `Iniciar tarefa` parte do texto dela.
- Fazer a change `nova-branch-ao-reiniciar` no ActumUI depois desta.
- Arquivar a change depois da revisão.
