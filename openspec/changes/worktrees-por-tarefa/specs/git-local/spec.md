# Spec Delta

## Purpose

Define como o Actum executa o git de linha de comando nos clones locais do usuário: busca de branch no remoto, criação, listagem e remoção de worktrees, conferência de pendências, tempo limite e tradução das falhas em respostas da API em pt-BR.

## ADDED Requirements

### Requirement: Execução do git
A API SHALL executar o git como processo do usuário, sem shell, passando cada argumento separadamente. Todo comando MUST ter tempo limite. No estouro, o processo e os filhos MUST ser encerrados. As variáveis `GIT_TERMINAL_PROMPT=0` e `GCM_INTERACTIVE=never` MUST ser definidas, e as variáveis `ASPNETCORE_*` e `Cors__*` do processo da API MUST NOT ser repassadas ao processo filho.

#### Scenario: Nome de branch com barra e caminho com espaço
- **WHEN** a API cria o worktree da branch `feature/ipi-devolvido` em `E:\wt\Minha Empresa\feature-ipi-devolvido\back`
- **THEN** o git MUST receber o nome e o caminho como argumentos únicos, sem interpretação de aspas

#### Scenario: Tempo esgotado
- **WHEN** um comando do git não termina dentro do tempo limite
- **THEN** o processo MUST ser encerrado e a falha MUST ser `GitTempoEsgotadoException`, com mensagem em pt-BR citando a operação

#### Scenario: Credencial que pediria interação
- **WHEN** o remoto exige uma credencial que não está disponível
- **THEN** o comando MUST falhar sem esperar interação do usuário

#### Scenario: Ambiente do Actum não vaza
- **WHEN** a API, rodando com `ASPNETCORE_URLS=http://localhost:5280`, executa um processo filho
- **THEN** o processo filho MUST NOT receber `ASPNETCORE_URLS`

### Requirement: Busca da branch no remoto
A API SHALL atualizar `origin/<branch>` no clone a partir do `origin`, mesmo em clones com uma única branch configurada.

#### Scenario: Branch recém-criada no GitHub
- **WHEN** a branch foi criada no GitHub e o clone ainda não a conhece
- **THEN** depois da busca, `refs/remotes/origin/<branch>` MUST existir no clone

#### Scenario: Remoto inacessível
- **WHEN** a busca falha (rede, credencial ou branch inexistente no remoto)
- **THEN** a falha MUST ser `GitRemotoInacessivelException` com o erro do git na mensagem

### Requirement: Worktrees do clone
A API SHALL listar os worktrees de um clone (caminho e branch) e criar um worktree de uma branch num caminho. Quando a branch local não existe, ela MUST ser criada a partir de `origin/<branch>` com upstream nele. Quando existe, MUST ser usada a branch local. Operações que alteram os worktrees de um mesmo clone MUST ser executadas uma por vez.

#### Scenario: Branch local inexistente
- **WHEN** a API cria o worktree de uma branch que só existe em `origin`
- **THEN** o worktree MUST ficar na branch local de mesmo nome, com upstream `origin/<branch>`, e `git push` sem argumentos no worktree MUST enviar para ela

#### Scenario: Branch em checkout em outro lugar
- **WHEN** a branch já está em checkout no clone principal
- **THEN** a falha MUST ser `GitComandoRecusadoException` com o erro do git na mensagem

#### Scenario: Listagem
- **WHEN** o clone tem um worktree em `E:\wt\aureum\x\front` na branch `x`
- **THEN** a listagem MUST devolver esse caminho com a branch `x`

### Requirement: Pendências do worktree
A API SHALL informar, para um worktree, a quantidade de alterações não commitadas (incluindo arquivos novos não ignorados) e a quantidade de commits locais não enviados. Os commits não enviados MUST ser comparados com o upstream da branch ou, sem upstream, com todas as branches de `origin`.

#### Scenario: Worktree limpo
- **WHEN** o worktree não tem alterações e o último commit já está no remoto
- **THEN** as pendências MUST ser zero alterações e zero commits não enviados

#### Scenario: Arquivo ignorado
- **WHEN** o worktree tem só um `.env.development` ignorado pelo git e uma pasta `node_modules`
- **THEN** as alterações MUST ser zero

#### Scenario: Commit não enviado
- **WHEN** há um commit local na branch que não está em `origin`
- **THEN** os commits não enviados MUST ser um

### Requirement: Remoção do worktree
A API SHALL remover um worktree sem forçar e limpar registros de worktrees cujas pastas não existem mais.

#### Scenario: Pasta apagada à mão
- **WHEN** a pasta de um worktree foi apagada fora do Actum
- **THEN** a limpeza MUST remover o registro do clone, e uma nova criação no mesmo caminho MUST ser possível

### Requirement: Falhas do git
Falhas do git que chegam a um endpoint SHALL virar Problem Details com a mensagem em pt-BR em `detail`: git não encontrado, tempo esgotado e remoto inacessível MUST virar `502`; comando recusado MUST virar `400`.

#### Scenario: Git fora do PATH
- **WHEN** o executável `git` não é encontrado
- **THEN** a resposta MUST ter status `502` com mensagem em pt-BR dizendo que o git não foi encontrado no PC
