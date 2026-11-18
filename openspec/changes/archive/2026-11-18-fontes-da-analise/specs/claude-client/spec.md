## ADDED Requirements

### Requirement: Arquivos enviados à IA
O analisador SHALL enviar cada arquivo da demanda identificado pelo nome: PDF como documento nativo, PNG/JPEG/GIF/WEBP como imagem nativa, e arquivos de texto, `.docx` e `.xlsx` como texto extraído. Os arquivos MUST vir antes das instruções e do contexto em texto, na ordem recebida.

#### Scenario: PDF e imagem
- **WHEN** a demanda tem `especificacao.pdf` e `tela.png`
- **THEN** a mensagem à Claude API MUST ter um bloco de documento PDF e um de imagem, cada um precedido do nome do arquivo, seguidos do contexto da tarefa

#### Scenario: Word
- **WHEN** a demanda tem `regras.docx`
- **THEN** o texto dos parágrafos e das tabelas MUST ser enviado como texto do arquivo `regras.docx`, na ordem do documento

#### Scenario: Excel
- **WHEN** a demanda tem `precos.xlsx` com duas planilhas
- **THEN** cada planilha MUST ser enviada como texto com o nome da planilha e as linhas com os valores das células (textos e resultados de fórmula, sem formatação) separados por tabulação

#### Scenario: Texto
- **WHEN** a demanda tem `consulta.sql`
- **THEN** o conteúdo MUST ser enviado como texto, lido em UTF-8

### Requirement: Legibilidade dos arquivos
O módulo Claude SHALL informar, a partir do nome, do tamanho e da hospedagem, se um arquivo pode ser lido pela análise e, se não, o motivo em pt-BR. Os limites de tamanho MUST ser configuráveis (seção `Claude:Arquivos`).

#### Scenario: Externo
- **WHEN** o arquivo está hospedado fora do Asana (Google Drive, Dropbox...)
- **THEN** MUST ser não legível com o motivo "Arquivos externos não podem ser lidos pela análise."

#### Scenario: Tipo não suportado
- **WHEN** o arquivo é `video.mp4`, `apresentacao.pptx` ou `antigo.doc`
- **THEN** MUST ser não legível com o motivo "Tipo de arquivo não suportado pela análise."

#### Scenario: Grande demais
- **WHEN** o arquivo passa do limite do seu tipo (padrão: 20 MB para PDF e documentos, 5 MB para imagens)
- **THEN** MUST ser não legível com o motivo informando o limite, por exemplo "Arquivo maior que 5 MB."

#### Scenario: Tamanho desconhecido
- **WHEN** o Asana não informa o tamanho de um arquivo hospedado nele
- **THEN** o arquivo MUST ser considerado legível, e o limite MUST ser verificado depois do download

### Requirement: Arquivo rejeitado
Quando a Anthropic recusar um arquivo (PDF protegido, com páginas demais ou corrompido, imagem inválida) ou o texto extraído não puder ser lido, o analisador SHALL falhar com mensagem em pt-BR que diga o nome do arquivo, quando souber, e a resposta da API MUST ter status `400`.

#### Scenario: PDF protegido por senha
- **WHEN** a Anthropic responde `400` ao documento `contrato.pdf`
- **THEN** a resposta da API MUST ter status `400` com mensagem dizendo que um dos anexos não pôde ser lido pela IA, sem gravar versão

#### Scenario: Word corrompido
- **WHEN** `regras.docx` não pode ser aberto para extrair o texto
- **THEN** a resposta MUST ter status `400` com a mensagem "Não foi possível ler o arquivo regras.docx.", sem chamar a Anthropic

#### Scenario: Texto extraído longo demais
- **WHEN** o texto extraído de `base.xlsx` passa do limite de caracteres configurado
- **THEN** a resposta MUST ter status `400` com mensagem dizendo que o texto de `base.xlsx` passa do limite, sem cortar o conteúdo e sem chamar a Anthropic

## MODIFIED Requirements

### Requirement: Estruturação com saída estruturada
O contrato `IAnalisadorDeDemanda` SHALL receber o contexto da demanda (empresa, título e projetos; e, conforme as fontes escolhidas, descrição, comentários com autor e data, retrabalhos com número, data e autor da reabertura, prompt do usuário e arquivos com nome, tipo e conteúdo) e devolver, em pt-BR, `objetivo`, `checklist`, `testes`, `duvidas` e `regras`. A chamada MUST usar saída estruturada com JSON Schema, de modo que a resposta seja sempre um JSON nesse formato, e MUST devolver também o modelo que respondeu e os tokens de entrada e de saída.

#### Scenario: Demanda estruturada
- **WHEN** a Anthropic conclui a resposta (`stop_reason` = `end_turn`)
- **THEN** o analisador MUST devolver o objetivo, o checklist, os testes, as dúvidas e as regras do JSON, sem itens vazios, com o modelo e os tokens consumidos; item sem grupo MUST ir para o grupo `Geral` e `vago`, `origem` ou `trecho` vazios MUST virar ausentes

#### Scenario: Tarefa reaberta
- **WHEN** o contexto tem retrabalhos
- **THEN** o pedido à IA MUST destacar o retrabalho mais recente e os comentários a partir dele, para que o checklist priorize o que foi pedido na reabertura

#### Scenario: Modo prompt
- **WHEN** a demanda traz o prompt do usuário
- **THEN** o pedido à IA MUST tratar o prompt como a orientação principal da análise, os arquivos citados nele como o material indicado pelo usuário (modelos, instruções, referências) e MUST NOT incluir a descrição

#### Scenario: Fontes ausentes
- **WHEN** a demanda não traz descrição ou comentários
- **THEN** o pedido à IA MUST omitir esses blocos, e não enviá-los vazios

### Requirement: Checklist agrupado com trechos vagos
Cada item do `checklist` SHALL ter `grupo` (a área ou camada, como `Front`, `Back: cálculo` ou `Back: integração TecnoSpeed`), `texto` (a ação), `vago` (o que está vago no pedido daquela ação, ausente quando está claro), `origem` (de onde veio, no formato das dúvidas) e `trecho` (cópia literal do trecho que pediu a ação). Itens do mesmo grupo MUST vir juntos, na ordem de execução. Os `testes` MUST trazer como validar a entrega, com os dados de referência citados, sem repeti-los no checklist.

#### Scenario: Demanda com áreas e trecho vago
- **WHEN** a demanda pede ajustes no front e no back e um dos pedidos diz apenas "ver onde mais mexer"
- **THEN** o checklist MUST separar os itens nos grupos das áreas e o item desse pedido MUST trazer em `vago` o que falta definir

#### Scenario: Demanda sem áreas distintas
- **WHEN** a IA não indica grupo para um item
- **THEN** o item MUST ir para o grupo `Geral`

#### Scenario: Item com origem e trecho
- **WHEN** um item do checklist vem de uma frase do comentário de Maria em `04/11/2026 14:30`
- **THEN** o item MUST trazer origem `comentário de Maria em 04/11/2026 14:30` e o trecho com a frase copiada sem alteração
