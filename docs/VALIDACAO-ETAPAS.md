# Validação por etapas — iniciada em 08/10/2026

Objetivo: testar todas as funcionalidades, corrigir falhas e permitir retomada sem repetir etapas aprovadas. Usar perfis isolados e preservar saves originais. Testes de mecanismos podem preparar cenários controlados; testes de autonomia devem usar trabalho, recursos e passagem do tempo nativos, velocidade 3×.

## Estado de retomada

Em andamento: etapa 6, repetição dos cinco combates mistos após correções. Etapas 0–3 aprovadas no escopo indicado; prisão nativa, comércio local, orbital e caravana passaram. Construção completa atual é etapa 7. Infraestrutura de retomada salva no Git em `0a3ab83`; revisão tática posterior ainda em validação.

| Etapa | Escopo | Estado / evidência |
|---|---|---|
| 0 | Compilação, cálculo e políticas | 57 verificações passaram; complemento de testes compilado sem erros |
| 1 | Carregamento, XML, allow, comida, equipamento e construção básica | PASS: carregamento, allow, Bills, jobs reais de equipamento e desligamento; log `Player-20261008-204405-289.log`. Construção antiga excluída desta etapa; layout modular terá ensaio próprio |
| 2 | Prioridades, agenda, emergência e resgate | PASS: 14 verificações de trabalho/agenda, emergência com recuperação/restauração e resgates nativos com/sem cama |
| 3 | Progressão, pesquisa, produção e armazenamento | PASS: sete grupos, incluindo fabricação nativa de capacete, substituição de parede por pedra, prateleira construída e salvar/carregar. Progressão prolongada ainda na etapa 7 |
| 4 | Prisão: construção, captura, alimentação, tratamento, conversão, recrutamento, liberação, controle manual e salvar/carregar | PASS parcial: captura, tratamento, conversão, recrutamento, liberação, salvar/carregar e desligamento. Construção natural da prisão e cenários adicionais de segurança/manual pendentes |
| 5 | Comércio: produção, interação local/orbital, orçamento, entrega, caravana, controles e persistência | PASS parcial: comércio local e orbital, entrega real, reserva, itens protegidos, salvar/carregar e desligamento. PASS: caravana nativa com viagem, compra, retorno e descarga. Produção econômica prolongada e cenários de interrupção/manual pendentes |
| 6 | Combate: cenários variados, colaboração melee/ranged, retirada e resgate | Falhas identificadas; revisão em validação. Resultados individuais abaixo |
| 7 | Autonomia integrada: construção completa, inverno e sobrevivência prolongada | Pendente |
| 8 | HUD, relatório final, saves para revisão, instalação e Git | Pendente |

Logs ficam em `.tools`, saves/checkpoints de partidas também. Não chamar uma etapa de aprovada apenas porque compilou ou emitiu uma ordem: confirmar o resultado real exigido pelo cenário. Registrar falhas, correção e repetição, incluindo limites de cobertura.

Falha inicial: `.tools/perception-test/Player-20261008-203628-681.log`, erro XML no carregamento. Nenhuma conclusão sobre comércio ou prisão ainda.

## Correções e evidências da primeira rodada

- Removido campo XML inválido da definição AutonomousRim_Trade, confirmado carregamento posterior sem esse erro.
- Corrigida comparação de IDs nulos no comércio: impedia iniciar trabalho e causava exceção ao desligar comércio ocioso. Adicionada regressão de controles, parada e suspensão sem negociação.
- Correção equivalente na limpeza de auxiliares da prisão sem referência/job válidos, coberta na regressão de controles.
- Fixture de Cooking agora fornece ingredientes para testar produção em lote, sem depender do mapa aleatório. Escassez simulada atualiza também Work Priorities antes de testar caça.
- Fixture histórica de construção exige layout compacto antigo e limites antigos; não valida o atual núcleo modular. Mantida separada com opção explícita `-SkipLegacyConstruction`; nenhuma aprovação de construção atual derivada disso.
- Script `scripts/FunctionalStage.ps1` cria diretório único, manifesto com hashes, log e saves por etapa. Não sobrescreve perfis anteriores. Retoma cenários independentes pelo respectivo flag; partidas longas usam checkpoints do executor específico.

Evidência local:

- Básicos: `.tools/perception-test/Player-20261008-204405-289.log`.
- Work/Schedule: `.tools/validation/work-schedule-20261008-204622-806/`, inclui `WorkScheduleRoundtrip.rws`, 14 PASS e DONE.
- Emergência: `.tools/validation/emergency-20261008-204736-910/`, inclui save verificado e DONE.
- Resgate: `.tools/validation/medical-rescue-20261008-204914-884/`, tratamento real com cama, estabilização no chão antes do prazo, proteção de ordem manual e DONE.

Não confundir essas fixtures com sobrevivência prolongada, produção econômica completa ou cinco vitórias em combate. Etapas 4–8 permanecem pendentes até suas respectivas evidências.

## Progressão e prisão

- Progressão: `.tools/validation/progression-20261008-205114-639/`: sete PASS e DONE. Recursos e estados preparados em fixture; não representa evolução econômica completa.
- Prisão: a propriedade ForPrisoners sozinha não atualizava IsInPrisonCell; corrigida configuração com notificações nativas de distrito/sala e migração de camas próprias existentes. O teste confirmou que a cama marcada antes era rejeitada por RestUtility.
- Tentativas anteriores de prisão registradas como FAIL/interrompidas. Ajustadas fixtures de necessidades ausentes em inimigos, enumeração mutável e seleção de candidato (paixões aleatórias podiam tornar ambos recrutáveis). Captura repetida com resultado nativo; conversão/recrutamento ainda em andamento.

- Comércio local: `.tools/validation/commerce-20261008-210727-235/`: quatro PASS e DONE; `CommerceRoundtrip.rws` contém transação concluída. Fixture forneceu comerciante, produtos e déficits; cultivo/fabricação e viagem comercial ainda não validados.
- Prisão: `prison-20261008-210428-997` capturou, tratou, converteu, recrutou e liberou, mas falhou na verificação final porque o teste salvava antes da próxima atualização de registro. Corrigida sincronização da fixture; repetição `prison-20261008-210807-417` em andamento.

- Prisão concluída: `.tools/validation/prison-20261008-210807-417/`: seis PASS e DONE, `PrisonRoundtrip.rws`. Resistência/certidão inicial de um candidato reduzidas apenas na fixture para acelerar as interações; captura, cuidado, conversão, recrutamento e saída ocorreram por jobs nativos.
- Orbital: `.tools/validation/commerce-orbital-20261008-211203-728/`: cinco PASS e DONE. Console e beacon alimentados por gerador/conduítes; negociação, pods e reconhecimento de estoque realmente entregues. Primeira tentativa falhou por nome errado do gerador na fixture; corrigido para WoodFiredGenerator.
- Caravana: `.tools/validation/commerce-caravan-20261008-211317-981/` em andamento: cenário controlado com destino amistoso vizinho, mercadorias e reservas; viagem/formação nativas exigidas.

- Caravana concluída: `.tools/validation/commerce-caravan-20261008-211508-209/`: seis PASS e DONE. Formação, mercadorias reais, viagem, venda/compra, retorno, descarga e persistência. Ensaio anterior conferia o depósito cedo demais, com compras no inventário; fixture passou a exigir armazenamento e descarga. Não houve criação gratuita das compras pela IA.
- As próximas fixtures filtram também incapacidades sem skill (por exemplo Hauling); a rodada aprovada tinha transporte suficiente, mas emitiu aviso ao configurar Hauling de um colono incapaz. Essa seleção da fixture foi corrigida, sem remover restrições dos colonos normais.

## Estratégia e combate

- Estratégia: `.tools/validation/strategy-20261008-211953-890/` PASS após corrigir expectativas antigas da fixture: cap de allow agora 16 e cinco checkpoints militares/inverno, com nave adiada. Nenhuma redução do cap ou retomada de nave introduzida para satisfazer teste antigo.
- Análise de falhas: `.tools/validation/failure-analysis-20261008-212032-665/` PASS: risco recuperável, evidência repetida de colapso, relatório causal, ajustes aprendidos, checkpoint de raid e persistência.
- Controles de combate: `.tools/validation/combat-controls-20261008-212302-071/`: três PASS, dano/interceptação nativos, cobertura e retirada sob força superior. Retirada: três feridos, zero mortos/derrubados; não representa recuperação médica já concluída.
- Próximo ensaio integrado: nova partida nativa, cinco colonos com skills 20, recursos normais, 3×, mínimo de 20 dias, núcleo/refrigeração, produção real, pesquisa/crafting/equipamento, hospital/oficina e prisão construída. Saves de checkpoint preservam início e marcos para retomada com `FunctionalStage -ResumeSave`. Não aprovado antes de cumprir esses critérios.

## Cinco combates e regressões táticas

Fixtures usam quatro aliados saudáveis, skills 20, dois melee com armadura de placas, dois ranged com colete e quatro capacetes. Terreno, equipamentos e inimigos são preparados; dano, movimentação, tiros e incapacitação são nativos. Cinco situações incluem desvantagem. RNG e geração dos participantes variam: comparação não é um benchmark estatístico determinístico.

| Caso | Rodada inicial `combat-melee-*` | Primeira revisão `combat-revised-*` |
|---|---|---|
| 1 | Derrota: 1 morto, 3 derrubados; melee não iniciou ataque | Sem desfecho em 6000 ticks: 0 mortos, 3 derrubados, 2 inimigos restantes |
| 2 | Vitória: 0 mortos/derrubados, 2 feridos | Sem desfecho em 6000 ticks: 0 mortos, 2 derrubados, 2 inimigos restantes |
| 3 | Retirada: 0 mortos/derrubados, 3 feridos | Retirada: 0 mortos/derrubados, 3 feridos |
| 4 | Vitória: 0 mortos, 3 derrubados | Vitória: 0 mortos, 3 derrubados |
| 5 | Vitória: 0 mortos, 1 derrubado | Vitória: 0 mortos/derrubados, 3 feridos |

Não considerar DONE como vitória. O executor agora registra `TACTICAL_FAILURE` para derrota e `INCONCLUSIVE` quando não há desfecho. Saves e logs de cada confronto permanecem em `.tools/validation/`.

Correções em validação: porta não conta como abrigo; exposição considera todos os atiradores, armadura e probabilidade nativa de acerto; sangramento recuperável não provoca retirada apenas pelo valor bruto; contato com ranged também exige autodefesa melee quando não há velocidade para escapar. A primeira revisão, excessivamente conservadora, causou timeout nos controles (`combat-controls-revised-20261008-213113`); após ajuste do critério de ferimentos, os três controles passaram em `combat-controls-revised-20261008-213554-375`.

Teste específico de contato: a primeira fixture confirmou a ordem inicial, mas exigia neutralização durante retirada contra atirador que podia se afastar. Falhou em `contact-defense-20261008-214658-640`; tentativa seguinte foi interrompida. Outra tentativa (`215147-301`) também não neutralizou o inimigo. A fixture final isola autodefesa em contato e exige dano nativo sem derrubar o defensor, sem alegar vitória. PASS e DONE em `contact-defense-20261008-215257-002`.

Regressão final dos três controles: `combat-final-controls-20261008-215329-735`, três PASS; dois cenários neutralizados, retirada contra superioridade numérica, zero mortos/derrubados, dois feridos por cenário. Inclui verificações de probabilidade nativa de tiro, parede bloqueando tiro, porta excluída de abrigo e sangramento recuperável. As 57 verificações de política passaram novamente. Cinco casos finais ainda em execução.
