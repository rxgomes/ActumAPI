# Spec Delta

## MODIFIED Requirements

### Requirement: Dados do cartão
Cada tarefa do painel SHALL conter identificador, título, empresa (com nome e cor), projetos, status, prazo interno, prazo do Asana, indicador de prazo pendente, prioridade, quantidade de retrabalhos, quantidade de comentários não lidos e a data (`ultimoComentarioEm`, data e hora local) e o autor (`ultimoComentarioAutor`) do comentário mais recente da tarefa.

#### Scenario: Tarefa reaberta duas vezes com comentários novos
- **WHEN** uma tarefa tem dois retrabalhos e três comentários não lidos
- **THEN** o cartão MUST ter `quantidadeRetrabalhos = 2` e `comentariosNaoLidos = 3`

#### Scenario: Tarefa com comentários
- **WHEN** uma tarefa tem um comentário de Ana em `2026-11-03T09:00:00` e outro de Bruno em `2026-11-04T14:30:00`
- **THEN** o cartão MUST ter `ultimoComentarioEm = 2026-11-04T14:30:00` e `ultimoComentarioAutor = "Bruno"`, estejam os comentários lidos ou não

#### Scenario: Tarefa sem comentários
- **WHEN** uma tarefa não tem comentários
- **THEN** o cartão MUST ter `ultimoComentarioEm` e `ultimoComentarioAutor` nulos

#### Scenario: Último comentário com autor desconhecido
- **WHEN** o comentário mais recente de uma tarefa não tem autor
- **THEN** o cartão MUST ter `ultimoComentarioEm` com a data desse comentário e `ultimoComentarioAutor` nulo

#### Scenario: Comentário enviado pelo Actum
- **WHEN** o usuário envia um comentário pelo Actum e ele é o mais recente da tarefa
- **THEN** o cartão MUST trazer a data e o autor desse comentário

#### Scenario: Listagem paginada
- **WHEN** um cliente lista as tarefas em `GET /tarefas`
- **THEN** cada item MUST trazer `ultimoComentarioEm` e `ultimoComentarioAutor` com as mesmas regras do painel
