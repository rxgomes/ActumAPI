# Tasks

## 1. Módulo GitHub

- [x] 1.1 Criar o projeto `Actum.Modules.GitHub` (com `GitHubModule`, leitura de `GitHub:Token` a cada chamada como no Asana, `GitHubException` e `GitHubExceptionHandler`), registrá-lo no `Actum.Api` e na solução, e verificar com `dotnet build`
- [x] 1.2 Implementar `IGitHubClient`/`GitHubClient` com `ObterShaDaBranchAsync` (`404` resolvido com a consulta ao repositório) e `CriarBranchAsync` (`201` → `true`, `422` "Reference already exists" → `false`), com os cabeçalhos e o tempo limite do design, sem registrar cabeçalhos em log
- [x] 1.3 Criar o projeto de testes `Actum.Modules.GitHub.Tests` (com `HttpMessageHandlerFalso`, no padrão do Asana) cobrindo os cenários da spec `github-client`: cabeçalhos e token, token ausente sem chamada, branch com barra, branch inexistente, branch criada e já existente, `401`/`403`/`404` do repositório → `400`, tempo esgotado e `5xx` → `502`. Verificar com `dotnet test`

## 2. Módulo Tarefas: domínio e banco

- [x] 2.1 Adicionar o enum `ParteBranch` e, em `TarefaBranch`, as partes e a propriedade calculada `Tag`, no formato da spec. Verificar com testes de unidade de `Tag` (duas partes, uma parte, sem partes)
- [x] 2.2 Ampliar `NomeDaBranch` com as regras de nome do Git e fazer `Tarefa.Iniciar` e `Tarefa.AdicionarBranch` receberem as partes (pelo menos uma, sem repetição) e recusarem implementação sem base registrada. Verificar com testes em `CicloDeVidaTests` (nome com espaço recusado, nome com barra aceito, implementação sem base)
- [x] 2.3 Adicionar `RepositorioFront` e `RepositorioBack` em `Empresa` e o mapeamento das quatro colunas (`repositorio_front`, `repositorio_back`, `parte_front`, `parte_back`) em snake_case
- [x] 2.4 Gerar a migration `yyyyMMdd_RepositoriosEPartesDaBranch` no `TarefasDbContext`, aplicá-la no banco local e verificar as colunas com `\d tarefas.empresa` e `\d tarefas.tarefa_branch` no psql

## 3. Módulo Tarefas: empresas

- [x] 3.1 Incluir `repositorioFront` e `repositorioBack` em `EmpresaResponse` e em `AlterarEmpresaRequest` (com a validação de formato, espaços removidos nas pontas, `""` remove e `null` mantém) no `EmpresaService`. Verificar com testes dos cenários de `company-repositories` (definir, remover, formato inválido com `RepositorioFront` em `errors`, só a cor)

## 4. Módulo Tarefas: início e registro de branches no GitHub

- [x] 4.1 Criar um `GitHubClientFalso` nos testes de Tarefas (no padrão do `AsanaClientFalso`), que registre as chamadas e permita simular branch inexistente, branch já existente e falha
- [x] 4.2 Implementar `BranchesNoGitHub` (resolve o repositório da parte, conferindo todas as bases antes de criar) e usá-la em `TarefaService.IniciarAsync`, seguindo o fluxo do design. Incluir `Partes` obrigatório em `IniciarTarefaRequest`. Verificar com testes dos cenários de "Iniciar tarefa": início válido, só uma parte, já existente, base inexistente sem criação, parte sem repositório sem chamada, falha no segundo repositório sem gravar e nova tentativa, e erros de domínio sem chamada ao GitHub
- [x] 4.3 Usar `BranchesNoGitHub` em `TarefaService.AdicionarBranchAsync` (base: só conferir; implementação: criar a partir da base mais recente) e incluir `Partes` obrigatório em `AdicionarBranchRequest`. Verificar com testes dos cenários de "Registro de branches": carga inicial, retrabalho, implementação sem base, base inexistente, partes ausentes e nome repetido sem chamar o GitHub
- [x] 4.4 Incluir `partes` e `tag` em `BranchResponse` e verificar no `GET /tarefas/{identifier}` de um teste que uma branch antiga devolve `partes = []` e `tag` igual ao nome

## 5. Módulo Tarefas: conclusão e bases recentes

- [x] 5.1 Fazer `ConclusaoService` usar `TarefaBranch.Tag` na prévia, na comparação com as tags da tarefa e na criação da tag. Atualizar os testes de conclusão e incluir os cenários "Primeira conclusão" com partes, "Branches sem partes", "Retrabalho com branch nova" com `(Front) => ...` e "Tag já presente" pelo texto completo
- [x] 5.2 Implementar `GET /tarefas/{identifier}/branches-base-recentes` em `TarefaEndpoints` com a consulta do design. Verificar com testes dos cenários de "Bases recentes da empresa" (ordem e distinção, outra empresa, lista vazia, `404`)

## 6. Documentação e integração

- [x] 6.1 Documentar no OpenAPI (XML doc dos contratos e endpoints) os campos e o endpoint novos, inclusive o formato de `tag` e as partes, e conferir em `http://localhost:5180/openapi/v1.json` que `partes`, `tag`, `repositorioFront`, `repositorioBack` e `branches-base-recentes` aparecem
- [x] 6.2 Registrar `GitHub:Token` (user-secrets, permissão Contents: read and write) junto da configuração do Asana no README ou em `docs/especificacao-fase1.md`, e verificar que o texto indica o comando `dotnet user-secrets set "GitHub:Token" ...`
- [ ] 6.3 Verificação integrada: com o token configurado e os repositórios de uma empresa cadastrados, iniciar uma tarefa real com uma base existente e confirmar no GitHub que a branch foi criada nos repositórios das partes marcadas, e na prévia da conclusão as tags `(Front, Back) => ...`. Rodar `dotnet build` e `dotnet test` sem falhas

## Workflow follow-up

- Fazer a change `criar-branch-ao-iniciar` no ActumUi (regenerar os tipos e ajustar os diálogos e a página Empresas) antes de publicar, por causa do `partes` obrigatório.
- Arquivar a change depois da revisão.
