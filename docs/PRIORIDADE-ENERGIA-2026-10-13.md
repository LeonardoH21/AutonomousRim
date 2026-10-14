# Prioridade da energia

Sala de energia, geradores auxiliares, conexão elétrica, baterias e complementos de energia recebem prioridade **Critical**. A energia também ganha preferência no desempate da emissão de obras e na pontuação dos alvos de construção/transporte. A preparação do terreno associada recebe a mesma prioridade. A antiga condição que só elevava a sala durante temperaturas extremas foi removida.

O atendimento de emergência e a entrega urgente de comida continuam sob os controles existentes. A mudança preserva a sequência normal de paredes, pisos e móveis e não reabre instalações manuais já reconhecidas como concluídas.

Teste nativo em cópia do TESTE L1, velocidade 3× e dificuldade pacífica, sem spawn de recursos ou conclusão instantânea:

- Primeira amostra de 12 horas: energia 0 → 84 tarefas; freezer 24 → 24. O check geral de progresso do freezer falhou nesse prazo, mostrando o adiamento das outras obras.
- A verificação foi estendida para 24 horas com uma opção específica do teste; o teste padrão de 12 horas permanece disponível. Nova execução: energia 0 → 92 de 93 tarefas; freezer 24 → 35 de 176. Ambos avançaram, oito colonos permaneceram vivos e as camas manuais foram preservadas. A energia ainda tinha uma tarefa pendente.

As execuções variam com a simulação; a contagem não garante um prazo fixo para outros saves. O save original não foi sobrescrito. Manifestos e resultados ficam em `outputs/PowerPriority/results-2026-10-13.json`.
