# Base em anel aprovada

Referência visual: [planta ampliada](base-anel.html). Implementação: `RingBasePlanner.cs`.

Esta revisão substitui o bloco linear nos **planos novos**. A base automática começa ligada em uma colônia nova. Em saves existentes, o botão conserva o estado salvo; desligar continua possível pela HUD. Um plano antigo já investido não é demolido para impor outra geometria.

## Prioridades

1. **Estoque padrão 12×11, cozinha 4×4, abate 4×4 e refeitório:** todos Critical. Cozinha e abate são salas diferentes, com parede entre elas e portas para o refeitório. O estoque exclui comida e funciona como zona antes da cobertura. Móveis essenciais e camas finais podem ser construídos cedo pelo apoio inicial.
2. **Quartos suficientes para os colonos**, 5×5 internos; freezer pesquisado, geração elétrica, cabos, acesso coberto e plantações. O núcleo oferece seis quartos e reserva uma segunda fileira para chegar a doze. A IA acrescenta quartos quando a população aumenta; não remove quartos quando diminui.
3. **Hospital, medicina, oficinas e recreação.** O hospital usa camas normais marcadas médicas. Medicina tem prioridade Critical de armazenamento. Bancadas e instalações pesquisadas são acrescentadas ao plano sem reiniciar as obras anteriores.
4. **Muro externo, depósitos especializados, demais salas, conforto, prateleiras e acabamento.** O muro aguarda os essenciais e os quartos, preservando duas passagens de duas células. Sala de fabricação e multiuso reservam espaço; esta revisão não automatiza todas as bancadas de produção nem requisitos de sala do trono.

O executor conserva o orçamento e os limites de três projetos ativos, seis novas obras por ciclo e dezoito blueprints/frames pendentes. Bloqueios são diagnosticados e não consomem recursos fictícios. Energia pode subir de prioridade em clima extremo.

## Espaço reservado

- A reserva completa mede **84×64 células**, incluindo muro, faixa externa de serviço, expansão dos quartos e terreno dos geradores.
- O pátio elétrico fica a leste, descoberto, com dois geradores a lenha como mínimo e seis posições possíveis. O número inicial é calculado pelo consumo nominal dos equipamentos planejados e margem de 20%, limitado à reserva; combustível depende de recursos reais.
- O freezer usa −2 °C. Sua saída quente tem uma faixa sem teto entre freezer e multiuso; não sopra diretamente na outra sala. Dois aquecedores usam 20 °C. A capacidade térmica não foi comprovada para todos os biomas.
- O depósito de pedras/corpos animais fica acima das oficinas/cozinha, dentro do muro e fora dos campos.
- Quarto: cama junto à parede norte, criado, cômoda, vaso e reserva de duas células de largura para cama de casal. A automação não troca a cama por casal nesta revisão.

## Plantações

Os campos preenchem os espaços externos livres do núcleo e respeitam paredes e acessos. Arroz fica no superior esquerdo (212 células); tecido no superior direito (130); batata no inferior esquerdo (190); medicina (118) e uma faixa menor de hemp (12) no inferior direito. Total previsto: 662 células.

As zonas são criadas depois do estoque, alimentação e quartos, somente em solo suficientemente fértil, descoberto e sem construção. Solo inapto reduz a área efetiva. Não se altera uma zona existente do jogador; a cultura que ele mudar em uma zona da IA permanece alterada. A semeadura, colheita, habilidade mínima e disponibilidade dos trabalhadores seguem o jogo nativo.

O jogo base usa **algodão** para tecido e **smokeleaf** para a faixa chamada hemp. Se houver plantas semeáveis de mod com `flax`/`hemp` no nome da definição, o planejador usa essas definições. Não instala plantas ou mods ausentes.

## Obstáculos, chão e persistência

O local deve ter terreno firme, não estar encoberto pela névoa e não conter zonas, construções, obras ou áreas Sem teto do jogador. Vegetação e itens permitidos são limpos/movidos pelo trabalho normal. Minerais neutros nas obras e acessos recebem até oito marcações de mineração simultâneas; a construção aguarda a escavação nativa. Nada é apagado instantaneamente pela produção.

Pisos são acabamento posterior. Um piso comum não é usado para fingir suporte em água, lama ou outro terreno inapto. Nesses casos, procura-se outra clareira. Pontes/aterros especializados continuam pendentes.

Uma porta nova entre fileiras pode exigir desmontar uma parede **concluída e rastreada como pertencente à IA**; o pedido usa a desmontagem nativa e é removido ao desligar. Paredes do jogador não recebem essa abertura automática. Instalações e cancelamentos manuais continuam protegidos.

Âncora, módulos, reserva, culturas, referências de zonas, cobertura, obstáculos e tarefas são gravados no save. Replanejar o mesmo núcleo não cria módulos duplicados. Recarregar mantém a automação ligada/desligada como salva.

## Verificação

Compilação de produção e complemento de teste sem erros/avisos; 35 verificações de política aprovadas. O teste isolado `NormalConstructionTest.ps1 -RingPlanOnly` usa uma colônia temporária, prepara solo plano e recursos de fixture, e verifica geometria/colisões/interações nativas, prioridades, estoque, quantidade de quartos, vazão limitada de blueprints com orçamento, culturas, mineração/desmontagem, desligamento e salvar/carregar nativo.

Para verificar transições de zonas, a fixture marca essenciais/quartos como completos, sem construir fisicamente esses módulos. Também fornece recursos, colonos com habilidades 20 e uma parede atribuída à IA para testar uma abertura. A fixture isola hostis gerados durante a preparação e simula uma nova oportunidade de execução ao religar após o carregamento; a produção conserva a pausa por perigo e o limite de execução por tick. Essas preparações existem apenas no complemento de teste.

Ele **não demonstra a construção completa desta base expandida por colonos**, nem mede velocidade de construção, produção prolongada dos campos ou desempenho térmico em todos os biomas. O teste completo anterior foi feito no layout linear; não representa este plano. Killbox, airlocks e ampliação além de doze quartos continuam pendentes.

## Planejamento de longo prazo e rotina

O planejador estratégico mantém uma rota de pesquisa com pré-requisitos visíveis e ocultos, reavalia energia, comida, medicina, armas, produção e defesa, registra itens desbloqueados e separa metas de curto, médio e longo prazo. A rota padrão aponta para a nave construída na colônia; ela não concede pesquisa grátis, não substitui uma escolha manual de pesquisa e não declara a vitória antes que a construção e o lançamento sejam executados.

Quando pesquisas liberam bancadas, o sistema tenta incorporá-las aos setores existentes por tarefas aditivas e validadas. Ele não duplica instalações, não desloca equipamentos do jogador e registra o bloqueio quando faltam materiais, espaço, bancada ou requisitos nativos.

Work Priorities são revistas durante a partida. Skills, paixões, função, saúde, humor, descanso, urgência, trabalho pendente, comida, recursos, incêndios e feridos alteram temporariamente a escolha. O executor preserva uma alteração manual quando detecta que o jogador mudou a prioridade aplicada pela IA.

A agenda usa Sleep, Work, Recreation, Anything e Meditation quando o colono possui suporte para meditar. Hostis, incêndios e colonos incapacitados entram em modo de emergência; ao terminar, a IA mantém um período de recuperação para sono, recreação e necessidades básicas. A agenda manual por horário também é preservada, e o botão da HUD restaura a agenda anterior quando a automação é desligada.

## Análise de falha e checkpoints

O diagnóstico monitora fome, agricultura, Cooking, armazenamento, energia, medicina, equipamentos, armas, armaduras, número de colonos, prioridades, agenda, pesquisa, construção, defesa, combate, recursos e velocidade de progressão. Ele acumula evidência por avaliações sucessivas para não declarar derrota por um único evento ruim. Enquanto houver recuperação plausível, o status fica em plano de emergência; a execução só recebe um relatório completo após colapso terminal ou risco crítico persistente.

O relatório salvo classifica cada sinal como pequeno, moderado, grave ou crítico e separa as categorias. Ele registra evento observado, decisão esperada, resultado real, comparação esperada/real, cadeia de causas e ajustes concretos. Os ajustes são evidências acumuladas, com confiança limitada, e não regras absolutas aplicadas a qualquer mapa ou storyteller.

### Horizonte sustentável de 500 dias

O plano estratégico acompanha um horizonte aproximado de **500 dias de jogo** e recalcula a cada avaliação o dia atual, os dias restantes, o objetivo em foco e o próximo objetivo. A lista de metas é separada em curto, médio e longo prazo. Cada meta grava se foi atendida, o bloqueador atual, os recursos necessários e o risco que ela pretende evitar.

Antes de acelerar pesquisa tecnológica ou a rota da nave, o planejador calcula uma estabilidade de 0 a 100 usando reserva de comida, hostis, colonos incapacitados, saúde, humor, descanso, capacidade de combate, infraestrutura essencial, aço e componentes. Fome grave, feridos, hostis ou estabilidade baixa mantêm o foco em recuperação, defesa, comida, medicina, energia e abrigo. A pesquisa manual continua soberana; quando a IA possui a pesquisa de longo prazo e a colônia entra em instabilidade, ela pode trocar temporariamente para uma pesquisa de recuperação sem apagar o progresso da nave.

A avaliação de terreno registra teto espesso, espaço livre e bordas conectadas de uma possível área montanhosa. Uma montanha candidata recebe um plano de análise para entradas, duas fugas, corredores, portas, chokepoints, melee block, linhas de tiro, temperatura, infestação e expansão por etapas. Esse registro não autoriza escavação automática: o anel externo continua sendo a opção segura até que os riscos sejam validados no mapa.

Quando uma nova presença hostil é detectada, a IA registra um snapshot de força amiga/inimiga, composição ranged/melee, risco, feridos e comida e cria um save separado `AutonomousRim_Raid_*`. Até doze snapshots ficam no histórico do save da colônia para futuras comparações. O checkpoint não sobrescreve o save do jogador e não reinicia a partida. A análise não treina um modelo externo: preserva evidências estruturadas para decisões táticas futuras.
