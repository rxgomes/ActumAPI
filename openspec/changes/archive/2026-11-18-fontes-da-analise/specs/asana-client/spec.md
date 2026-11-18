## ADDED Requirements

### Requirement: Conteúdo do anexo
O cliente do Asana SHALL ler o conteúdo (bytes) de um anexo: pede um endereço de download novo (`GET /attachments/{gid}`) e baixa o arquivo desse endereço `https`, devolvendo o nome, a tarefa a que pertence e o conteúdo. O token MUST NOT ser enviado ao endereço de download, e o download MUST parar com erro ao passar do tamanho máximo pedido.

#### Scenario: Anexo do Asana
- **WHEN** o cliente lê o conteúdo de `especificacao.pdf`
- **THEN** MUST devolver o nome, o gid da tarefa e os bytes do arquivo, sem cabeçalho `Authorization` na requisição ao endereço de download

#### Scenario: Anexo inexistente
- **WHEN** o anexo não existe mais no Asana
- **THEN** o cliente MUST devolver ausente (`null`)

#### Scenario: Arquivo maior que o máximo
- **WHEN** o arquivo baixado passa do tamanho máximo informado
- **THEN** o download MUST ser interrompido e o cliente MUST falhar com erro de arquivo grande demais, com mensagem em pt-BR

#### Scenario: Falha no download
- **WHEN** o endereço de download responde erro ou a conexão falha
- **THEN** o cliente MUST falhar com o erro de indisponibilidade do Asana (`502`) e mensagem em pt-BR
