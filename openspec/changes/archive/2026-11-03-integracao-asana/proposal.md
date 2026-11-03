# Proposal

## Why

Todo o valor do Actum depende de ler as tarefas do Asana e, na conclusão, escrever tags e marcar a tarefa como concluída. Concentrar a comunicação com a API do Asana em um módulo próprio isola autenticação, paginação, limite de requisições e tradução de erros. Assim o módulo de tarefas trabalha com um contrato simples, testável sem chamar o Asana de verdade.

## What Changes

- Novo módulo `Actum.Modules.Asana`, sem banco de dados.
- Contrato público `IAsanaClient` com as operações de leitura de workspaces, tarefas e stories, e as de escrita de tags e conclusão.
- Autenticação por Personal Access Token lido de `Asana:Token` (dotnet user-secrets).
- Paginação automática, novas tentativas em `429` respeitando `Retry-After` e tradução de falhas para uma exceção própria do módulo.
- Endpoint `GET /asana/conexao` para verificar se o token está configurado e válido.

## Capabilities

### New Capabilities

- `asana-client`: acesso à API do Asana em nome do usuário (leitura de tarefas atribuídas, histórico e comentários; escrita de tags e conclusão).

### Modified Capabilities

Nenhuma.

## Impact

- **Módulos afetados:** novo `Actum.Modules.Asana`. O host passa a registrá-lo no `ModuleCatalog`.
- **Contrato da API:** novo endpoint `GET /asana/conexao` (não breaking). Impacto no front (`..\ActumUI`): pode mostrar um aviso quando o token não estiver configurado.
- **Dependências novas:** nenhuma além do `HttpClient`/`IHttpClientFactory` do ASP.NET.
- **Configuração:** o desenvolvedor precisa executar `dotnet user-secrets set "Asana:Token" "<token>"` no projeto `Actum.Api`.

### Fora do escopo

- Persistência de dados do Asana (responsabilidade do módulo `Tarefas`).
- Comentários, anexos e upload de imagens (Fase 3).
- Webhooks e sincronização em segundo plano.
- OAuth ou múltiplos usuários.
