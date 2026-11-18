# Tasks

## 1. Shared: executor de processos

- [x] 1.1 Criar em `Actum.Shared.Infrastructure` o `IExecutorDeProcessos`/`ExecutorDeProcessos` (com `ComandoExterno`, `ResultadoDoProcesso`, `TempoEsgotadoException` e `ExecutavelNaoEncontradoException`) conforme o design: `ArgumentList`, leitura simultânea de stdout/stderr em UTF-8, tempo limite com `Kill(entireProcessTree: true)` e remoção de `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_HTTP_PORTS` e `Cors__*` do ambiente. Verificar com `dotnet build`
- [x] 1.2 Testes em `Actum.Shared.Infrastructure.Tests`, usando `pwsh`/`powershell` como processo filho: código de saída e saídas capturadas, argumento com espaço e aspas chegando intacto, tempo esgotado encerrando o processo, executável inexistente e `ASPNETCORE_URLS` não repassado. Verificar com `dotnet test`

## 2. Módulo Git

- [x] 2.1 Criar o projeto `Actum.Modules.Git` (`GitModule` sem banco nem endpoints, opções `Git:TempoLimite*` com padrões, `GitException` e filhas, `GitExceptionHandler` com `400`/`502`, `TravasDosClones` singleton) e registrá-lo no `ModuleCatalog` e na solução. Verificar com `dotnet build`
- [x] 2.2 Implementar `IGitLocal`/`GitLocal` com os métodos e comandos da tabela do design (`ObterOrigemAsync`, `BuscarBranchAsync`, `ListarWorktreesAsync`, `AdicionarWorktreeAsync`, `EhIgnoradoAsync`, `ObterPendenciasAsync`, `RemoverWorktreeAsync`, `LimparWorktreesOrfaosAsync`), com `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`, `-c core.quotepath=false`, interpretação só de saída porcelain e stderr limitado na mensagem. Interface com XML doc, `<author>`, `<created>` e `<modified>`
- [x] 2.3 Criar `Actum.Modules.Git.Tests` com git real em pasta temporária (um repositório `--bare` como `origin` e um clone dele), cobrindo os cenários da spec `git-local`: branch com barra e caminho com espaço, fetch de branch nova e de remoto inexistente, worktree com branch local inexistente (upstream configurado) e existente, branch em checkout no clone, listagem, pendências (limpo, ignorado, alteração, commit não enviado, sem upstream), remoção e limpeza de pasta apagada à mão, e duas criações simultâneas no mesmo clone. Verificar com `dotnet test`

## 3. Módulo Tarefas: domínio e banco

- [x] 3.1 Em `Empresa`, adicionar `CloneFront`, `CloneBack`, `PastaWorktrees`, `ArquivosFront`, `ArquivosBack` e os métodos `Clone(parte)` e `Arquivos(parte)`, com mapeamento em snake_case (`text[]` para as listas)
- [x] 3.2 Criar `Worktree`, `WorktreeParte` e o enum `SituacaoWorktree` (snake_case), com `Worktree` 0..1 em `TarefaBranch`, a situação geral calculada, a reativação de um worktree removido e o cálculo de `PastaDaBranch`. Incluir o worktree no `TarefaCiclo.CarregarAsync`. Testes de unidade de `PastaDaBranch` (barra, caracteres reservados, ponto final) e da situação geral
- [x] 3.3 Fazer `Tarefa.RemoverBranch` recusar com `DomainException` uma branch com worktree ativo. Teste em `CicloDeVidaTests`
- [x] 3.4 Gerar a migration `20261112_WorktreesDasBranches` no `TarefasDbContext`, aplicá-la no banco local e conferir com `\d tarefas.empresa`, `\d tarefas.worktree` e `\d tarefas.worktree_parte` no psql (nomes em snake_case, `timestamp without time zone`, índice único de `tarefa_branch_id` e de `(worktree_id, parte)`)

## 4. Módulo Tarefas: empresas

- [x] 4.1 Incluir os campos novos em `EmpresaResponse` e `AlterarEmpresaRequest` (mensagens de validação em pt-BR) e a validação do design no `EmpresaService` (caminho absoluto, até 260 caracteres, clone com `origin` igual ao repositório da parte via `IGitLocal.ObterOrigemAsync`, arquivos relativos sem `..`, até 20 e sem repetição, nada gravado quando um campo é inválido)
- [x] 4.2 Criar `GitLocalFalso` nos testes de Tarefas (no padrão do `GitHubClientFalso`): clones com origem, branches remotas, worktrees registrados, arquivos ignorados e pendências em memória, registro das chamadas e falha simulável por clone e operação. Cobrir os cenários de `company-repositories` para a configuração local. Verificar com `dotnet test`

## 5. Módulo Tarefas: worktrees

- [x] 5.1 Implementar `WorktreesLocais.CriarAsync` com o fluxo do design (condição de configuração, partes independentes, fetch, conferência pela listagem, limpeza de órfãos, `worktree add`, cópia dos arquivos com `EhIgnoradoAsync` e sem sobrescrever, falhas gravadas na parte e registradas em log)
- [x] 5.2 Chamar `WorktreesLocais.CriarAsync` depois do `SaveChangesAsync` em `TarefaService.IniciarAsync`, `RetomarAsync` (com branch) e `AdicionarBranchAsync` (implementação), gravando o resultado. Verificar com testes dos cenários de "Criação dos worktrees": duas partes, só back, empresa sem configuração sem chamar o git, falha numa parte com `200`, retomada com branch nova e branch base
- [x] 5.3 Implementar `POST /tarefas/{identifier}/branches/{branchIdentifier}/worktree` (`CriarWorktree`) em `TarefaEndpoints`. Verificar com testes dos cenários de "Nova tentativa" e de "Arquivos copiados" (usando arquivos reais numa pasta temporária)
- [x] 5.4 Implementar a remoção (`RemoverWorktree`, `DELETE .../worktree`) com `TarefaResultado.WorktreeComPendencias` → `409`, gravando cada parte ao terminar e apagando a pasta vazia. Verificar com testes dos cenários de "Remoção dos worktrees"
- [x] 5.5 Incluir `WorktreeResponse`/`WorktreeParteResponse` em `BranchResponse`, com `comandoClaude` montado com aspas simples. Testes de "Worktree no detalhe da tarefa", incluindo apóstrofo e branch sem worktree (`null`)

## 6. Documentação e integração

- [x] 6.1 Documentar no OpenAPI os campos e os endpoints novos e conferir em `http://localhost:5180/openapi/v1.json` que `cloneFront`, `pastaWorktrees`, `arquivosFront`, `worktree`, `comandoClaude` e as duas rotas `.../worktree` aparecem
- [x] 6.2 Documentar no README (ou em `docs/especificacao-fase1.md`) os pré-requisitos (git no `PATH`, credenciais no Git Credential Manager, `git config --global core.longpaths true`, pasta de worktrees curta) e a configuração das empresas
- [x] 6.3 Verificação integrada: com uma empresa real configurada, iniciar uma tarefa com front e back e conferir as pastas, `git worktree list` nos dois clones, o upstream (`git -C <worktree> status -sb`) e os arquivos copiados. Abrir o Claude Code com o `comandoClaude`, fazer um commit sem enviar e confirmar o `409` na remoção. Depois do `git push`, confirmar a remoção. Rodar `dotnet build` e `dotnet test` sem falhas

## Workflow follow-up

- Fazer a change `worktrees-por-tarefa` no ActumUi: regenerar os tipos, campos novos na página Empresas e, em `BranchesDaTarefa`, a pasta e a situação por parte com os botões "Copiar comando do Claude", "Recriar worktree" (quando `falhou` ou `removido`) e "Remover worktree" (mostrando a mensagem do `409`).
- Próxima change da API: portas por tarefa (slot em `worktree`), `.env.development.local` do front, scripts e `CLAUDE.md` gerados na pasta da tarefa, e o roteiro para o Firewall do Windows.
- Arquivar a change depois da revisão.
