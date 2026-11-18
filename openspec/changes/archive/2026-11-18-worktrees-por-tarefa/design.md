# Design

## Context

- `TarefaService.IniciarAsync`, `RetomarAsync` (com branch nova) e `AdicionarBranchAsync` (tipo `implementacao`) chamam `BranchesNoGitHub.CriarImplementacaoAsync` antes do `SaveChangesAsync`. Se o GitHub falhar, nada é gravado. `IGitHubClient.CriarBranchAsync` aceita uma branch que já existe, então a nova tentativa é segura.
- Nada acontece nos clones locais. O usuário tem um clone do front e um do back por empresa e troca de branch à mão.
- A API roda no PC do usuário. `Actum-Logon.ps1` e `Actum-Iniciar.ps1` sobem o `Actum.Api.exe` com `Start-Process`, como processo do usuário logado. O processo tem no ambiente `ASPNETCORE_URLS=http://localhost:5280`, `ASPNETCORE_ENVIRONMENT=Development` e `Cors__AllowedOrigins__0`.
- Integrações seguem o padrão do GitHub: módulo próprio sem banco nem endpoints, contrato público (`IGitHubClient`), hierarquia de exceções em pt-BR e um `IExceptionHandler` que traduz as exceções em `400`/`502`. Os testes de Tarefas usam fakes em memória (`GitHubClientFalso`).
- Não há serviços em segundo plano na API. Concorrência por processo é controlada com singletons (`SincronizacaoEmAndamento`, `AnalisesEmAndamento`).

## Goals / Non-Goals

**Goals:**
- Criar os worktrees de cada branch de implementação sem que uma falha local (git, rede, pasta) desfaça ou impeça a criação no GitHub.
- Nova tentativa idempotente: recriar completa só o que falta e não apaga nada.
- Remoção segura: nunca perder trabalho não commitado ou não enviado.

**Non-Goals:**
- Portas, proxy, preparo de dependências e abertura de IDE/terminal (changes seguintes). O desenho deixa o lugar do slot reservado na tabela `worktree`, mas não cria a coluna agora.
- Sincronizar o estado com mudanças manuais feitas fora do Actum, além do necessário para a idempotência (ver "Worktree apagado à mão").

## Decisions

### Executor de processos em `Actum.Shared.Infrastructure`
`IExecutorDeProcessos.ExecutarAsync(ComandoExterno comando, CancellationToken)` devolve `ResultadoDoProcesso(int CodigoDeSaida, string Saida, string Erro)`. `ComandoExterno` contém executável, `ArgumentList`, pasta de trabalho, tempo limite e variáveis extras.

- `UseShellExecute = false`, `CreateNoWindow = true` e argumentos por `ArgumentList`, sem shell. Assim os nomes de branch e os caminhos não passam por interpretação de aspas.
- stdout e stderr são lidos ao mesmo tempo (`ReadToEndAsync` dos dois antes do `WaitForExitAsync`), em UTF-8.
- Tempo limite por `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter`. No estouro, `Kill(entireProcessTree: true)` e `TempoEsgotadoException`.
- Ambiente: remove `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_HTTP_PORTS` e qualquer `Cors__*` herdados do Actum, depois aplica as variáveis extras. O git não precisa disso agora. A regra fica no executor para que os processos das próximas changes (`dotnet run` do back da empresa, terminal) não subam na porta 5280 do Actum.
- Executável ausente (`Win32Exception` no `Start`) vira `ExecutavelNaoEncontradoException`.

O executor fica em Shared por ser utilitário sem regra de negócio. O módulo Git o usa agora, e o lançador de IDE/terminal o usará depois.

Alternativa considerada: CliWrap ou LibGit2Sharp. Foi descartada porque o LibGit2Sharp não tem suporte completo a worktrees nem usa o Git Credential Manager do usuário. Para o CliWrap, o executor próprio tem cerca de 80 linhas e não traz dependência nova.

### Novo módulo `Actum.Modules.Git`
Contrato público `IGitLocal`. Todos os métodos recebem caminhos absolutos:

| Método | Comando |
|---|---|
| `ObterOrigemAsync(clone)` → `string?` | `git -C <clone> rev-parse --show-toplevel` + `git -C <clone> remote get-url origin`; `null` se não for repositório |
| `BuscarBranchAsync(clone, branch)` | `git -C <clone> fetch --no-tags origin +refs/heads/<b>:refs/remotes/origin/<b>` |
| `ListarWorktreesAsync(clone)` → `[(Caminho, Branch?)]` | `git -C <clone> worktree list --porcelain` |
| `AdicionarWorktreeAsync(clone, caminho, branch)` | Sem branch local (`show-ref --verify --quiet refs/heads/<b>`): `worktree add -b <b> <caminho> origin/<b>`; com branch local: `worktree add <caminho> <b>` |
| `EhIgnoradoAsync(clone, arquivo)` → `bool` | `git -C <clone> check-ignore -q -- <arquivo>` (saída 0 = ignorado, 1 = não ignorado) |
| `ObterPendenciasAsync(worktree)` → `PendenciasDoWorktree(int Alteracoes, int CommitsNaoEnviados)` | `status --porcelain` (linhas) e `rev-list --count @{u}..HEAD`; sem upstream, `rev-list --count HEAD --not --remotes=origin` |
| `RemoverWorktreeAsync(clone, caminho)` | `git -C <clone> worktree remove <caminho>` (sem `--force`) |
| `LimparWorktreesOrfaosAsync(clone)` | `git -C <clone> worktree prune` |

- O refspec explícito do fetch atualiza `origin/<b>` mesmo em clones `--single-branch`. O `-b <b> ... origin/<b>` cria a branch local já com upstream (`branch.autoSetupMerge`), então o `git push` no worktree funciona sem argumentos.
- Variáveis em todos os comandos: `GIT_TERMINAL_PROMPT=0` e `GCM_INTERACTIVE=never`. Credencial expirada falha na hora, em vez de abrir um prompt que ninguém vê.
- `-c core.quotepath=false` em todos os comandos, para caminhos com acento sairem legíveis.
- Só a saída `--porcelain` é interpretada, porque as mensagens do git podem vir traduzidas. O stderr vai para o log e para a mensagem da exceção, limitado a 500 caracteres.
- Tempos limite (`Git:TempoLimite*`, com padrões): fetch 60s, `worktree add` 120s, demais 15s.
- Exceções, filhas de `GitException` com mensagem em pt-BR:
  - `GitNaoEncontradoException`: git fora do `PATH` → `502`;
  - `GitTempoEsgotadoException`: cita o comando → `502`;
  - `GitRemotoInacessivelException`: fetch com saída ≠ 0 → `502`, com o stderr;
  - `GitComandoRecusadoException`: demais saídas ≠ 0 → `400`, com o stderr.

  `GitExceptionHandler` traduz as que escapam de um endpoint.
- Concorrência: `TravasDosClones` (singleton) tem um `SemaphoreSlim` por caminho de clone normalizado (`Path.GetFullPath`, comparação sem diferenciar maiúsculas). `AdicionarWorktreeAsync`, `RemoverWorktreeAsync` e `LimparWorktreesOrfaosAsync` passam pela trava, porque o git usa locks em `.git/worktrees` e no índice.

Alternativa considerada: colocar o git dentro de Tarefas. Foi descartada pelo mesmo motivo do GitHub: transporte separado da regra de negócio, e uma integração por módulo.

### Configuração local na empresa
Colunas novas em `empresa`:
- `clone_front varchar(260) null` e `clone_back varchar(260) null`;
- `pasta_worktrees varchar(260) null`;
- `arquivos_front text[] not null default '{}'` e `arquivos_back text[] not null default '{}'`.

`Empresa.Clone(parte)` e `Empresa.Arquivos(parte)` seguem o padrão de `Repositorio(parte)`.

`AlterarEmpresaRequest` ganha `CloneFront`, `CloneBack`, `PastaWorktrees` (`string?`, em que `null` mantém e `""` remove, como os repositórios) e `ArquivosFront`/`ArquivosBack` (`IReadOnlyList<string>?`, em que `null` mantém e lista vazia limpa).

Validação no `EmpresaService`, com `DomainException` em pt-BR:
- Caminhos: `Path.IsPathFullyQualified`, até 260 caracteres. `pastaWorktrees` não precisa existir, porque é criada na primeira vez.
- Clone: `IGitLocal.ObterOrigemAsync` precisa devolver uma origem, e ela precisa corresponder ao repositório do GitHub da parte. A comparação extrai o `dono/repo` de URLs `https://github.com/dono/repo(.git)` ou `git@github.com:dono/repo(.git)`, sem diferenciar maiúsculas. Sem repositório do GitHub cadastrado na parte, o clone é recusado: *"Cadastre o repositório de front antes do clone local."*. Isso evita criar worktrees do repositório errado, por exemplo com os clones de front e back trocados.
- Arquivos: caminhos relativos (sem raiz, sem `..`), com `/` ou `\`, até 20 por parte e 260 caracteres cada, sem repetição.

Alternativa considerada: uma raiz global de worktrees. O usuário escolheu raiz por empresa, então o caminho é `<pastaWorktrees>\<pasta-da-branch>`, sem o nível da empresa.

### Entidades `Worktree` e `WorktreeParte`
```
tarefas.worktree                         tarefas.worktree_parte
  id, identifier, create_in, ...           id, identifier, create_in, ...
  tarefa_branch_id  (unique, fk cascade)   worktree_id      (fk cascade)
  pasta             varchar(260)           parte            varchar(10)   -- front | back
  removido_em       timestamp null         caminho          varchar(260)
                                           situacao         varchar(20)   -- criado | falhou | removido
                                           ultimo_erro      varchar(1000) null
                                           criado_em        timestamp null
                                           unique (worktree_id, parte)
```
- `Worktree` pertence à `TarefaBranch` (0..1) e é carregado pelo `TarefaCiclo.CarregarAsync` junto com as branches. É criado só para branches `implementacao`.
- Fica em tabela própria, e não em colunas de `tarefa_branch`, porque a `TarefaBranch` também guarda bases, porque são dados por parte e porque a próxima change acrescenta o slot de portas aqui (`slot smallint null` com índice único parcial `where removido_em is null`).
- Situação geral, calculada (`Worktree.Situacao`): `removido` se `removido_em` estiver preenchido; senão `falhou` se alguma parte falhou; senão `criado`.
- Ativo = `removido_em is null`.
- Remover e recriar reaproveita a mesma linha: `removido_em` volta a `null` e as partes voltam a ser tentadas.

### Pasta da tarefa
- `pasta = Path.Combine(empresa.PastaWorktrees, PastaDaBranch(nome))`.
- `PastaDaBranch` troca `/` por `-` (os demais caracteres já são válidos pelas regras de nome do Git, exceto os reservados do Windows `<>:"|?*`, que também viram `-`) e remove `.` e espaço do fim.
- Partes em `<pasta>\front` e `<pasta>\back`.
- A pasta é calculada e gravada na criação. Mudar `pastaWorktrees` depois não move worktrees existentes.

### Fluxo de criação (`WorktreesLocais`, irmã de `BranchesNoGitHub`)
```
 TarefaService (Iniciar / RetomarComBranch / AdicionarBranch implementacao)
   1. regras de domínio + GitHub + SaveChanges            <- como hoje
   2. WorktreesLocais.CriarAsync(empresa, branch)         <- nunca lança exceção do git
        a. empresa sem pasta ou sem clone de alguma parte da branch -> não cria (worktree = null)
        b. cria/reativa Worktree e as partes pendentes (situacao != criado)
        c. para cada parte, independentemente:
             - fetch da branch no clone
             - worktree list: caminho já registrado com a branch -> ok
                              caminho registrado com outra branch / pasta existe e não é worktree -> falhou
                              registro órfão (pasta sumiu) -> worktree prune e segue
             - worktree add
             - copiar arquivos (ver abaixo)
             - situacao = criado, criado_em = agora, ultimo_erro = null
           GitException / IOException -> situacao = falhou, ultimo_erro = mensagem (log Warning)
   3. SaveChanges                                         <- grava o resultado do passo 2
```
- O passo 2 não lança exceção do git. A resposta de Iniciar, Retomar e AdicionarBranch continua `200` com o `BranchResponse.worktree` mostrando a falha. A branch no GitHub e o registro na tarefa ficam gravados. Uma falha inesperada (bug) no passo 2 também é capturada, registrada em log com `Error` e gravada como `falhou`, para não transformar em `500` uma operação que já gravou o essencial.
- `POST .../worktree` chama o mesmo `CriarAsync`. Com a configuração da empresa incompleta, responde `400`: *"Configure a pasta dos worktrees e os clones locais de front e back da empresa X na página Empresas."*. Branch `base` → `400`. Branch inexistente → `404`. Worktree já todo criado → `200` sem chamar o git, exceto o fetch para atualizar `origin/<b>`.
- Retomada com branch nova (retrabalho) gera uma branch de implementação nova, e portanto worktrees novos numa pasta nova. Os worktrees das branches anteriores não são tocados.

Alternativa considerada: criar os worktrees antes do `SaveChanges`, dentro da mesma falha do GitHub. Foi descartada porque o worktree depende só do PC (clone, disco, credencial) e não deve impedir o registro da tarefa, e porque a nova tentativa já é possível pelo endpoint próprio.

Alternativa considerada: criar em segundo plano (fila + `BackgroundService`). Foi adiada. `fetch` + `worktree add` levam alguns segundos e cabem na requisição. O segundo plano entra com o `npm ci`/`dotnet restore`, que demoram minutos.

### Arquivos copiados
Para cada arquivo configurado na parte:
1. `origem = <clone>\<arquivo>`. Se não existir, pula e registra em log com `Information`. Não é falha, porque um arquivo opcional pode não existir em todo clone.
2. `IGitLocal.EhIgnoradoAsync(clone, arquivo)` precisa ser `true`. Senão a parte fica `falhou` com *"O arquivo X não é ignorado pelo git no clone do front; ele não foi copiado para não aparecer como alteração no worktree."*.
3. Se `<worktree>\<arquivo>` já existir, não sobrescreve. Isso preserva ajustes feitos na tarefa e mantém a recriação idempotente.
4. Cria as pastas intermediárias e copia.

Como o arquivo é ignorado, ele não conta como alteração na conferência de pendências nem impede o `worktree remove`.

### Remoção
`DELETE .../worktree`:
1. Branch inexistente ou sem worktree ativo → `404`.
2. Para cada parte `criado` cuja pasta existe: `ObterPendenciasAsync`. Se alguma parte tiver `Alteracoes > 0` ou `CommitsNaoEnviados > 0`, devolve `409` com Problem Details em pt-BR, listando por parte, por exemplo *"front: 3 alterações não commitadas; back: 1 commit não enviado"*. Nesse caso nenhuma parte é removida.
3. Remove cada parte: pasta inexistente → `worktree prune`; existente → `worktree remove`. A parte fica `removido`. Parte `falhou` só passa por `prune` e fica `removido`.
4. Apaga a `<pasta>` se ficou vazia. `removido_em = agora`.

Se o `worktree remove` falhar numa parte (arquivo aberto pelo Rider, `vite` rodando, `obj` preso pelo build server), a exceção vira `400` com o stderr e uma dica: *"Feche a IDE e pare os servidores da tarefa e tente de novo."*. As partes já removidas continuam gravadas como `removido`, porque cada parte é salva assim que termina.

O resultado "pendências" é um resultado do serviço (`409`), e não uma exceção, conforme a convenção de conflito do CLAUDE.md. Para isso, `TarefaResultado` ganha `WorktreeComPendencias(string detalhe)`.

`Tarefa.RemoverBranch` passa a lançar `DomainException` quando a branch tem worktree ativo: *"Remova o worktree da branch antes de excluí-la."*.

### Contrato
```
BranchResponse { ..., Worktree: WorktreeResponse? }
WorktreeResponse(
    string Pasta,
    SituacaoWorktree Situacao,                // criado | falhou | removido
    IReadOnlyList<WorktreeParteResponse> Partes,
    string ComandoClaude)                     // cd "<pasta>"; claude
WorktreeParteResponse(ParteBranch Parte, string Caminho, SituacaoWorktree Situacao, string? Erro, DateTime? CriadoEm)
```
- `ComandoClaude` é montado na API, em sintaxe PowerShell com aspas simples e `'` duplicado. A sessão abre na pasta da tarefa, que contém `front` e `back`, então não precisa de `--add-dir`. Os `CLAUDE.md` de cada repositório são carregados quando o Claude trabalha nos arquivos de cada subpasta.
- Novos endpoints em `TarefaEndpoints`, com `WithName`, `WithSummary`, `WithDescription` e metadados `Produces`/`ProducesProblem`: `CriarWorktree` e `RemoverWorktree`.
- `SituacaoWorktree` serializado em snake_case, com `SnakeCaseEnumJsonConverter` no tipo.

### Migration
Uma migration no `TarefasDbContext`, `20261112_WorktreesDasBranches`: colunas novas em `empresa` e tabelas `worktree` e `worktree_parte` com as FKs e índices acima, tudo em snake_case e com `timestamp without time zone`. Não há migração de dados.

## Risks / Trade-offs

- [Branch já em checkout no clone principal] → O git recusa o `worktree add`. A parte fica `falhou` com o stderr, e a mensagem sugere trocar de branch no clone e usar "Recriar worktree".
- [PC sem acesso ao remoto ou credencial expirada] → O fetch falha rápido (`GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`) e a parte fica `falhou`. A branch no GitHub já existe, então a nova tentativa resolve.
- [Clone desatualizado] → Irrelevante para a branch da tarefa, que vem do fetch explícito de `origin/<b>`.
- [Caminho longo no Windows] → `node_modules` dentro de `<pasta>\front` pode passar de 260 caracteres. A documentação recomenda `core.longpaths` e uma `pastaWorktrees` curta. O Actum não altera a configuração global do git.
- [Worktree apagado à mão] → O próximo criar ou remover faz `worktree prune` e trata a parte como ausente.
- [Duas branches com a mesma pasta (`feature/x` e `feature-x`)] → A segunda encontra a pasta registrada com outra branch e fica `falhou` com mensagem clara. Raro, e aceito.
- [Requisição de início mais lenta] → Alguns segundos a mais por causa do fetch e do checkout. Aceito nesta entrega; o front mostra o carregamento que já existe.
- [Remoção com IDE aberta] → Falha do `worktree remove` vira `400` com a dica de fechar a IDE e pode deixar a pasta pela metade. A nova tentativa conclui.
- [Banco de dev compartilhado entre tarefas em paralelo] → Fora do escopo. Fica registrado para uma change futura (banco por slot).

## Migration Plan

1. `git config --global core.longpaths true` no PC (recomendado).
2. Aplicar a migration.
3. Preencher, em cada empresa, `pastaWorktrees`, `cloneFront`, `cloneBack` e os arquivos (pela página Empresas, depois da change do front, ou por `PATCH /empresas/{identifier}`).
4. Publicar a API (`Actum-Iniciar.ps1 -Rebuild`). Nada no contrato é breaking, então o front pode vir depois.

Rollback: reverter a migration remove as colunas e as tabelas. Os worktrees já criados continuam no disco e podem ser removidos com `git worktree remove` em cada clone.
