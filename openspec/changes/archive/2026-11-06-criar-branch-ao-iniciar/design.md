# Design

## Context

- A API não tem nenhuma integração com Git. `Tarefa.Iniciar` e `Tarefa.AdicionarBranch` só registram nomes (`TarefaBranch`), e o nome da branch é usado como tag no Asana (`ConclusaoService`, comparação `Ordinal`).
- `TarefaService.IniciarAsync` e `AdicionarBranchAsync` carregam a tarefa (`TarefaCiclo.CarregarAsync`), aplicam a regra de domínio em memória e chamam `SaveChangesAsync`.
- Integrações externas seguem o padrão do módulo Asana: um projeto próprio com um cliente tipado (`AddHttpClient`), opções lidas da configuração (`Asana:Token` em user-secrets), uma exceção própria e um `IExceptionHandler` que a traduz em `400`/`502`. `Actum.Modules.Tarefas` referencia o projeto do Asana e usa só os seus contratos públicos.
- `Empresa` fica no módulo Tarefas (schema do módulo), com `PATCH /empresas/{identifier}` em `EmpresaEndpoints`.

## Goals / Non-Goals

**Goals:**
- Criar ou conferir as branches no GitHub antes de gravar a tarefa, sem deixar a tarefa num estado intermediário quando o GitHub falha.
- Manter a conclusão compatível com as branches já registradas sem partes.

**Non-Goals:**
- Transação distribuída entre GitHub e banco. O que fica no GitHub depois de uma falha é aceito na nova tentativa, e não desfeito.
- Cache das referências do GitHub.

## Decisions

### Novo módulo `Actum.Modules.GitHub`
O projeto tem `IGitHubClient`/`GitHubClient` (lê `GitHub:Token` da configuração a cada chamada, como o `AsanaClient`), `GitHubException` e `GitHubExceptionHandler`, no mesmo formato do Asana. Tarefas passa a referenciar o projeto, como já faz com o Asana. Não há endpoints próprios, porque não há necessidade de `GET /github/conexao` agora.

Alternativa considerada: colocar o cliente dentro de Tarefas. Foi descartada porque quebra o padrão de uma integração por módulo e mistura transporte HTTP com regra de negócio.

Contrato:
- `Task<string?> ObterShaDaBranchAsync(string repositorio, string branch, CancellationToken)`: `GET /repos/{dono}/{repo}/git/ref/heads/{branch}`. O nome da branch vai com cada segmento escapado e as barras mantidas. Na resposta `404`, faz `GET /repos/{dono}/{repo}` para distinguir as causas: repositório inexistente ou sem acesso lança `GitHubException` (`400`); repositório acessível devolve `null` (branch inexistente).
- `Task<bool> CriarBranchAsync(string repositorio, string branch, string sha, CancellationToken)`: `POST /repos/{dono}/{repo}/git/refs` com `{ ref: "refs/heads/{branch}", sha }`. Devolve `true` quando cria (`201`) e `false` quando recebe `422` com "Reference already exists".
- Base `https://api.github.com/`, cabeçalhos `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`, `User-Agent: Actum`, e tempo limite igual ao do Asana. Como no Asana, nenhum cabeçalho é registrado em log.
- Mapeamento de falhas: token ausente ou `401` → `400`; `403`/`404` no repositório → `400` citando `dono/repo`; tempo esgotado, falha de rede ou `5xx` → `502`.

### Fluxo do início: validar, conferir, criar, gravar
```
 TarefaService.IniciarAsync
   1. carrega a tarefa e a empresa
   2. tarefa.Iniciar(base, impl, prazo, partes)      <- em memória; DomainException => 400, sem GitHub
   3. resolve o repositório de cada parte            <- sem repositório => 400, sem GitHub
   4. para cada parte: sha = ObterShaDaBranch(base)  <- null => 400, nada criado
   5. para cada parte: CriarBranch(impl, sha)        <- false (já existe) => segue
   6. SaveChangesAsync
```
Todas as bases são conferidas antes de qualquer criação, o que cobre o caso mais comum de erro (base digitada errada) sem deixar branches pela metade. Se o passo 5 falhar no segundo repositório, a exceção sai antes do passo 6, então nada é gravado e a branch do primeiro repositório continua no GitHub. A nova tentativa recebe `false` ("já existe") nesse repositório e segue.

Alternativa considerada: gravar primeiro e criar depois. Foi descartada porque deixaria a tarefa `em_andamento` com branches que não existem no GitHub.

`AdicionarBranchAsync` segue o mesmo fluxo:
- `base`: só o passo 4 (a branch informada precisa existir em cada repositório).
- `implementacao`: os passos 4 e 5, usando como base a `TarefaBranch` de tipo `Base` com o maior `CreateIn`. Sem base registrada, a tarefa lança `DomainException` (`400`).

A orquestração com o GitHub fica no `TarefaService`, e não na entidade, porque a entidade continua sem dependências externas. Para não repetir o código, uma classe interna `BranchesNoGitHub` concentra a resolução dos repositórios e as chamadas ao GitHub, usada nos dois fluxos.

### Partes como enum de flags no domínio e duas colunas no banco
- `ParteBranch { Front, Back }`, serializado em snake_case (`front`, `back`) com `SnakeCaseEnumJsonConverter`, como `TipoBranch`. Nos requests, `Partes` é `IReadOnlyList<ParteBranch>?` com `[Required]` e pelo menos um item. Repetições são ignoradas.
- Em `tarefa_branch`, duas colunas: `parte_front boolean not null default false` e `parte_back boolean not null default false`. Branches antigas ficam com as duas `false`, o que significa "sem partes".
- Alternativa considerada: uma coluna texto `partes` (`front,back`). Foi descartada porque duas booleanas são mais simples de consultar, não precisam de conversão e o conjunto de partes é fixo.

### Tag calculada na entidade
`TarefaBranch.Tag` (propriedade calculada, sem coluna) devolve `"(Front, Back) => {Nome}"`, `"(Front) => {Nome}"`, `"(Back) => {Nome}"` ou `Nome`. `ConclusaoService` passa a usar `Tag` na prévia, na comparação com as tags da tarefa no Asana e em `ObterOuCriarTagAsync`. `BranchResponse` ganha `Partes` (lista na ordem front, back) e `Tag`.

Ao comparar com as tags que já estão na tarefa, a conclusão continua usando `Ordinal` sobre o texto completo da tag.

### Validação do nome para o Git
A validação fica num método estático da entidade (`NomeDaBranch`, já existente), que ganha as regras de `git check-ref-format` listadas na spec. A regra vale para qualquer branch nova, com ou sem GitHub. As branches já gravadas não são revalidadas.

### Repositórios da empresa
Em `empresa`, duas colunas: `repositorio_front varchar(200) null` e `repositorio_back varchar(200) null`. `AlterarEmpresaRequest` ganha `RepositorioFront` e `RepositorioBack` (`string?`). A validação usa `[RegularExpression("^$|^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$")]`, aplicada depois de remover os espaços das pontas no serviço. `null` mantém o valor; `""` remove. `EmpresaResponse` ganha os dois campos.

### Bases recentes
`GET /tarefas/{identifier}/branches-base-recentes` em `TarefaEndpoints`. A consulta pega as `TarefaBranch` de tipo `Base` das tarefas com o mesmo `EmpresaId`, ordena por `CreateIn` decrescente, tira as repetições pelo nome mantendo a primeira ocorrência e devolve as 3 primeiras. O volume é pequeno (um usuário), então basta trazer os nomes ordenados e fazer a distinção em memória. Resposta: `IReadOnlyList<string>`. Tarefa inexistente → `404`.

### Migration
Uma migration no `TarefasDbContext`, `yyyyMMdd_RepositoriosEPartesDaBranch`, adiciona as quatro colunas acima. Não é preciso migrar dados.

## Risks / Trade-offs

- [Branch órfã no GitHub quando o início falha depois de criar num repositório e o usuário desiste] → Aceito. A branch fica vazia (aponta para a base) e pode ser apagada à mão. A nova tentativa a reaproveita.
- [Base com o mesmo nome em repositórios diferentes, mas que significa coisas diferentes] → Fora do controle da API. A base informada é a mesma para as duas partes, como pedido.
- [Token com permissão insuficiente (só leitura)] → `CriarBranchAsync` recebe `403`/`404` e vira `400` com mensagem citando o repositório e a permissão Contents: read and write.
- [Mudança do texto das tags] → Tags antigas no Asana continuam iguais. Só as branches novas, com partes, geram o formato novo.
- [Contrato breaking em `iniciar` e `branches`] → O front é alterado na change `criar-branch-ao-iniciar` do ActumUi, a ser publicada junto.

## Migration Plan

1. Configurar `GitHub:Token` com `dotnet user-secrets` na máquina da API (192.168.15.200).
2. Aplicar a migration.
3. Cadastrar os repositórios de cada empresa (pela página Empresas, depois da change do front, ou por `PATCH /empresas/{identifier}`).
4. Publicar a API e o front juntos, por causa do `partes` obrigatório.

Rollback: reverter a migration remove as colunas novas. As tags já enviadas no formato novo permanecem no Asana.
