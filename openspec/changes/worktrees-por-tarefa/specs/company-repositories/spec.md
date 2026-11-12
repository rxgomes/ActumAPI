# Spec Delta

## ADDED Requirements

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
