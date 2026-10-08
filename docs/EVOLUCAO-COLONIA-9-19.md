# Evolução da colônia — pedido 9 a 19

Pedido recebido em 08/10/2026. Ordem aplicada: métricas e checkpoints → demanda de equipamentos/bancadas → reservas e materiais → armazenamento/pedra → agricultura → sala social → validação nativa. A nave continua adiada.

## Implementação

| Item | Comportamento atual |
| --- | --- |
| 9 — depósito | Conta pilhas, estima capacidade e adiciona até duas prateleiras pequenas por lote. Cada célula de prateleira comporta três pilhas, acrescentando duas posições sobre o chão. Preserva eixos de passagem, portas e cancelamentos. Após três dias pode iniciar uma melhoria pequena mesmo sem lotação extrema; não cria depósitos dispersos para resolver o volume. |
| 10 — métricas de conjuntos | Contagem de combatentes/ranged/melee, conjuntos vestidos, peças melhores acessíveis, déficit de fabricação, custo estimado pelos ingredientes nativos e impedimentos por pesquisa/bancada/habilidade/material. Visível no painel estratégico e persistente no save. |
| 11 — checkpoints | Tecnologia pesquisada não conclui a etapa. Só equipamentos vestidos e utilizáveis liberam a etapa ofensiva seguinte. A rota deixa de percorrer automaticamente toda a árvore ao terminar as pesquisas cadastradas. Comida/medicina/energia/obras continuam na rota de utilidades. |
| 12 — bancadas | Forja e alfaiataria da oficina, usinagem industrial e módulo de fabricação continuam ligados aos desbloqueios nativos. Demanda identifica a bancada ausente; bills usam apenas receitas disponíveis e bancadas realmente construídas. Componentes industriais/avançados recebem metas para alimentar a etapa spacer. |
| 13 — ranged | Inicial → Heavy SMG/flak → Assault Rifle → Charge Rifle/marine. São referências comparadas por utilidade, condição e qualidade; armas equivalentes ou melhores satisfazem a etapa. Não exige fabricar uma arma intermediária para quem já está melhor equipado. |
| 14 — melee | Espada de aço/placas/capacete → proteção industrial e armas/materiais melhores disponíveis → marine/cataphract e escudo. A melhor arma adquirida pode satisfazer o objetivo sem aguardar um item raro específico. |
| 15 — checkpoint da colônia | Analisa arma, roupa vestida, equipamentos acessíveis e demanda antes da próxima pesquisa ofensiva. Prioriza médicos, reservas e estabilidade. Equipamentos forçados/políticas manuais impedem fabricação repetida de peças que o colono não poderá vestir. |
| 16 — pedra | Corte de pedra na rota, mesa da oficina e bill nativa até reserva de 100–600 blocos conforme população/obras. Escassez e perigo suspendem produção secundária. Blocos disponíveis podem entrar nas paredes futuras. Após estabilidade das obras básicas, substitui uma parede de madeira da própria IA por pedra, com desconstrução e construção reais. Remover a marca de desconstrução veta a troca; paredes do jogador não entram nessa rotina. |
| 17 — sala social | Refeitório e recreação combinados, mesa 3×3, doze lugares ao redor, xadrez com duas cadeiras, luz, piso e vaso. Acabamento continua separado do abrigo e abaixo das bancadas essenciais. Planos antigos/construções existentes são preservados. |
| 18 — inverno | No preparo para inverno/frio, bills de parka e tuque visam um item utilizável por colono, contando os vestidos e rejeitando tainted e peças com menos de 51% de HP. A meta estratégica aceita reservas guardadas; não exige vestir a parka antecipadamente. Comida e combustível seguem as reservas anteriores. |
| 19 — agricultura | Mantém um agricultor quando existe zona agrícola e alguém apto; cultivo continua com reservas suficientes. Sem tarefas agrícolas, transporte passa à frente das outras tarefas; sem transporte, trabalhos de reserva continuam possíveis. Algodão é dimensionado pelos custos nativos de roupas e equipamento, população, estoque de cloth e rendimento da cultura. Planeja arroz/algodão/healroot em células acessíveis e férteis, sem sobrepor parcelas. |

## Regras importantes

As metas de fabricação descontam equipamentos vestidos suficientes e itens melhores disponíveis que podem ser equipados. Bills da IA preservam ordens do jogador, inclusive edições dos filtros de HP e qualidade; receitas e trabalho continuam nativos. Saves com a assinatura anterior das bills são migrados. A etapa avançada espera pelo menos três dias de alimento, ausência de colonos caídos e sangramento. A seleção ofensiva também usa estabilidade global; pesquisa spacer espera energia e fabricante 8+. O sistema não dá pesquisa, equipamento, materiais ou cura à colônia real.

Paredes de pedra são substituídas individualmente, com material reservado e um construtor disponível. Enquanto a substituição está pendente, nenhuma outra é iniciada. A obra pode abrir temporariamente uma célula do cômodo; uma ameaça suspende trabalho secundário pelas regras de emergência. Não há reconstrução instantânea em caso de raid.

As métricas de ingredientes são estimativas usando aço/cloth como referências para ingredientes flexíveis; couro e outros materiais nativos ainda podem ser escolhidos pelas bills. Capacidade do depósito é uma estimativa de posições, não volume ou quantidade de unidades individuais. Não se troca mobiliário intacto apenas para tentar obter uma rolagem de qualidade maior.

Durante ameaça imediata, preserva o equipamento em uso e suspende trocas secundárias conforme o sistema de emergência. Troca antecipada de roupa de inverno para armadura antes de uma raid ainda precisa de um detector de janela segura de preparação; não foi adicionada uma corrida ao estoque durante ataque.

## Validação e retomada

```powershell
.\scripts\CombatTest.ps1 -ProgressionChecks
.\scripts\CombatTest.ps1 -WorkScheduleChecks
.\scripts\CombatTest.ps1 -MedicalRescueChecks
```

A suíte de progressão usa perfil privado, dificuldade Pacífica e velocidade 3×; prepara cinco colonos com skills 20, sem traços/incapacidades, recursos, equipamentos e pesquisas necessários ao cenário. Depois da preparação, a fabricação do novo capacete e a troca da parede usam custos, jobs, consumo e trabalho nativos. Itens já presentes são registrados para que um capacete antigo largado no chão não seja contado como produto novo.

Execução final `progression-tests/20261008-170400/Player-20261008-170400.log`: sete grupos passaram — checkpoints/equipamento/materiais (incluindo plasteel/ouro/insumos de componentes) e proteção de roupa manual, inverno/filtro manual de HP, prateleiras e culturas, agricultor/transporte, pedra/sala social, fabricação/troca real de parede/construção da prateleira com três posições e filtro correto, save/load dos planos/métricas/parede e migração da assinatura antiga das bills.

Regressão de trabalho/agenda `work-schedule-tests/Player-20261008-165837.log`: quatorze grupos passaram, incluindo prioridades dinâmicas, sobrevivência, médico, necessidades, edição manual, save/load e sono nativo. Resgate `medical-rescue-tests/20261008-165902/Player-20261008-165902.log`: atendimento na cama/no chão, defesa restante, liberação automática e proteção de ordens manuais passaram. Os 57 checks de políticas passaram. Builds de produção e complemento sem avisos/erros.

Ainda falta validar a economia inteira de uma partida natural do início até marine/cataphract e a reserva num inverno inteiro. As verificações atuais provam decisões e trabalhos específicos, não produção contínua de todos os tiers. O antigo teste natural de aço continua separado, preservado no checkpoint registrado em [PLANO-PESQUISA-DEFESA.md](PLANO-PESQUISA-DEFESA.md); não conta como aprovado.

Próximo teste de longa duração: partida nova com cinco colonos 20, Pacífico e 3×; observar bancada construída → bill executada → peça nova equipada em cada etapa, consumo de materiais/componentes, falta de comida e preparação para frio. Salvar checkpoints por etapa para continuar sem recriar colonos ou recursos quando a sessão for interrompida.
