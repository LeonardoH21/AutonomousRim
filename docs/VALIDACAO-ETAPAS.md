# Validação por etapas — iniciada em 08/10/2026

Objetivo: testar todas as funcionalidades, corrigir falhas e permitir retomada sem repetir etapas aprovadas. Usar perfis isolados e preservar saves originais. Testes de mecanismos podem preparar cenários controlados; testes de autonomia devem usar trabalho, recursos e passagem do tempo nativos, velocidade 3×.

## Estado de retomada

Em andamento: etapa 7, ensaio integrado iniciado em 09/10. Etapas 0–3 aprovadas no escopo indicado; prisão nativa, comércio local, orbital e caravana passaram. Etapa 6 tem limitações explícitas e cenários adicionais pendentes. Correções de combate/resgate e armazenamento salvas no Git até `3e9de0f`; revisões posteriores detalhadas abaixo.

| Etapa | Escopo | Estado / evidência |
|---|---|---|
| 0 | Compilação, cálculo e políticas | 57 verificações passaram; complemento de testes compilado sem erros |
| 1 | Carregamento, XML, allow, comida, equipamento e construção básica | PASS: carregamento, allow, Bills, jobs reais de equipamento e desligamento; log `Player-20261008-204405-289.log`. Construção antiga excluída desta etapa; layout modular terá ensaio próprio |
| 2 | Prioridades, agenda, emergência e resgate | PASS: 14 verificações de trabalho/agenda, emergência com recuperação/restauração e resgates nativos com/sem cama |
| 3 | Progressão, pesquisa, produção e armazenamento | PASS: sete grupos, incluindo fabricação nativa de capacete, substituição de parede por pedra, prateleira construída e salvar/carregar. Progressão prolongada ainda na etapa 7 |
| 4 | Prisão: construção, captura, alimentação, tratamento, conversão, recrutamento, liberação, controle manual e salvar/carregar | PASS parcial: captura, tratamento, conversão, recrutamento, liberação, salvar/carregar e desligamento. Ensaio integrado atingiu três vagas construídas; cenários adicionais de segurança/manual pendentes |
| 5 | Comércio: produção, interação local/orbital, orçamento, entrega, caravana, controles e persistência | PASS parcial: comércio local e orbital, entrega real, reserva, itens protegidos, salvar/carregar e desligamento. PASS: caravana nativa com viagem, compra, retorno e descarga. Produção econômica prolongada e cenários de interrupção/manual pendentes |
| 6 | Combate: cenários variados, colaboração melee/ranged, retirada e resgate | Falhas identificadas; revisão em validação. Resultados individuais abaixo |
| 7 | Autonomia integrada: construção completa, inverno e sobrevivência prolongada | Em andamento, checkpoint preservado em 12,9875 dias; ainda sem aprovação |
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

Regressão dos três controles: `combat-final-controls-20261008-215329-735`, três PASS; dois cenários neutralizados, retirada contra superioridade numérica, zero mortos/derrubados, dois feridos por cenário. Inclui verificações de probabilidade nativa de tiro, parede bloqueando tiro, porta excluída de abrigo e sangramento recuperável. As 57 verificações de política passaram novamente.

Rodada `combat-final-*`, código `60f93f3`:

| Caso / perfil | Resultado | Mortos | Derrubados | Feridos |
|---|---|---|---|---|
| 1 / `215434-060` | Derrota, 3 inimigos restantes | 0 | 4 | 4 |
| 2 / `215600-952` | Vitória em 2033 ticks | 0 | 1 | 3 |
| 3 / `215703-322` | Retirada com abrigo em 1800 ticks | 0 | 0 | 4 |
| 4 / `215757-583` | Vitória em 1424 ticks | 0 | 1 | 2 |
| 5 / `215847-904` | Vitória em 1760 ticks | 0 | 0 | 3 |

O caso 1 permanece reprovado. A rodada não comprova recuperação completa no hospital; o resgate/cuidado tem ensaio próprio. Correção seguinte em validação: considerar todos os atiradores na exposição da rota melee e incluir entradas de cômodos próprios até 50 células na busca de abrigo, com limite de 16 portas e verificação de caminho. Repetir controles, caso 1, resgate e emergência antes do ensaio integrado.

Revisão da busca de abrigo: `combat-refuge-controls-20261008-220030-564` passou nos três controles, incluindo abrigo além do raio local; zero mortos/derrubados. O caso 1 (`combat-refuge-1-20261008-220131-472`) terminou sem desfecho em 6000 ticks: um inimigo restante, zero mortos, dois derrubados, quatro feridos. Dois sobreviventes se abrigaram; isso não comprova resgate dos derrubados ou vitória.

Regressões posteriores: `medical-rescue-regression-20261008-220338-207` passou nos sete controles, com resgate/tratamento nativo no hospital, estabilização no chão antes do prazo e preservação manual. `emergency-regression-20261008-220433-252` passou em detecção, suspensão, evacuação, recuperação gradual, reentrada em perigo e restauração. Nenhum processo de teste permaneceu aberto após essas rodadas.

Pendências de combate: cenário difícil ainda inconclusivo; recuperação integral depois de confrontos mistos; ensaios variados contra manhunters/mechs preparados com `autonomousrimvariedthreats`, ainda não executados. Próxima etapa: autonomia integrada por vinte dias, com checkpoints nativos.

## Ensaio integrado e retomada

Perfil atual: `.tools/validation/integrated-colony-20261009-093012-172/`. Partida quicktest nativa, cinco colonos com skills 20, sem materiais ou pesquisas concedidos pelo observador. Peaceful, velocidade 3×. Construção, alimentação, equipamento, trabalho, agenda, estratégia, prisão, comércio e emergência ligados. Caravanas permanecem opção separada; o teste nativo de viagem já tem evidência própria.

Primeiros marcos: primeiro quarto pronto em 16680 ticks (6,67 horas); mais de cem paredes antes do primeiro dia. `CheckModularSave.py` passou na geometria planejada: cinco módulos conectados, paredes compartilhadas, sem sobreposição e pisos de madeira previstos. Essa verificação não afirma que todos os pisos já foram construídos.

Retomar caso o executor termine antes da conclusão: usar `scripts/FunctionalStage.ps1 -Stage integrated-resume -Flag autonomousrimmodulartrial -ExtraFlags @('autonomousrimintegratedtest','autonomousrimprogressiontrial','autonomousrimpeacefultrial') -SuccessMarker '[AutonomousRim.ModularTrial] PASS:' -ResumeSave '<perfil>/Saves/ModularCheckpoint.rws' -TimeoutSeconds 3600`. O novo perfil preserva o checkpoint anterior e o observador mantém o início original da contagem dos vinte dias.

Exportação preparada: `ExportModularSave.ps1 -ValidationProfile '<perfil>' -Integrated`, opcionalmente `-CopyToGameSaves`. Remove somente observadores/metadados de teste, preservando a colônia. Confirmada recusa de exportação antecipada sem save final/PASS; exportação e carregamento do resultado aprovado ainda pendentes. Vinte dias de jogo não comprovam automaticamente passagem por inverno: registrar a cobertura sazonal efetivamente observada.

### Correção de armazenamento durante a partida

O ensaio inicial completou quartos em 57000 ticks, cozinha em 140760, abate em 155280 e freezer/refrigeração em 248640 (4,14 dias). Produziu Simple Meal, Fine Meal e Simple Meal Bulk por Bills nativas. Encontrou uma prateleira planejada sobre o espaço de interação do CraftingSpot comercial, impedindo o estoque de concluir seu upgrade. Foi interrompido intencionalmente após guardar `Saves/BeforeShelfFix.rws`; o manifesto mantém o erro original do executor e registra `INTERRUPTED_FOR_FIX`.

Correção: prateleiras reservam espaços de interação de bancadas existentes e planejadas, sem tratar conduítes como ocupantes exclusivos; planos automáticos não emitidos podem ser reposicionados, preservando cancelamentos e obras já iniciadas. Se não existe posição válida, o upgrade opcional não deve bloquear a sala com um plano impossível. Ao ligar construção, projetos suspensos pela própria automação retomam antes da conciliação de armazenamento; cancelamentos manuais permanecem suspensos.

`storage-interaction-regression-20261009-094847-146` passou; repetição com conduítes em todas as células (`095106-925`) também passou nos sete grupos, incluindo reserva de interação, reparo de plano antigo, cancelamento manual, fabricação/construção nativa e persistência. As 57 verificações de políticas passaram. Tentativas de retomada `095143-308`, `095604-323` e `100004-793` foram reprovadas pela geometria: a suspensão criada ao carregar ainda impedia a conciliação inicial. Nenhuma aprovação derivada dessas tentativas.

Perfil vivo após corrigir a sequência de retomada: `.tools/validation/integrated-shelf-resume-20261009-100236-258/`, carregado de `BeforeShelfFix.rws`. Mantém cinco colonos originais e início original, passou novamente na geometria, retomou construção e iniciou pesquisa nativa depois do quinto dia. Usar seu `Saves/ModularCheckpoint.rws` para a próxima retomada. A alteração da sequência de ativação ainda precisa da regressão final de controles; o ensaio integrado continua sem aprovação até vinte dias e todos os marcos.

### Retomada após limite de mensagens do jogo

O perfil `integrated-shelf-resume-20261009-100236-258` atingiu o limite de mensagens do RimWorld: `Reached max messages limit. Stopping logging to avoid spam.` A colônia continuava avançando em saves nativos; não confundir ausência de novas linhas com paralisação da IA. Interrupção intencional registrada no manifesto, com `Saves/BeforeLogThrottle.rws` preservado em 779370 ticks (12,9875 dias desde o início). Cinco colonos originais vivos, refrigeração e prisão construída registradas, dois postos de pesquisa usados, estoque/oficina concluídos e fabricação nativa de calça, camisa e capacete. Armadura, espada, equipamento completo e hospital ainda não aprovados.

Correção: limpeza de reservas mantém intervalo de 600 ticks; diagnóstico de recuperação passa a registrar a primeira ocorrência, mudança com intervalo mínimo de seis horas de jogo e repetição no máximo uma vez por dia. Último motivo/tick são persistidos para não reiniciar o spam ao carregar. Compilação limpa e 57 verificações de política passaram. `storage-log-regression-20261009-102916-771` passou nos sete grupos nativos, incluindo produção, construção de parede/prateleira e salvar/carregar.

Observador ampliado para registrar receitas concluídas que não declaram produtos fixos, como abate, e contadores nativos de caça/refeições. Compilado; sua execução prolongada será verificada na próxima retomada. Nenhum recurso ou pesquisa é concedido pelo novo observador.

Exportação corrigida para aceitar a evidência de refrigeração persistida no save de uma retomada, mantendo exigência de PASS integrado e prova de crafting. Contrato sintético isolado `export-resume-contract-20261009-102516` passou: remoção exclusiva do observador, preservação da colônia, recusa de sobrescrita e de exportação incompleta. Isso não representa exportação/carregamento do ensaio integrado real.

Ensaio variado contra seis manhunters: `combat-varied-4-20261009-103015-981`, vitória nativa em 2687 ticks, zero mortos, um derrubado e três feridos; todos os inimigos neutralizados. Ainda não comprova tratamento completo após esse combate.

Contra cinco mechanoides, `combat-varied-5-20261009-103125-541` terminou em derrota em 2190 ticks: um morto, três derrubados, quatro feridos e cinco inimigos ainda ativos. Manifesto `TACTICAL_FAILURE`; nenhuma aprovação de combate contra mechanoides. A investigação deve verificar retirada coletiva e apoio: aliados já recuando ainda entram na força local usada por `Overwhelmed`, embora sejam excluídos do apoio de avanço melee. Não alterar o resultado da rodada nem considerar uma hipótese como correção validada.

Regressão final de controles: `Player-20261009-103252-488.log` passou no carregamento, comida, equipamento, allow e nos novos controles de construção: desligar suspende projetos; religar retoma pausas automáticas; cancelamento manual continua preservado. A construção completa usa o ensaio integrado, separado deste teste de controles.

Perfil de retomada mais recente: `.tools/validation/integrated-log-resume-20261009-103502-937/`, iniciado de `BeforeLogThrottle.rws` com código `eb3bee5`. Usar seu `Saves/ModularCheckpoint.rws` na próxima retomada. Relógio original e cinco colonos preservados. Após retomar, observados `ButcherCorpseFlesh`, `Make_StoneBlocksAny` e `CookMealSimple` concluídos nativamente; contadores registraram 26 animais abatidos, tempo de caça e 112 refeições produzidas. Esses contadores não associam individualmente cada morte a uma receita. Plano estratégico mostra LongBlades e PlateArmor após a pesquisa corrente; fabricação/equipamento completo continua pendente. Correção de logs já executa no processo novo; avaliar volume até o fim, sem afirmar teste de vinte dias concluído.

Continuação acompanhada até 396,36 horas (16,5 dias), cinco colonos vivos e reserva de cerca de 3,4 dias. Geometria atual passou: sete módulos conectados, seis quartos, paredes compartilhadas e planos de pisos completos. Existem cultivos reais geridos pelo AgriculturePlanner; zero zonas modulares 13x13 neste relatório não significa ausência de plantações. Refeitório concluído; hospital e conjunto de combate continuam pendentes. Nenhuma aprovação por sobrevivência parcial.

Executor preparado para distinguir `INCOMPLETE_TIMEOUT` quando o limite de tempo externo encerra um ensaio modular com checkpoint, e `OBSERVABILITY_FAILURE` quando o jogo silencia logs por excesso de mensagens. Sintaxe PowerShell verificada; o processo longo já iniciado usa a versão anterior carregada do executor. Falhas funcionais nativas continuam `FAIL`. A mudança não concede PASS nem apaga o erro registrado.
