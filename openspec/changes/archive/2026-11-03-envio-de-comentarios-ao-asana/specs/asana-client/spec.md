## ADDED Requirements

### Requirement: Participantes da tarefa
O cliente do Asana SHALL listar os seguidores de uma tarefa (`followers`), com o identificador (`gid`) e o nome de cada um.

#### Scenario: Tarefa com seguidores
- **WHEN** a tarefa tem como seguidores Ricardo e Maria Angelica
- **THEN** o cliente MUST devolver os dois, com `gid` e nome

#### Scenario: Tarefa inexistente no Asana
- **WHEN** o Asana responde `404` para a tarefa
- **THEN** o cliente MUST indicar que a tarefa não existe no Asana, sem lançar erro de indisponibilidade

### Requirement: Anexo de arquivo
O cliente do Asana SHALL anexar um arquivo a uma tarefa por `POST /attachments` (`multipart/form-data`, com `parent` igual ao `gid` da tarefa), com o nome original e o tipo do arquivo, e devolver o `gid` e o nome do anexo criado. O upload MUST ter tempo limite próprio, maior que o das demais requisições, e MUST NOT ser repetido automaticamente depois de o Asana ter recebido o arquivo.

#### Scenario: Print colado
- **WHEN** o cliente anexa `print-20261103-181500.png` (`image/png`) à tarefa
- **THEN** o Asana MUST receber o arquivo com esse nome e tipo, e o cliente MUST devolver o anexo criado

### Requirement: Comentário com menções
O cliente do Asana SHALL publicar um comentário por `POST /tasks/{gid}/stories` usando `html_text`, com o texto do usuário escapado e as quebras de linha preservadas, e cada menção como `<a data-asana-gid="..."/>`. O cliente MUST devolver o `gid`, a data e o texto simples do comentário criado.

#### Scenario: Comentário mencionando quem pediu
- **WHEN** o usuário publica "Pode verificar?" mencionando Maria Angelica
- **THEN** o `html_text` enviado MUST ter a menção a ela e o texto, e o Asana MUST notificá-la

#### Scenario: Texto com caracteres especiais
- **WHEN** o texto tem `<`, `>`, `&` ou aspas
- **THEN** esses caracteres MUST ser escapados, para o Asana publicar exatamente o que foi digitado

### Requirement: Recusa do Asana a uma escrita
Quando o Asana responder `400`, `413` ou `422` a um comentário ou anexo, o cliente SHALL falhar com erro de requisição recusada, com mensagem em pt-BR, e a resposta da API MUST ter status `400`, sem nova tentativa.

#### Scenario: Arquivo recusado
- **WHEN** o Asana responde `413` ao upload
- **THEN** a resposta da API MUST ter status `400` com a mensagem de que o Asana recusou o arquivo
