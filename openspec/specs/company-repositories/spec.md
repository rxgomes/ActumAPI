# company-repositories Specification

## Purpose
Associa a cada empresa (workspace do Asana) um repositório de front e um de back no GitHub, usados para criar e conferir as branches das tarefas dessa empresa.

## Requirements

### Requirement: Repositórios da empresa
Cada empresa SHALL ter um repositório de front e um de back opcionais, no formato `dono/repo`, devolvidos em `GET /empresas` como `repositorioFront` e `repositorioBack` (`null` quando não definidos). Empresas criadas pela sincronização MUST começar sem repositórios.

#### Scenario: Empresa nova
- **WHEN** a sincronização cria a empresa do workspace aremas.com.br
- **THEN** `GET /empresas` MUST devolvê-la com `repositorioFront = null` e `repositorioBack = null`

### Requirement: Ajuste dos repositórios
`PATCH /empresas/{identifier}` SHALL aceitar `repositorioFront` e `repositorioBack`. Um valor informado MUST ser gravado sem espaços nas pontas; texto vazio MUST remover o repositório; campo não enviado MUST manter o valor atual. Um valor fora do formato `dono/repo` MUST ser recusado com `400` e o campo em `errors`.

#### Scenario: Definir os repositórios
- **WHEN** um cliente envia `repositorioFront = "ixact/erp-web"` e `repositorioBack = "ixact/erp-api"`
- **THEN** `GET /empresas` MUST devolver a empresa com esses dois repositórios

#### Scenario: Remover um repositório
- **WHEN** um cliente envia `repositorioBack = ""` para uma empresa que tinha `ixact/erp-api`
- **THEN** a empresa MUST ficar com `repositorioBack = null` e o repositório de front MUST permanecer

#### Scenario: Formato inválido
- **WHEN** um cliente envia `repositorioFront = "https://github.com/ixact/erp-web"`
- **THEN** a resposta MUST ter status `400` com `RepositorioFront` em `errors` e mensagem em pt-BR

#### Scenario: Só a cor
- **WHEN** um cliente envia apenas uma nova cor
- **THEN** os repositórios da empresa MUST permanecer iguais

### Requirement: Configuração local da empresa
Cada empresa SHALL ter, opcionalmente, os campos abaixo, devolvidos em `GET /empresas`:
- `cloneFront` e `cloneBack`: caminhos absolutos dos clones locais;
- `pastaWorktrees`: pasta raiz dos worktrees das tarefas da empresa;
- `arquivosFront` e `arquivosBack`: listas de arquivos fora do git a copiar do clone para o worktree, com caminhos relativos ao repositório.

Empresas criadas pela sincronização MUST começar com os caminhos `null` e as listas vazias.

#### Scenario: Empresa nova
- **WHEN** a sincronização cria a empresa do workspace aremas.com.br
- **THEN** `GET /empresas` MUST devolvê-la com `cloneFront`, `cloneBack` e `pastaWorktrees` `null` e `arquivosFront` e `arquivosBack` vazios

### Requirement: Ajuste da configuração local
`PATCH /empresas/{identifier}` SHALL aceitar `cloneFront`, `cloneBack`, `pastaWorktrees`, `arquivosFront` e `arquivosBack`.
- Caminho informado MUST ser gravado sem espaços nas pontas; texto vazio MUST remover; campo não enviado MUST manter o valor.
- Lista informada MUST substituir a anterior; lista vazia MUST limpar; lista não enviada MUST manter.

#### Scenario: Definir a configuração local
- **WHEN** um cliente envia `cloneFront = "E:\\src\\aureum\\erp-web"`, cujo `origin` é `https://github.com/ixact/erp-web.git`, para uma empresa com `repositorioFront = "ixact/erp-web"`, `pastaWorktrees = "E:\\wt\\aureum"` e `arquivosFront = [".env.development"]`
- **THEN** `GET /empresas` MUST devolver a empresa com esses valores

#### Scenario: Remover o clone
- **WHEN** um cliente envia `cloneBack = ""`
- **THEN** a empresa MUST ficar com `cloneBack = null` e os demais campos MUST permanecer

### Requirement: Validação da configuração local
Caminhos SHALL ser absolutos, com até 260 caracteres. Um clone MUST ser um repositório git cujo `origin` aponte para o repositório do GitHub da mesma parte na empresa. Arquivos MUST ser caminhos relativos sem `..`, até 20 por parte, sem repetição. Valores inválidos MUST ser recusados com `400` e mensagem em pt-BR, sem gravar nenhum campo.

#### Scenario: Clone de outro repositório
- **WHEN** um cliente envia como `cloneBack` um clone cujo `origin` é `ixact/erp-web`, e o `repositorioBack` da empresa é `ixact/erp-api`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR citando os dois repositórios

#### Scenario: Clone sem repositório do GitHub
- **WHEN** um cliente envia `cloneFront` para uma empresa sem `repositorioFront`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR pedindo para cadastrar o repositório de front antes

#### Scenario: Pasta que não é repositório
- **WHEN** um cliente envia como `cloneFront` uma pasta que não é um repositório git
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

#### Scenario: Caminho relativo
- **WHEN** um cliente envia `pastaWorktrees = "wt\\aureum"`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR

#### Scenario: Arquivo fora do repositório
- **WHEN** um cliente envia `arquivosBack = ["..\\segredos.json"]`
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR
