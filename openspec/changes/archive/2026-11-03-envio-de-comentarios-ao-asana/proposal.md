# Proposal

## Why

Hoje o Actum só lê o Asana (tarefas e comentários) e escreve tags e conclusão. Para responder a quem pediu, mandar um print do erro ou postar as dúvidas que a análise com IA levantou, o usuário ainda precisa abrir o Asana. A Fase 3 (docs/especificacao-fase1.md) leva para o Actum o envio de comentários, anexos e prints (Ctrl+V) para a tarefa do Asana, com menção a quem pediu para que a pessoa seja notificada.

## What Changes

- `IAsanaClient` ganha três operações:
  - listar os participantes (seguidores) de uma tarefa, com identificador e nome;
  - anexar um arquivo à tarefa (upload);
  - publicar um comentário em texto formatado, com menções.
- `GET /tarefas/{identifier}/participantes`: participantes da tarefa lidos do Asana na hora, com a sugestão de quem mencionar (o autor da reabertura mais recente ou, sem reabertura, do comentário mais recente de outra pessoa).
- `POST /tarefas/{identifier}/comentarios` (`multipart/form-data`): texto, menções e arquivos (até 10, até 25 MB cada, 50 MB no total). Anexa os arquivos à tarefa e publica o comentário com as menções e a lista dos anexos. Também aceita só arquivos.
- O comentário publicado é gravado na hora na tabela `comentario` (já lido), então aparece no detalhe sem esperar a sincronização e não é duplicado por ela.
- Se os anexos subirem e o comentário falhar, a resposta informa quais anexos já foram enviados, para não serem enviados de novo.

## Capabilities

### New Capabilities

- `task-comments`: envio de comentários, anexos e prints do Actum para a tarefa do Asana, com menções.

### Modified Capabilities

- `asana-client`: novas operações de leitura de participantes, upload de anexo e publicação de comentário, e tradução da recusa do Asana a um comentário ou anexo.

## Impact

- **Módulos afetados:**
  - `Actum.Modules.Asana`: novas operações em `IAsanaClient` e um erro novo para requisição recusada pelo Asana.
  - `Actum.Modules.Tarefas`: serviço e endpoints de participantes e comentários. A dependência do contrato `IAsanaClient` já existe.
- **Contrato da API:** dois endpoints novos, `GET /tarefas/{identifier}/participantes` e `POST /tarefas/{identifier}/comentarios`. Nenhum contrato existente muda (não breaking).
- **Impacto no front (`..\ActumUI`):** caixa de comentário no detalhe da tarefa, com:
  - texto;
  - arquivos por botão, arrastar e soltar, ou colar print com Ctrl+V;
  - menções sugeridas;
  - o botão "Enviar dúvidas ao Asana" na análise com IA, que preenche a caixa para revisão.

  Será uma change própria no ActumUI, depois desta.
- **Banco:** nenhuma tabela nova. O comentário enviado usa a tabela `comentario` existente.
- **Asana:** são escritas visíveis para outras pessoas (comentários notificam os seguidores e os mencionados). Os testes automáticos usam o Asana simulado; o teste real é feito numa tarefa escolhida pelo usuário, com autorização.

### Fora do escopo

- Editar ou excluir comentários já enviados.
- Responder a um comentário específico (o Asana não tem respostas encadeadas).
- Baixar ou exibir no Actum os anexos que já estão no Asana.
- Menção livre a qualquer pessoa do workspace (só os participantes da tarefa).
- Formatação rica no texto (negrito, listas); o texto vai como foi digitado, com as quebras de linha.
