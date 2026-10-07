# Trabalho e agenda dinâmica

As prioridades são recalculadas a cada ciclo de gerenciamento da colônia (360 ticks). A emergência global é verificada a cada 60 ticks. As opções **Prioridades de trabalho**, **Agenda** e **Emergência** continuam independentes no painel Rim AI.

## Distribuição de funções

- A seleção combina nível da habilidade, paixão, saúde, velocidade efetiva de trabalho, humor, descanso e quantidade de funções já atribuídas ao colono. Há um pequeno bônus para manter um especialista adequado, reduzindo trocas desnecessárias.
- Comida abaixo da reserva desejada promove cozinha e cultivo; abaixo de um dia, esses trabalhos entram antes de construção e coleta na seleção. Poucas refeições prontas também protegem o cozinheiro quando há uma bill ativa, mesmo com bastante alimento cru. Quando as reservas se recuperam, a promoção temporária termina.
- Pacientes que precisam de descanso médico ou estão incapacitados promovem Doctor. Patient, PatientBedRest e BasicWorker usam os nomes reais de RimWorld e normalmente têm prioridade 1. Um médico selecionado com ferimentos leves pode receber Patient/PatientBedRest em 2 para atender os demais antes de deitar; essa exceção não vale para saúde grave.
- Plantios pendentes e colheitas, blueprints/frames, designações de mineração/corte/caça e bills ativas dimensionam a demanda. Filas maiores permitem mais especialistas, limitados a aproximadamente metade dos trabalhadores disponíveis por função.
- Construção essencial tem prioridade 1. Um único construtor capaz de terminar um frame já financiado é protegido de cozinha e coleta quando há alternativas e a comida não está crítica.
- Mineração/corte ganham prioridade 1 quando há coleta para construção e tarefas efetivamente designadas. Estoque baixo sozinho não promove um trabalho sem alvos. O gerente de recursos existente continua responsável por localizar e designar recursos.
- Bills de bancadas também selecionam especialistas de produção, como Tailoring, Smithing e Art, usando as bancadas dos WorkGiverDefs nativos. Bills suspensas ou que atingiram seu alvo não contam.
- Incêndios próximos da colônia promovem Firefighter. Durante emergência, trabalhos secundários seguem os bloqueios existentes e o médico principal é selecionado por aptidão.
- Incapacidades, trabalhos desabilitados, colonos recrutados e estados mentais são respeitados. Alterações manuais de prioridades continuam protegidas.

O sistema escolhe funções a partir da situação atual, sem uma profissão permanente configurada pelo jogador. A fila contabiliza tarefas e bills, sem estimar precisamente horas de trabalho ou garantir que cada ingrediente e rota estejam disponíveis; os gerentes de construção, comida e segurança e os WorkGivers nativos fazem essas verificações na execução. Trabalhos de mods que não declaram bancadas em `fixedBillGiverDefs` mantêm a prioridade geral de apoio.

## Saúde e recuperação

Trabalhos produtivos são suspensos quando o colono precisa de descanso médico urgente, tem saúde abaixo de 75% ou apresenta descanso abaixo de 25%, recreação abaixo de 20%, humor abaixo de 30% ou comida abaixo de 15%. Autocuidado continua habilitado. Ferimentos leves mantêm a possibilidade de um médico apto atender os demais, evitando desabilitar todos os médicos em uma colônia ferida. Em crise de comida, um colono com fome que ainda tem saúde, descanso, recreação e humor suficientes pode continuar cozinha/cultivo para conseguir alimento; não recebe tarefas secundárias. A mesma proteção se aplica ao trabalho da emergência, com exceção da cozinha de sobrevivência em local seguro; colonos recrutados continuam sob o gerente de combate.

A agenda mantém recuperação pessoal até atingir descanso de 60%, recreação de 45%, humor de 40%, comida de 30% e não precisar mais de descanso médico. A recuperação após emergência tem um intervalo mínimo de 6.000 ticks e só acaba quando as necessidades estão recuperadas e a emergência global voltou ao normal.

## Horários

A rotina comum reserva sono das 22h às 6h, trabalho das 8h às 17h, recreação das 17h às 19h e Anything nas transições. O traço notívago desloca a rotina em 12 horas, reservando sono das 10h às 18h, cobrindo o intervalo nativo de penalidade por ficar acordado de dia (11h–17h).

Sono, fome, recreação, humor e descanso médico podem alterar a hora atual e a seguinte. As necessidades atuais não transformam as 24 horas em sono ou recreação. Na recuperação, os períodos normais de Work viram Anything. Meditação é prevista somente com Royalty e psylink ativo; colonos comuns não recebem esse horário.

Uma emergência pode substituir a rotina por Work, mas respeita as proteções de necessidades e a recuperação pessoal. A agenda não cancela ordens de combate para enviar um colono exposto para a cama. Horários editados manualmente, inclusive devolvidos ao valor original, permanecem protegidos. Desligar a agenda restaura somente alterações que ainda pertencem à IA. A propriedade das alterações e a recuperação são serializadas nos saves.

## Validação

O teste nativo dedicado usa quatro colonos, habilidades e necessidades controladas e tarefas reais em um perfil separado. Verifica falta/reposição de comida, paixão, filas de coleta e construção, incêndio, bill de costura, médico, descanso, recreação, fome, humor, notívago, meditação com psylink, emergência, recuperação, alterações manuais e save/load. Depois de recarregar, um colono exausto recebe apenas a agenda e uma cama: o próprio jogo deve iniciar LayDown e aumentar seu descanso durante 600 ticks, sem uma ordem de sono forçada.

Execução, após compilar o mod e o projeto `Tests/AutonomousRim.RuntimeChecks` e instalar a DLL:

```powershell
./scripts/CombatTest.ps1 -WorkScheduleChecks
./scripts/CombatTest.ps1 -EmergencyChecks
```

Os testes usam perfis em `.tools/work-schedule-tests` e `.tools/emergency-tests`, sem carregar saves do jogador. São verificações funcionais curtas, não uma simulação de sobrevivência de longo prazo nem cobertura de todos os mods.

Resultado em 6 de outubro de 2026: 14 grupos nativos de trabalho/agenda e 11 grupos nativos de emergência passaram. Os 35 checks existentes de política também passaram. Logs locais finais: `.tools/work-schedule-tests/Player-20261007-004531.log` e `.tools/emergency-tests/Player-20261007-004620.log` (nomes com data UTC). A DLL compilada e a instalada no jogo têm o mesmo SHA-256: `BAA3528E5D468835F65CEED00C3D7CF03AF3C421AF801025FA7874FF0EEEA0AF`.
