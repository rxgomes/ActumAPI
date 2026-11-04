## MODIFIED Requirements

### Requirement: Novas tarefas após a carga inicial
Depois da carga inicial da empresa, toda tarefa aberta ainda inexistente no Actum SHALL ser criada com status `entrada`, sem prazo interno. Conclusões e reaberturas anteriores à criação MUST ser gravadas como histórico (conclusões e retrabalhos), sem mudar o status `entrada`.

#### Scenario: Tarefa nova atribuída
- **WHEN** uma tarefa nova é atribuída ao usuário no Asana e a sincronização é executada
- **THEN** a tarefa MUST ser criada com status `entrada`, `prazo_interno` nulo e o prazo do Asana em `prazo_asana`

#### Scenario: Tarefa concluída antes do Actum e reaberta
- **WHEN** uma tarefa concluída no Asana antes de existir no Actum é reaberta e a sincronização é executada
- **THEN** a tarefa MUST ser criada com status `entrada` e um retrabalho de histórico para esse par, sem prazo pendente
