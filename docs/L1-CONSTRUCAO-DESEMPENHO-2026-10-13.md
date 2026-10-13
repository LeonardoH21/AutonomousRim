# TESTE L1 — quartos existentes, infraestrutura e desempenho

## Diagnóstico e correções

O save tinha oito camas em quartos válidos. Três projetos ainda esperavam camas nas posições antigas: `mod:1:-1:1`, `mod:1:-1:4` e `mod:1:-1:8`. As camas reais estavam dentro dos respectivos interiores, mas haviam sido deslocadas. O verificador interpretava essa alteração como construção destruída e mantinha outras salas aguardando os quartos.

A IA agora consulta camas e salas reais do RimWorld. Reconhece abrigo em quartos/dormitórios fechados, com camas humanas disponíveis, excluindo camas médicas, de prisão, proibidas ou em chamas. A alteração de um projeto antes concluído pode ser adotada sem reconstruir a cama ou o recinto do jogador. Se esse abrigo deixar de existir, o projeto volta à avaliação normal. A HUD mostra a contagem reconhecida. Essa contagem representa camas físicas; não é uma avaliação completa de conforto, propriedade ou capacidade de camas de casal.

Freezer e energia deixam de depender da correspondência exata dos quartos. O planejamento de quartos considera o abrigo existente antes de acrescentar módulos.

O maior custo repetido era consultar todos os projetos, suas células e interações para cada trabalhador. Foi criado um índice compartilhado por mapa, com atualização a cada 60 ticks e invalidação após revisão da construção. Os jobs continuam validando alvos, reservas e acesso pelo jogo. A consulta de abrigo tem cache de 120 ticks. Prioridades rotineiras passam de 180 para 600 ticks; observações econômicas são compartilhadas quando têm menos de 180 ticks. A manutenção ociosa da prisão passa a 600 ticks, mantendo a frequência de 60 para ordens/pacientes. Ingredientes mineráveis são indexados uma vez por revisão de recursos, eliminando buscas repetidas entre as 52.884 coisas do mapa.

Nenhuma API mutável de RimWorld/Unity foi transferida para outra thread.

## Medições

Todos os benchmarks carregaram uma cópia do mesmo L1, começaram no tick 373672, com oito colonos e 93 projetos, velocidade 3×, e amostraram 2.000 ticks. Os tempos de módulos são inclusivos: não devem ser somados entre si. A instrumentação também tem custo.

| Execução | TPS | CPU em MapComponentTick / 2.000 chamadas | FPS visível |
| --- | ---: | ---: | ---: |
| Antes, IA ligada, sem janela | 243,4 | 3.758,8 ms | não medido |
| Antes, IA desligada, sem janela | 337,1 | 373,0 ms | não medido |
| Final, IA ligada, sem janela | 277,2 | 2.252,4 ms | não medido |
| Final, IA ligada, janela 1920×1080 | 226,2 | 2.111,1 ms | 75,2 |
| Final, IA desligada, janela 1920×1080 | 316,3 | 351,9 ms | 87,6 |

Na comparação sem janela, o tempo de CPU do componente caiu 40,1% e o TPS cresceu 13,9%. Não existe medição de FPS visível da versão original para afirmar um ganho equivalente em FPS. As amostras visíveis são curtas e variam com renderização, aquecimento e tarefas; não garantem esse FPS em uma partida longa. FPS é frequência de desenho; TPS é velocidade da simulação. Mesmo desligando a automação, permanecem o mapa, colonos, verificações periódicas e os demais custos do jogo.

## Verificação funcional

- Benchmark final reconheceu oito camas, adotou os três quartos alterados e preservou IDs/posições originais. Freezer e energia ficaram ativos, sem bloqueio por quartos.
- Teste nativo de 30.000 ticks (12 horas de jogo), em cópia isolada e dificuldade pacífica: freezer avançou de 24 para 35 tarefas concluídas, de um total de 176; energia avançou de 0 para 48, de um total de 93. Todos os oito colonos permaneceram vivos e as camas manuais foram preservadas. Não houve spawn de recursos, cura ou conclusão instantânea. As salas ainda não estavam completas; corte de vegetação e entrega de materiais continuavam pendentes.
- Regressão de construção enfileirada passou, incluindo trabalho nativo, preservação de ordens manuais e atribuição de responsáveis.
- Regressão industrial passou, incluindo construção/transporte/fabricação nativos, filtros, prioridades de prateleiras, recuperação e save/load. A etapa isolada do contrato de prioridades remove um incêndio após confirmar a proteção contra incêndios; também manipula estados de comida/ameaça. Esse teste não representa uma partida inteira sem intervenção.

O save original não foi sobrescrito: SHA-256 `919248EB574C63AF85380F399D69843C4BEA92851E4232670F0BB3111E2D0774`. A DLL instalada e utilizada nos testes finais tem SHA-256 `72CC5CE1A39C18BE7EF4C46CDCC40C35C8451FBF239AB8C18A9D0661E4789DCC`.

O carregamento informa cinco referências antigas de genes ausentes (`VeryRobust` ×2, `Pain_VeryReduced` ×2 e `Mood_SuperDepressive` ×1). Elas aparecem nos controles também. Não alteramos os genes nem migramos o save neste pacote; isso limita a reprodução exata de um ambiente que possua esses defs.

Manifestos e resultados resumidos estão em `outputs/L1/results-2026-10-13.json`. Logs completos permanecem localmente em `.tools/validation/`. O arquivo preexistente e não rastreado `L1PerformanceChecks.cs` foi preservado e não integra este pacote.
