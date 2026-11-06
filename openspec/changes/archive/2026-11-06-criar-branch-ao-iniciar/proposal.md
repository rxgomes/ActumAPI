# Proposal

## Why

Hoje, ao iniciar uma tarefa, o usuário cria a branch de implementação à mão no GitHub (ou no Git local) e depois digita os dois nomes no Actum, sem nenhuma verificação de que a base existe. Além disso, a tag enviada ao Asana não diz se a branch é do front, do back ou dos dois. A mudança faz a API criar a branch de implementação no GitHub a partir da base, nos repositórios das partes escolhidas (Front e/ou Back), e registrar essas partes na tag.

## What Changes

- Novo módulo `Actum.Modules.GitHub`, com um cliente da API REST do GitHub que consulta o último commit de uma branch e cria uma branch a partir de um commit. O token fica em `GitHub:Token` (user-secrets), no mesmo padrão do Asana e do Claude.
- Empresa ganha `repositorioFront` e `repositorioBack` (`dono/repo`), devolvidos em `GET /empresas` e alteráveis em `PATCH /empresas/{identifier}`.
- **BREAKING** `POST /tarefas/{identifier}/iniciar` passa a exigir `partes` (`front` e/ou `back`). Para cada parte, a API confere se a branch base existe no repositório e cria nele a branch de implementação a partir do último commit da base. Se o GitHub recusar, a tarefa não é iniciada. Uma branch de implementação que já exista no repositório é aceita, o que torna a nova tentativa segura.
- **BREAKING** `POST /tarefas/{identifier}/branches` passa a exigir `partes`. Uma branch `base` só tem a existência conferida em cada repositório. Uma branch `implementacao` é criada a partir da base mais recente da tarefa.
- Cada branch guarda as suas partes. `BranchResponse` ganha `partes` e `tag` (o texto que vai ao Asana).
- O nome da tag passa a ser `(Front, Back) => nome`, `(Front) => nome` ou `(Back) => nome`. Branches sem partes (registradas antes desta mudança) continuam com a tag igual ao nome. A prévia e a conclusão usam esse texto, inclusive para reconhecer uma tag já presente na tarefa.
- Novo `GET /tarefas/{identifier}/branches-base-recentes`: até 3 nomes distintos de branch base usados mais recentemente nas tarefas da mesma empresa, para o seletor do front.
- O nome de branch novo passa a ser validado com as regras de nome de referência do Git (sem espaço, `~`, `^`, `:`, `?`, `*`, `[`, `\`, `..`, `@{`, sem terminar em `/`, `.` ou `.lock`).

## Capabilities

### New Capabilities
- `github-client`: acesso à API REST do GitHub (configuração do token, consulta do último commit de uma branch, criação de branch, tradução de falhas em respostas da API).
- `company-repositories`: repositórios de front e de back do GitHub associados a cada empresa.

### Modified Capabilities
- `task-lifecycle`: o início e o registro de branches passam a exigir as partes e a criar ou conferir as branches no GitHub; a tag no Asana passa a incluir as partes; nova consulta das bases recentes da empresa.

## Impact

- **Módulos**: novo `Actum.Modules.GitHub`; `Actum.Modules.Tarefas` passa a depender dele, como já depende do Asana, por meio do contrato público `IGitHubClient`; `Actum.Api` registra o módulo.
- **Banco** (schema do módulo Tarefas): colunas novas em `empresa` (repositórios) e em `tarefa_branch` (partes). Uma migration.
- **Contrato**:
  - breaking: `partes` é obrigatório em `IniciarTarefaRequest` e `AdicionarBranchRequest`;
  - não breaking: campos novos em `EmpresaResponse`, `AlterarEmpresaRequest` e `BranchResponse`, endpoint novo de bases recentes;
  - as tags na prévia da conclusão mudam de texto.
- **Front (..\ActumUI)**: precisa regerar os tipos e alterar os diálogos Iniciar e Nova branch, a prévia da conclusão e a página Empresas (change `criar-branch-ao-iniciar` no ActumUi, feita depois desta).
- **Configuração**: `GitHub:Token` em user-secrets (fine-grained PAT com Contents: read and write nos repositórios das empresas).

## Fora do escopo

- Criar a branch no Git local, fazer checkout ou pull na máquina do usuário. A branch só precisa existir no remoto.
- Outros provedores (GitLab, Azure DevOps, Bitbucket).
- Mais de um par de repositórios por empresa (cadastro de projetos/produtos).
- Listar ou buscar as branches existentes no GitHub para o seletor da base.
- Excluir a branch no GitHub ao excluir a branch da tarefa, ou alterar as tags que já estão no Asana.
- Reescrever as tags de branches antigas, sem partes.
- Gerar a sugestão de nome `yyMMdd-titulo` (feita pelo front).
