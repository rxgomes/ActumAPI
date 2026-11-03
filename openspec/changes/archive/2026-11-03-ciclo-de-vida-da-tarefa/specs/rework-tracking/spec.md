# Spec Delta

## ADDED Requirements

### Requirement: Novo prazo após reabertura
Quando uma tarefa com `prazo_interno_pendente = true` recebe um prazo interno, o prazo SHALL ser gravado também como `prazo_interno_novo` do retrabalho aberto, e a pendência MUST ser removida.

#### Scenario: Definir novo prazo
- **WHEN** uma tarefa reaberta, com prazo pendente, recebe `prazoInterno = 2026-11-07`
- **THEN** a tarefa MUST ficar com `prazo_interno = 2026-11-07` e `prazo_interno_pendente = false`, e o retrabalho aberto MUST ter `prazo_interno_novo = 2026-11-07`

### Requirement: Conclusão resolve o retrabalho
Ao concluir uma tarefa, o retrabalho aberto da tarefa, se houver, SHALL receber a data de resolução.

#### Scenario: Conclusão após retrabalho
- **WHEN** uma tarefa em `retrabalho` é concluída
- **THEN** o retrabalho aberto MUST ter `resolvido_em` preenchido com a data e hora da conclusão
