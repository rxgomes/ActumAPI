# Proposal

## Why

Hoje o Actum cria a branch de implementação só no GitHub. Para trabalhar numa tarefa, o usuário precisa trocar de branch à mão no clone local do front e do back de cada empresa. Por isso só dá para trabalhar numa tarefa por vez, já que existe um único checkout por repositório. A mudança faz o Actum criar, para cada branch de implementação, um par de **git worktrees** (front e back) lado a lado numa pasta da tarefa. Assim cada tarefa pode ter a sua própria sessão do Claude Code no terminal, em paralelo, sem IDE aberta. Esta é a primeira entrega: criar, recriar e remover os worktrees e mostrar os caminhos. Portas por tarefa, acesso pelo note, preparo (`npm ci`/`dotnet restore`) e botões de IDE ficam para changes seguintes.

## What Changes

- Novo módulo `Actum.Modules.Git`, sem banco e sem endpoints, com um cliente do git de linha de comando para os clones locais. Ele busca uma branch no remoto, lista, cria e remove worktrees e informa as pendências de um worktree (alterações não commitadas e commits não enviados). As falhas viram respostas da API em pt-BR.
- Novo executor de processos em `Actum.Shared.Infrastructure`, com tempo limite, captura de stdout/stderr e ambiente limpo. Ele não deixa passar para os filhos as variáveis `ASPNETCORE_*` e `Cors__*` do próprio Actum.
- Empresa ganha os campos abaixo, devolvidos em `GET /empresas` e alteráveis em `PATCH /empresas/{identifier}`:
  - `cloneFront` e `cloneBack`: caminhos dos clones locais;
  - `pastaWorktrees`: pasta raiz dos worktrees da empresa;
  - `arquivosFront` e `arquivosBack`: arquivos fora do git a copiar do clone para o worktree.

  Um clone informado precisa ser um repositório git cujo `origin` corresponda ao repositório do GitHub da parte.
- Depois de criar uma branch de implementação no GitHub (início, retomada com branch nova e registro de branch de implementação), a API cria os worktrees das partes da branch em `<pastaWorktrees>\<nome-da-branch>\front` e `...\back`. Esse é um passo à parte: se falhar, a operação continua com sucesso e a falha fica registrada na parte.
- Novos endpoints:
  - `POST /tarefas/{identifier}/branches/{branchIdentifier}/worktree`: cria ou recria os worktrees de uma branch de implementação. É idempotente e completa só o que faltou.
  - `DELETE /tarefas/{identifier}/branches/{branchIdentifier}/worktree`: remove os worktrees. É recusado com `409` quando há alterações não commitadas ou commits não enviados em alguma parte, e nesse caso nada é removido.
- `BranchResponse` ganha `worktree`, com a pasta, a situação e o caminho de cada parte, o erro da última tentativa e o comando para abrir o Claude Code na pasta da tarefa. Fica `null` quando a branch não tem worktree.
- Uma branch com worktree ativo não pode ser excluída da tarefa.

## Capabilities

### New Capabilities
- `git-local`: execução do git nos clones locais (busca no remoto, worktrees, pendências, tempo limite e tradução das falhas).
- `task-worktrees`: worktrees das branches de implementação das tarefas (criação depois do GitHub, nova tentativa, remoção com conferência de pendências, arquivos copiados, exposição no detalhe da tarefa).

### Modified Capabilities
- `company-repositories`: a empresa passa a ter clones locais, pasta dos worktrees e arquivos a copiar por parte.

## Impact

- **Módulos**:
  - novo `Actum.Modules.Git`;
  - `Actum.Modules.Tarefas` passa a depender dele pelo contrato público `IGitLocal`, como já depende do GitHub e do Asana;
  - `Actum.Shared.Infrastructure` ganha o executor de processos;
  - `Actum.Api` registra o módulo.
- **Banco** (schema `tarefas`): colunas novas em `empresa` e tabelas novas `worktree` e `worktree_parte`. Uma migration.
- **Contrato**: nada é breaking.
  - Campos novos em `EmpresaResponse`, `AlterarEmpresaRequest` e `BranchResponse`.
  - Dois endpoints novos.
  - `DELETE /tarefas/{identifier}/branches/{branchIdentifier}` passa a recusar com `400` uma branch com worktree ativo.
- **Front (..\ActumUI)**:
  - regerar os tipos;
  - página Empresas com os campos novos;
  - em `BranchesDaTarefa`, mostrar caminhos e situação e incluir os botões "Copiar comando do Claude", "Recriar worktree" e "Remover worktree".

  Isso fica numa change `worktrees-por-tarefa` no ActumUi, feita depois desta.
- **Ambiente**: o git precisa estar no `PATH` do processo da API, que roda como o usuário logado e por isso usa as credenciais do Git Credential Manager. Recomenda-se `git config --global core.longpaths true` e pastas de worktrees com caminho curto.

## Fora do escopo

- Portas por tarefa (slots), proxy do Vite, `--host`, Firewall do Windows e o `CLAUDE.md` da pasta da tarefa com as portas (próxima change).
- `npm ci` e `dotnet restore` em segundo plano.
- Abrir Windows Terminal, WebStorm ou Rider a partir do Actum.
- Remover os worktrees automaticamente na conclusão ou no arquivamento. Nesta entrega a remoção é só pelo botão.
- Apagar a branch local no clone depois da remoção.
- Worktree de branch `base`.
- Banco de dados de desenvolvimento separado por tarefa.
- Alterações nos `vite.config` e `CLAUDE.md` dos repositórios das empresas.
