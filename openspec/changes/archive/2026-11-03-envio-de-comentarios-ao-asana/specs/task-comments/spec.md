## Purpose

Define o envio de comentários, anexos e prints do Actum para a tarefa do Asana: participantes que podem ser mencionados, publicação do comentário com menções e anexos, e registro local do comentário enviado.

## ADDED Requirements

### Requirement: Participantes para menção
A API SHALL expor `GET /tarefas/{identifier}/participantes`, que lê do Asana os seguidores da tarefa, exceto o próprio usuário do token, e devolve `gid`, `nome` e `sugerido`. É sugerido o participante cujo nome (sem diferenciar maiúsculas e acentos) é o do autor da reabertura mais recente ou, sem reabertura, o do autor do comentário mais recente; reaberturas e comentários do próprio usuário não contam.

#### Scenario: Tarefa reaberta
- **WHEN** a tarefa foi reaberta por Maria Angelica e ela segue a tarefa
- **THEN** a resposta MUST ter status `200` com os seguidores e Maria Angelica com `sugerido=true`

#### Scenario: Ninguém a sugerir
- **WHEN** a tarefa não tem reabertura nem comentário de outra pessoa
- **THEN** todos os participantes MUST vir com `sugerido=false`

#### Scenario: Tarefa inexistente
- **WHEN** não existe tarefa com o `identifier` no Actum
- **THEN** a resposta MUST ter status `404`, sem chamar o Asana

#### Scenario: Asana indisponível
- **WHEN** o Asana não responde
- **THEN** a resposta MUST ter status `502` com a mensagem em pt-BR

#### Scenario: Tarefa excluída no Asana
- **WHEN** a tarefa existe no Actum, mas o Asana responde que ela não existe
- **THEN** a resposta MUST ter status `400` com a mensagem de que a tarefa não existe mais no Asana, e o envio de comentário MUST falhar da mesma forma

### Requirement: Envio de comentário com anexos
A API SHALL expor `POST /tarefas/{identifier}/comentarios` (`multipart/form-data`) com `texto` (opcional, até 10.000 caracteres), `mencoes` (lista de `gid`, opcional) e `arquivos` (opcional). Pelo menos texto ou um arquivo MUST ser informado. A API MUST anexar primeiro os arquivos, na ordem, e depois publicar o comentário com as menções no início, o texto e, havendo anexos, a linha "Anexos: " com os nomes. A resposta MUST ter status `201` com o comentário criado e os nomes dos anexos.

#### Scenario: Comentário com print
- **WHEN** o usuário envia o texto "Segue o erro" mencionando Maria Angelica e um print `print-20261103-181500.png`
- **THEN** o print MUST ser anexado à tarefa, o comentário MUST ser publicado com a menção, o texto e "Anexos: print-20261103-181500.png", e a resposta MUST ter status `201`

#### Scenario: Só anexos
- **WHEN** o usuário envia apenas dois arquivos, sem texto
- **THEN** os dois MUST ser anexados e o comentário MUST conter a lista dos anexos

#### Scenario: Nada para enviar
- **WHEN** não há texto nem arquivo
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem chamar o Asana

#### Scenario: Menção a quem não participa
- **WHEN** `mencoes` tem um `gid` que não está entre os seguidores da tarefa
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem anexar nem publicar nada

#### Scenario: Tarefa arquivada
- **WHEN** a tarefa está arquivada
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem chamar o Asana

### Requirement: Limites dos arquivos
A API SHALL aceitar até 10 arquivos por envio, cada um com até 25 MB e no total até 50 MB, e MUST NOT aceitar arquivo vazio.

#### Scenario: Arquivo grande demais
- **WHEN** um dos arquivos tem mais de 25 MB
- **THEN** a resposta MUST ter status `400` com o nome do arquivo e o limite na mensagem, sem chamar o Asana

#### Scenario: Arquivos demais
- **WHEN** o envio tem 11 arquivos
- **THEN** a resposta MUST ter status `400` com mensagem em pt-BR, sem chamar o Asana

### Requirement: Comentário registrado no Actum
O comentário publicado SHALL ser gravado na tabela `comentario` da tarefa, com o `gid` da story criada, o autor igual ao usuário do token, o texto simples devolvido pelo Asana, a data local e `lido=true`. A sincronização MUST NOT duplicá-lo.

#### Scenario: Comentário aparece no detalhe
- **WHEN** o comentário é publicado
- **THEN** `GET /tarefas/{identifier}` MUST trazê-lo na lista de comentários, já lido

#### Scenario: Sincronização depois do envio
- **WHEN** a sincronização lê a story do comentário publicado
- **THEN** o comentário MUST continuar único

### Requirement: Falha depois dos anexos
Se algum anexo foi enviado e um passo seguinte falhar (outro anexo ou o comentário), a API SHALL responder com o status do erro (`400` ou `502`), a mensagem em pt-BR dizendo que parte já foi enviada e a extensão `anexosEnviados` com os nomes dos arquivos já anexados ao Asana.

#### Scenario: Comentário falha após o upload
- **WHEN** o print foi anexado e o Asana fica indisponível ao publicar o comentário
- **THEN** a resposta MUST ter status `502`, `anexosEnviados` com o nome do print e mensagem orientando a enviar de novo só o texto
