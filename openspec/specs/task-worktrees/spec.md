# task-worktrees Specification

## Purpose
Define os worktrees locais das branches de implementação das tarefas: criação no clone de cada parte depois da criação da branch no GitHub, nova tentativa idempotente, cópia dos arquivos fora do git, remoção com conferência de pendências e exposição no detalhe da tarefa.

## Requirements

### Requirement: Criação dos worktrees
Depois de criar uma branch de implementação no GitHub e gravar a tarefa (no início, na retomada com branch nova e no registro de branch de implementação), a API SHALL criar, no clone de cada parte da branch, um worktree em `<pastaWorktrees>\<pasta-da-branch>\<parte>`, com `/` e caracteres inválidos no Windows trocados por `-` no nome da pasta. A criação MUST acontecer só quando a empresa tem `pastaWorktrees` e o clone de todas as partes da branch.

#### Scenario: Início com as duas partes
- **WHEN** uma tarefa da empresa Aureum, com `pastaWorktrees = "E:\\wt\\aureum"` e os dois clones configurados, é iniciada com a implementação `261112-ipi/devolvido` e as partes `front` e `back`
- **THEN** a resposta MUST ter status `200`, os worktrees MUST existir em `E:\wt\aureum\261112-ipi-devolvido\front` e `...\back` na branch `261112-ipi/devolvido`, e a branch MUST ter `worktree.situacao = criado`, com as duas partes `criado`

#### Scenario: Só a parte back
- **WHEN** a tarefa é iniciada só com a parte `back`
- **THEN** só o worktree `...\back` MUST ser criado, e `worktree.partes` MUST ter só a parte `back`

#### Scenario: Empresa sem configuração local
- **WHEN** a tarefa é iniciada numa empresa sem `pastaWorktrees` ou sem o clone de uma das partes
- **THEN** a tarefa MUST ser iniciada como antes, sem chamar o git, e a branch MUST ter `worktree = null`

#### Scenario: Retomada com branch nova
- **WHEN** uma tarefa em retrabalho é retomada com a implementação `261120-ipi-ajuste`
- **THEN** um par de worktrees novo MUST ser criado na pasta `261120-ipi-ajuste`, e os worktrees das branches anteriores MUST permanecer como estavam

#### Scenario: Branch base
- **WHEN** uma branch `base` é registrada na tarefa
- **THEN** nenhum worktree MUST ser criado para ela

### Requirement: Falha na criação dos worktrees
Cada parte SHALL ser tentada independentemente. Uma falha na criação MUST NOT alterar o status da resposta da operação, MUST NOT desfazer a branch no GitHub nem o registro na tarefa, e MUST ficar registrada na parte com a situação `falhou` e a mensagem do erro.

#### Scenario: Falha numa parte
- **WHEN** o worktree do front é criado e o do back falha porque a branch está em checkout no clone principal
- **THEN** a resposta MUST ter status `200`, a parte `front` MUST ficar `criado`, a parte `back` MUST ficar `falhou` com o erro do git em `erro`, e `worktree.situacao` MUST ser `falhou`

#### Scenario: Remoto inacessível
- **WHEN** o PC não alcança o remoto durante o início da tarefa
- **THEN** a tarefa MUST ser iniciada, a branch MUST continuar criada no GitHub, e as partes MUST ficar `falhou` com a mensagem do git

### Requirement: Nova tentativa
`POST /tarefas/{identifier}/branches/{branchIdentifier}/worktree` SHALL criar os worktrees ainda não criados da branch de implementação, sem alterar os que já estão criados. Também SHALL recriar os de um worktree removido, na mesma pasta. A operação MUST ser idempotente.

#### Scenario: Recriar depois de falha
- **WHEN** a parte `back` está `falhou` e o motivo foi corrigido
- **THEN** a chamada MUST criar só o worktree do back e devolver a branch com as duas partes `criado`

#### Scenario: Pasta já registrada com a mesma branch
- **WHEN** o worktree da parte já existe no clone, no caminho esperado e na branch da tarefa, mas a parte estava `falhou` (por exemplo, por falha na cópia de arquivos)
- **THEN** a parte MUST ser considerada criada, sem recriar o worktree, e a cópia dos arquivos MUST ser refeita

#### Scenario: Pasta ocupada
- **WHEN** a pasta da parte existe e não é worktree da branch da tarefa
- **THEN** a parte MUST ficar `falhou` com mensagem em pt-BR citando a pasta, e nada na pasta MUST ser apagado

#### Scenario: Configuração incompleta
- **WHEN** a empresa não tem `pastaWorktrees` ou o clone de uma parte da branch
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR pedindo para configurar a empresa na página Empresas

#### Scenario: Branch base ou inexistente
- **WHEN** a branch informada é do tipo `base`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR; e quando a tarefa ou a branch não existe, MUST ter status `404`

### Requirement: Arquivos copiados
Ao criar o worktree de uma parte, a API SHALL copiar do clone principal para o worktree cada arquivo configurado na empresa para a parte, no mesmo caminho relativo.
- Arquivo inexistente no clone MUST ser ignorado.
- Arquivo não ignorado pelo git no clone MUST NOT ser copiado e MUST deixar a parte `falhou` com mensagem em pt-BR.
- Arquivo que já existe no worktree MUST NOT ser sobrescrito.

#### Scenario: Cópia dos arquivos do front
- **WHEN** a empresa tem `arquivosFront = [".env.development"]` e o clone do front tem esse arquivo ignorado pelo git
- **THEN** o worktree do front MUST ter uma cópia de `.env.development`

#### Scenario: Arquivo versionado
- **WHEN** um arquivo configurado não é ignorado pelo git no clone
- **THEN** ele MUST NOT ser copiado, e a parte MUST ficar `falhou` com mensagem citando o arquivo

#### Scenario: Arquivo alterado na tarefa
- **WHEN** a parte é recriada e o `.env.development` do worktree foi alterado
- **THEN** o arquivo do worktree MUST permanecer como estava

### Requirement: Remoção dos worktrees
`DELETE /tarefas/{identifier}/branches/{branchIdentifier}/worktree` SHALL remover os worktrees das partes, sem forçar. Se alguma parte tiver alterações não commitadas ou commits não enviados, a resposta MUST ter status `409`, com mensagem em pt-BR listando as pendências por parte, e nenhuma parte MUST ser removida. Uma parte cuja pasta não existe mais MUST ter o registro limpo no clone. Ao final, a pasta da tarefa MUST ser apagada se estiver vazia, e o worktree MUST ficar `removido`.

#### Scenario: Remoção limpa
- **WHEN** as duas partes não têm pendências
- **THEN** as pastas MUST ser removidas, o clone MUST NOT listar mais os worktrees, e a branch MUST ter `worktree.situacao = removido`

#### Scenario: Alterações não commitadas
- **WHEN** o worktree do back tem 2 arquivos alterados e o do front está limpo
- **THEN** a resposta MUST ter status `409` com mensagem citando "back" e as 2 alterações, e os dois worktrees MUST permanecer

#### Scenario: Commits não enviados
- **WHEN** o worktree do front tem 1 commit que não está no remoto
- **THEN** a resposta MUST ter status `409` com mensagem citando "front" e o commit não enviado

#### Scenario: Arquivo em uso
- **WHEN** o git não consegue remover a pasta porque um arquivo está em uso
- **THEN** a resposta MUST ter status `400` com o erro do git e a orientação de fechar a IDE e parar os servidores da tarefa, e as partes já removidas MUST continuar registradas como `removido`

#### Scenario: Sem worktree
- **WHEN** a branch não tem worktree ativo
- **THEN** a resposta MUST ter status `404`

### Requirement: Branch com worktree ativo
A API SHALL recusar a exclusão de uma branch da tarefa (`DELETE /tarefas/{identifier}/branches/{branchIdentifier}`) enquanto ela tiver worktree ativo.

#### Scenario: Excluir branch com worktree
- **WHEN** um cliente exclui uma branch não enviada que tem worktree `criado`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR pedindo para remover o worktree antes, e a branch MUST permanecer

### Requirement: Worktree no detalhe da tarefa
Cada branch em `BranchResponse` SHALL ter `worktree`, `null` quando a branch nunca teve worktree. Quando preenchido, `worktree` MUST trazer:
- a pasta da tarefa;
- a situação geral (`criado`, `falhou` ou `removido`);
- as partes, na ordem front, back, com caminho, situação, erro da última tentativa e data de criação;
- `comandoClaude`, o comando PowerShell que abre o Claude Code na pasta da tarefa.

#### Scenario: Comando do Claude
- **WHEN** a pasta da tarefa é `E:\wt\aureum\261112-ipi-devolvido`
- **THEN** `comandoClaude` MUST ser `cd 'E:\wt\aureum\261112-ipi-devolvido'; claude`

#### Scenario: Pasta com apóstrofo
- **WHEN** a pasta contém `'`
- **THEN** o `'` MUST ser duplicado dentro das aspas simples do comando
