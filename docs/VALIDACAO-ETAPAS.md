# Validação por etapas — iniciada em 08/10/2026

Objetivo: testar todas as funcionalidades, corrigir falhas e permitir retomada sem repetir etapas aprovadas. Usar perfis isolados e preservar saves originais. Testes de mecanismos podem preparar cenários controlados; testes de autonomia devem usar trabalho, recursos e passagem do tempo nativos, velocidade 3×.

## Estado de retomada

Em andamento: repetir a etapa 7 após correções de pesquisa e expansão. Recuperação clínica por checkpoint passou, sem mortes, preservando sequelas nativas. Prisão passou em seis guardas e recaptura controlada; contenção de rebeliões continua pendente. HUD passou na auditoria visual dos 12 controles após correção da rolagem. Ensaio integrado anterior chegou a trinta dias com cinco sobreviventes, mas reprovou progressão militar e conectividade modular; não exportar como aprovado. Evidências e limites detalhados abaixo.

| Etapa | Escopo | Estado / evidência |
|---|---|---|
| 0 | Compilação, cálculo e políticas | 57 verificações passaram; complemento de testes compilado sem erros |
| 1 | Carregamento, XML, allow, comida, equipamento e construção básica | PASS: carregamento, allow, Bills, jobs reais de equipamento e desligamento; log `Player-20261008-204405-289.log`. Construção antiga excluída desta etapa; layout modular terá ensaio próprio |
| 2 | Prioridades, agenda, emergência e resgate | PASS: 14 verificações de trabalho/agenda, emergência com recuperação/restauração e resgates nativos com/sem cama |
| 3 | Progressão, pesquisa, produção e armazenamento | PASS: sete grupos, incluindo fabricação nativa de capacete, substituição de parede por pedra, prateleira construída e salvar/carregar. Progressão prolongada ainda na etapa 7 |
| 4 | Prisão: construção, captura, alimentação, tratamento, conversão, recrutamento, liberação, controle manual e salvar/carregar | PASS parcial: captura, tratamento, conversão, recrutamento, liberação, salvar/carregar e desligamento. Ensaio integrado atingiu três vagas construídas; cenários adicionais de segurança/manual pendentes |
| 5 | Comércio: produção, interação local/orbital, orçamento, entrega, caravana, controles e persistência | PASS parcial: comércio local e orbital, entrega real, reserva, itens protegidos, salvar/carregar e desligamento. PASS: caravana nativa com viagem, compra, retorno e descarga. Produção econômica prolongada e cenários de interrupção/manual pendentes |
| 6 | Combate: cenários variados, colaboração melee/ranged, retirada e resgate | Falhas identificadas; revisão em validação. Resultados individuais abaixo |
| 7 | Autonomia integrada: construção completa, inverno e sobrevivência prolongada | FAIL aos trinta dias: cinco sobreviventes, mas conjunto militar incompleto e hospital separado do núcleo. Save final preservado. Correções de pesquisa/expansão passaram em controles; nova partida prolongada pendente |
| 8 | HUD, relatório final, saves para revisão, instalação e Git | PASS dos 12 controles reais, rolagem e estado gravado; exportação de base integrada aprovada e relatório completo pendentes |

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

Revisão de combate preparada após a derrota contra mechanoides: `Overwhelmed` passa a considerar ordens ativas, excluindo companheiros já recuando ou gravemente feridos da força que sustenta uma posição. O próprio colono avaliado permanece na estimativa para poder se recuperar quando a ameaça restante for manejável. Controle nativo acrescentado para três defensores contra três inimigos e retirada de dois apoiadores. Compila sem erros e as 57 políticas passaram; ainda não instalado no processo integrado vivo nem aprovado por combate nativo. Após o ensaio integrado, instalar e repetir controles, confronto contra mechanoides e resgate/emergência.

### Resultado integrado aos trinta dias

`integrated-log-resume-20261009-103502-937` encerrou com falha funcional no prazo nativo de trinta dias, preservando `Saves/ModularFailure.rws` e `ModularCheckpoint.rws`. Os cinco colonos originais sobreviveram; hospital, pesquisa, oficina, estoque e refrigeração avançaram. O conjunto militar completo não foi fabricado/equipado: LongBlades ainda estava em pesquisa e PlateArmor pendente. Não estender o prazo para transformar essa reprovação em aprovação. Uma retomada posterior poderá validar uma correção, preservando relógio e evidência anteriores.

Contadores nativos finais: 88 animais abatidos, 349680 ticks de caça e 314 refeições produzidas. Receitas observadas incluem abate, blocos de pedra, refeições simples individuais/em lote, Fine Meal, arco, camisa e duster. Não houve prova de produção comercial de baseados. Sobrevivência por vinte dias está demonstrada; exportação integrada aprovada, inverno completo e progressão militar permanecem pendentes.

Correções adicionais preparadas: exposição melee inclui até três segundos no destino de ataque sob fogo de outros atiradores; observador separado acompanha resgate, tratamento e recuperação nativos depois da vitória, com quatro participantes originais, hospital inicial controlado e checkpoint persistido. Nenhuma cura é aplicada pelo observador. Compilação passou; validação nativa ainda em andamento.

### Rodadas posteriores e próximo checkpoint

`combat-support-controls-20261009-113536-577`: controles 1 e 2 passaram; controle 3 reprovado, com os três combatentes derrubados antes da retirada segura. Save `AutonomousRim-Combate-Falha.rws` preservado. Asserções de apoio ativo e alcance do abrigo passaram no setup; isso não aprova a execução da retirada.

`combat-varied-revision-5-20261009-113700-367`: retirada em 2345 ticks contra cinco mechanoides, dois neutralizados, zero mortos/derrubados e três feridos. Sangramento permaneceu em três participantes. A execução melhorou em relação à derrota registrada anteriormente, mas geração nativa de colonos/dano não usa somente a semente do seletor de equipamentos; não é uma comparação determinística nem prova de recuperação.

`combat-postcare-4-20261009-113828-434`: confronto difícil contra seis manhunters ficou sem desfecho em 6000 ticks; um colono derrubado, cinco inimigos ativos, cuidado pós-vitória não verificado. Manifesto `INCONCLUSIVE`. Encontrado também erro no observador: criação da porta do hospital substituía a referência da porta do abrigo usada na classificação de retirada. Corrigido preservando a referência original; não reclassificar retrospectivamente o resultado anterior.

Pesquisa: save final mostra LongBlades com 215,48 pontos, MicroelectronicsBasics com 3000 e Machining com 1000. Construções planejadas tinham score 97 enquanto defesa inicial tinha 78. Correção preparada eleva somente o primeiro checkpoint militar para 98, mantendo eletricidade e refrigeração urgente antes dele. Controle nativo novo disputa a rota com um console comercial planejado e verifica preservação da escolha manual/ausência de pesquisa grátis. Partida longa após correção ainda pendente.

`strategy-defense-contention-20261009-114007-700` passou na disputa de pesquisa, DAG, checkpoints, preservação manual e persistência nativa. Compilação limpa e 57 verificações de política passaram.

Checagem da geometria no save final de trinta dias reprovou conectividade: hospital `mod:0:-2:3`, sem módulo `0:-1`. Paredes compartilhadas, dimensões e planos de pisos passaram antes dessa asserção. O alocador permitia distância Manhattan de até dois módulos; correção preparada exige um vizinho direto para novos módulos de construção/cultivo. Não move nem destrói o hospital já construído. Controle novo bloqueia quatro vizinhos, exige espera em vez de salto e depois libera um vizinho para planejar o hospital.

`combat-postcare-balanced-4-20261009-114043-534`: vitória provisória em 1584 ticks contra quatro manhunters, zero mortos, um derrubado, dois feridos. Resgate de Holt e tratamento de Joodle iniciaram por jobs nativos. Observador reprovou ao reaparecer ameaça: um inimigo original derrubado recuperou movimento. `CombatCareFailure.rws` preservado. Correção do observador admite reentrada dos inimigos originais, conserva o prazo original de oito dias e só aprova recuperação com nenhuma ameaça ativa; continua rejeitando participantes novos. Retomar do save da falha permite validar persistência sem substituir colonos ou apagar ferimentos.

`strategy-compact-regression-20261009-114256-920` passou em expansão compacta, disputa de pesquisa e demais contratos estratégicos, incluindo salvar/carregar. Hospital/cultivo novos ficam limitados a módulos adjacentes; isso não corrige retroativamente o layout da partida final anterior.

Retomada clínica em execução: `combat-care-resume-4-20261009-114351-458`, de `CombatCareFailure.rws`. Quatro referências originais e resultado do confronto restaurados; contagem não reiniciada. Após reaparecer um manhunter, emergência voltou a Danger e depois a Recovery. Até 6,22 horas de recuperação, sete tratamentos nativos e ocupação de cama médica observados, sem ameaça ativa; recuperação final ainda pendente. Próxima retomada: mesmas flags de cuidado + `autonomousrimcombatresume`, usando o `Saves/CombatCareCheckpoint.rws` desse perfil.

Retirada: controle falho começou com nove inimigos melee a aproximadamente vinte células. O limite fixo de dezesseis células só reconhecia inferioridade quando a carga já se aproximava da porta. Correção adicional preparada inclui até cinco segundos de aproximação melee (mínimo 16, máximo 26 células), mantendo força de apoio ativa. Compilação e 57 políticas passaram; ainda não instalada/validada nativamente no processo clínico atual. Repetir controles de combate após seu término.

Interrupção clínica intencional para regressões registrada como `INTERRUPTED_FOR_REGRESSION`, mantendo erro original do executor. Checkpoint imutável `combat-care-resume-4-20261009-114351-458/Saves/BeforeCombatControls.rws`, SHA256 `51672C3E5084105063A85487C703288AB482CDBC7A4FDFC4B8B71329328A7439`. Retomar com as mesmas flags de cuidado/retomada; não reiniciar participantes, ferimentos ou prazo nativo.

`combat-early-withdrawal-controls-20261009-114926-406` passou nos três controles: vitória favorável, vitória com cobertura e retirada para abrigo com porta fechada diante de nove inimigos. Zero mortos/derrubados nos três; dois feridos por cenário. Isso valida a retirada neste controle, sem apagar a reprovação anterior nem assegurar todos os combates variados. Resgate/emergência após essa revisão em verificação.

Regressões após retirada antecipada: `medical-early-withdrawal-regression-20261009-115050-504` passou nos sete controles, incluindo resgate hospitalar, estabilização no chão antes de morte por sangramento (1499 ticks), liberação do médico e preservação manual. `emergency-early-withdrawal-regression-20261009-115202-003` passou em detecção, evacuação/resgate, rejeição de rota exposta, reentrada em perigo, recuperação clínica e restauração de controles.

Retomada clínica mais recente: `combat-care-final-resume-4`, iniciada do checkpoint imutável `BeforeCombatControls.rws` com a revisão instalada. O diretório completo contém timestamp e manifesto. Recuperação final ainda precisa do marcador `[FiveCombatTrials] CARE PASS:`; mera conclusão do combate ou ocupação de cama não concede aprovação.

Próxima ampliação de prisão preparada: flag `autonomousrimprisonsafetytest`, marcador `[PrisonTests] DONE`. Além de captura/conversão/recrutamento/liberação nativos, verifica durante o setup controlado: helpers drafted preservados, ameaça imediata suspende captura, ferido próprio tem precedência, ausência de cama válida bloqueia captura e reserva simulada menor que dois dias bloqueia novos presos. Compilação passou; execução nativa desse novo flag pendente. Ferimentos artificiais desse controle são removidos ainda no setup; depois do início da captura, tratamento continua inteiramente nativo.

`combat-care-final-resume-4-20261009-115313-231` concluiu com `careResult=PASS`: quatro combatentes originais vivos, sete tratamentos nativos, cama médica observada, nenhum sangramento/necessidade de repouso médico, saúde >=90%, colonos undrafted e emergência Normal. Recuperação levou aproximadamente 64 horas de jogo desde a vitória inicial; inclui inimigo original voltando a atacar e duas retomadas sem reiniciar participantes/prazo. `Saves/CombatCareComplete.rws` preservado. Joodle perdeu partes corporais no combate; o PASS não promete regeneração de partes perdidas nem ausência de sequelas. Não estender essa aprovação ao cenário difícil de seis manhunters ou ao pós-retirada contra mechanoides.

`prison-safety-regression-20261009-115804-490` completou cinco controles adicionais e os seis grupos de captura/tratamento/conversão/recrutamento/liberação/persistência. Entretanto o setup emitiu erro nativo ao atribuir `ForPrisoners=false`; manifesto reclassificado `INVALID_FIXTURE`, preservando `originalResult=PASS` e motivo. Corrigido para `ForOwnerType=BedOwnerType.Colonist`, conforme API nativa 1.6. Executor agora rejeita também essa mensagem de erro. Nova rodada `prison-owner-api-regression` em execução, usando fixture corrigido. Não considerar a rodada com erro uma aprovação limpa.

Ordem de continuidade: coletar resultado de `prison-owner-api-regression`; depois HUD visível/controles, interrupções manuais de comércio e prisão, combates humanos variados ainda pendentes, nova partida integrada com pesquisa/expansão corrigidas e cobertura sazonal. Preservar a falha integrada aos trinta dias. Exportação aprovada depende de uma nova execução que cumpra os marcos, seguida de carregamento sem observador.

### Fuga e recaptura

`prison-owner-api-regression-20261009-120247-775` passou nos controles de segurança, captura, tratamento e conversão sem o erro de propriedade das camas. Entretanto o candidato Ñore iniciou uma fuga nativa antes de ser recrutado: `Saves/AutonomousRim_Raid_35160.rws` contém `LordJob_PrisonBreak`, `lastPrisonBreakTicks=35153`, resistência zero e humor aproximadamente 0,616. O log depois registra zero presos e ausência de job do candidato; cenário encerrado intencionalmente, manifesto `FUNCTIONAL_FAILURE_ESCAPE`, mantendo erro do executor e motivo. Não é aprovação integral de prisão. A fixture usa ajudantes desarmados e combate desligado, portanto não demonstra contenção de rebelião por defensores equipados.

Inspeção revelou falha independente na recaptura: qualquer registro prévio do mesmo Pawn, mesmo automático e concluído após fuga/liberação, impedia eternamente uma nova captura. Correção permite substituir apenas um registro automático concluído depois de emitir uma nova ordem de captura válida; registros manuais e ordens ativas continuam protegidos. Destino é reavaliado com necessidades atuais. Compilação e 57 políticas passaram.

Rodada `prison-recapture-regression` em execução: seis guardas de segurança, incluindo registro manual concluído, e candidato com registro automático concluído controlado. Exige nova captura nativa do mesmo Pawn sem duplicação, depois tratamento/conversão/recrutamento/liberação/persistência. O observador agora salva `PrisonFailure.rws` e reprova imediatamente se o candidato sair do mapa antes de ser recrutado; não aguarda um timeout quando o objetivo já ficou impossível. Contenção física de fuga e interação manual de um preso vivo ainda pendentes.

`prison-recapture-regression-20261009-120852-641` executou a captura nativa do candidato com registro automático concluído, sem duplicação, e passou em tratamento/conversão. Porém o novo observador interpretou `Spawned=false` como fuga quando um cuidador carregava o candidato entre camas. `PrisonFailure.rws` contém `Human38010` no `carryTracker/innerContainer/innerList`, ainda prisioneiro. Manifesto `INVALID_OBSERVER`, erro original preservado. Corrigido para `MapHeld==map`, que inclui Pawn carregado; nova rodada `prison-carried-recapture-regression` em execução. A fuga de Ñore na rodada anterior continua uma falha real, comprovada por LordJob_PrisonBreak e saída do mapa.

`prison-carried-recapture-regression-20261009-121107-319` passou sem erro nativo: seis guardas de segurança, captura do candidato com registro automático concluído sem duplicação (assertiva Single no registro), tratamento, conversão, recrutamento/liberação, persistência e desligamento. A recaptura usa estado prévio controlado; não representa derrota/recaptura de uma rebelião real. Ensaio anterior de fuga permanece reprovado.

Auditoria de HUD preparada com `HudInteractionChecks` e `FunctionalStage.ps1 -Visible`: observador pausa a colônia isolada e só registra estados; não clica nem aciona controles para produzir aprovação. Computer Use deve operar os doze controles reais, verificar transições visuais, rolagem e save final. Mudanças acopladas entre combate/emergência são registradas, mas não substituem seus cliques individuais. Screenshot/save nativos apenas depois de todos os estados terem ligado e desligado. Texto da HUD corrigido para distinguir plano modular atual do legado em anel. Compilação/sintaxe passaram; auditoria visual em execução.

## Auditoria visual da HUD e correção da rolagem

- `hud-visible-audit-20261009-121523-406`: dez controles clicados realmente em ON/OFF. FAIL funcional da rolagem: prisão e expedições ficavam fora da área visível. `Verse.Listing.NewColumnIfNeeded` distribui linhas em colunas quando `maxOneColumn` é falso; a altura calculada também encolhia. Corrigido com uma única coluna no painel rolável.
- `hud-scroll-regression-20261009-122408-508`: interrupção excedeu prazo da sessão; nenhum controle auditado nesta repetição. Preservado como FAIL por timeout, sem conclusão funcional.
- `hud-scroll-final-20261009-163051-899`: PASS. Computer Use clicou individualmente cada um dos 12 controles em ON e OFF. Observador nativo registrou 24 transições, sem gerar cliques; rolagem expôs prisão e expedições. Meta populacional mudou 8→9→8. Prévia, reavaliação e análise de risco foram acionadas sem exceções; isso sozinho não comprova toda a lógica desses sistemas.
- `Saves/HudAuditComplete.rws` verificado diretamente: todas as 12 opções desligadas, conforme campos/defaults do Scribe; nenhuma Blueprint/Frame pendente. `HudAuditComplete.png` preservado. Texto de planta atualizado para núcleos modulares, mantendo descrição separada para saves antigos em anel.
- HUD usa partida pausada para isolar os cliques. Ensaios de gameplay continuam em 3×. Esta aprovação não representa construção, comércio ou combate autônomo completo.
- Próxima retomada: nova partida integrada com correções atuais, mantendo prazo nativo de trinta dias. Depois: rebelião com defensores equipados; variações humanas de combate; fabricação comercial orgânica/interrupções; inverno real; revisão e exportação da base aprovada.

### Partida integrada após correções — em execução

`integrated-compact-defense-20261009-163653-326`, revisão `90672c6`, velocidade 3×, dificuldade nativa Peaceful. Cinco colonos com skills 20; recursos, pesquisa, construção, necessidades e fabricação nativos. Primeiro checkpoint confirmou sete paredes construídas após 4.800 ticks (1,92 horas), pesquisa Smithing selecionada e ausência de falhas nativas até esse ponto. Ainda não é aprovação do ensaio longo. Manter o prazo de trinta dias e inspecionar conectividade da planta final.

Retomada: primeiro verificar processo/PID 28724 e sessão do executor 20407. Não iniciar outro jogo enquanto este estiver ativo. Saves `ModularStart.rws` e `ModularCheckpoint.rws` estão no perfil acima. Se o executor terminar por tempo real, usar o checkpoint preservando o início e o prazo nativos; se terminar por falha funcional, investigar antes de repetir.

### Bloqueio de quarto excedente e reserva de pedra — 09/10/2026

A partida `integrated-compact-defense-20261009-163653-326` foi interrompida após cerca de 41,8 horas nativas: cinco quartos concluídos para cinco colonos, 100 paredes e 245/1586 tarefas, mas nenhum projeto ativo/blueprint pendente. Um sexto quarto aguardava calcário indisponível e bloqueava cozinha, abate, estoque e pesquisa. Manifesto reclassificado `FUNCTIONAL_FAILURE_BEDROOM_DEPENDENCY`, preservando erro original do executor. Não é aprovação da partida de vinte/trinta dias.

Checkpoint imutável: `Saves/BeforeBedroomDependencyFix.rws`, SHA256 `E8C6A39D8308CF197586555CAD0BEF02B4E92EB8E5E7A963CE536E568E1DCEDD`. A retomada deve preservar colonos, recursos, início e prazo original; saves do usuário permanecem intocados.

Correções: quartos concluídos contam primeiro para a capacidade atual; quarto excedente não bloqueia serviços. Cozinha/abate/estoque podem avançar enquanto o abrigo é construído. Reserva de blocos desconta blueprints/frames nativos e cada parede futura compartilhada uma única vez; material indisponível de plano automático ainda não emitido volta a madeira. Planos emitidos, cancelados pelo jogador e blueprints existentes são preservados.

`construction-dependency-regression-20261009-165123-412`: `INVALID_FIXTURE`; blueprint manual sobrepôs célula de plano futuro e a assertiva contou uma parede corretamente excluída do orçamento. Corrigida a separação das células. `construction-dependency-regression-v2-20261009-165205-838`: quatro PASS nativos em contratos controlados de reserva/repetição, desconto nativo/preservação manual, fallback e dependências. Flags de cômodos concluídos desta fixture são lógicas; não provam construção física. 57 políticas passaram novamente. Regressão de progressão e retomada física ainda necessárias.
`progression-stone-budget-regression-20261009-165244-918`: sete PASS, incluindo capacete de aço fabricado por job nativo, substituição de parede por pedra, estante construída/filtros/capacidade e persistência após save/load. Não representa progressão completa da colônia integrada.

`integrated-bedroom-budget-resume-20261009-165352-153` parou no validador antes da continuação: uma lâmpada sem parede ainda construída foi rejeitada, apesar do apoio existir no plano. Manifesto `VALIDATION_FAILURE_PLANNED_SUPPORT`, erro original preservado. API nativa `Placeworker_AttachedToWall` confirma a mensagem `MustPlaceOnWall`; validador aceita somente essa razão quando há parede planejada não cancelada na célula de apoio conforme rotação. Não ignora obstruções, terreno, colisões nem outros motivos. Emissão de blueprint continua condicionada à API nativa e à construção da casca/piso.

`construction-lamp-dependency-regression-20261009-165617-959`: cinco PASS, acrescentando apoio planejado válido e rejeição de apoio ausente/cancelado. Builds limpos. Retomar novamente do checkpoint imutável original, sem considerar nenhuma das duas tentativas como aprovação da partida integrada.

Retomada `integrated-support-budget-resume-20261009-165701-963`, revisão `4361164`: carregamento/validação passaram. Às 42,78 horas originais, voltou de zero para nove projetos ativos, 36 blueprints/frames pendentes, 16 marcações de coleta e job nativo `FinishFrame` de Mitsuya. 246/1771 tarefas (plano expandiu); ainda 100 paredes. Isso prova desbloqueio/emissão e trabalho iniciado, não conclusão física dos serviços nem aprovação dos vinte dias. Continuação em 3× mantém cinco originais e prazo nativo.

Próxima retomada: verificar sessão do executor `45890` e processo próprio `28040`, perfil acima, antes de iniciar qualquer outro jogo. Sucesso integral exige marcador `[AutonomousRim.ModularTrial] PASS: integrated twenty days`; nova falha deve ser investigada. Se ocorrer apenas timeout real, retomar `Saves/ModularCheckpoint.rws` preservando início/prazo. Nenhuma exportação aprovada até os marcos nativos serem alcançados.

Continuação integrada após desbloqueio: 53,74 horas → 152 paredes/415 tarefas; 74,65 horas → 219 paredes/601 tarefas, cinco originais vivos e trabalho nativo ativo. Cozinha 50/51 tarefas, fogão aguardando aço; reserva caiu a 1,1 dia. Aos 75,65–78,64h houve job nativo Mine; acompanhar entrega do minério/fogão/comida, sem declarar sobrevivência ou abastecimento aprovados antecipadamente.

Próxima fixture de prisão preparada e compilada, ainda NÃO EXECUTADA: `autonomousrimprisonbreaktest`, marcador `[PrisonBreakTests] DONE`. Cinco helpers saudáveis skills20, quatro defensores com dois espadachins/dois atiradores e colete/capacete; quinto desarmado para trabalho médico. Mantém cenário controlado de captura/tratamento antes de iniciar `PrisonBreakUtility.StartPrisonBreak` nativo. Requer detecção ativa, resposta de draft observada, rebelde derrubado vivo, retorno nativo à cama, tratamento sem sangramento e mapa seguro; morto/fugitivo/timeout reprovam. Checkpoints `BeforeNativePrisonBreak` e `PrisonBreakContained`; a manipulação do estado de interação manual é apenas setup para evitar recrutamento/liberação antes do confronto, não prova de controle do jogador. Equipamentos/instalações/ferimentos iniciais são fixtures explícitas; nada é fornecido/curado depois do início do confronto. Executar somente após a sessão integrada liberar o jogo. A DLL observadora desta fixture ainda não substituiu a DLL carregada no processo integrado atual.

### Obstáculo no ponto de trabalho do fogão — investigação em andamento

`integrated-support-budget-resume-20261009-165701-963` avançou até 105,53 horas, 238 paredes e 849/1821 tarefas, seis animais caçados/processados e reserva 1,4 dia. Entretanto o fogão permaneceu sem blueprint: `Interaction spot is blocked by granite chunk`. Não houve refeições cozidas. Manifesto `FUNCTIONAL_FAILURE_INTERACTION_OBSTACLE`, erro do executor preservado. Checkpoint imutável `Saves/BeforeInteractionClearanceFix.rws`, SHA256 `8242184D92E8704AE802EEABD41DF41DB94EA66CB0D619B0DDC0122E122D41BA`; nenhum recurso/necessidade deve ser modificado na retomada integrada.

Correção em investigação: despachante procura itens transportáveis não atravessáveis no ponto de interação de móveis do estágio atual e usa HaulAside nativo, com destino fora de móveis/pontos de trabalho planejados, áreas proibidas, lavouras, fogo e mineração. Usa ordens rastreadas, sem teleportar/destruir; preserva proibições, draft/ordens manuais, saúde/descanso e trabalhos prioritários. Ainda NÃO APROVADA.

Fixture `InteractionClearanceChecks`, flag `autonomousriminteractionclearancetest`: recursos e terreno/colonos iniciais controlados; após setup exige o mesmo granito intacto transportado por job nativo e fogão concluído com entrega/construção reais. Guardas de draft e item proibido antecedem execução. Granito não tem CompForbiddable, portanto guarda de proibição usa cadáver humano nativo explicitamente criado/removido apenas no setup.

Rodadas iniciais preservadas: v1 coleção de colonos mudou durante draft (INVALID_FIXTURE); v2/v3 tentaram proibir granito sem componente (INVALID_FIXTURE). v4 não tinha disponibilidade assegurada, terminou sem transporte e colonos com fome; prova de montagem insuficiente. v5/v6 reprovaram corretamente a nova pré-condição de transportador elegível. v7 usa cinco colonos novos saudáveis, habilidades20, Anything e necessidades inicialmente atendidas; pré-condição/guardas passaram, mas nenhum job de limpeza surgiu em doze horas, apesar de vaguearem. Falha funcional mantida; diagnóstico detalhado de CanDispatch/CanAct/work/necessidades/timetable em nova rodada. Não relaxar proteções para fabricar aprovação. Nenhuma retomada integrada foi feita com esta correção parcial.

Diagnóstico confirmado pela DLL nativa 1.6: `Verse.JobDef.joyGainRate` tem valor padrão **1f**, inclusive em GotoWander/Wait_Wander. `CanDispatch` usava esse número isoladamente para identificar recreação, recusando todos esses jobs ociosos. Corrigido para `joyKind != null` e proteção explícita de Meditate; controles de ordens, draft, comida, descanso, saúde, trabalho prioritário e construção em andamento continuam ativos. O diagnóstico `interaction-clearance-diagnosis-20261009-172156-549` preserva a reprovação antes da correção (CanAct/CanWork/initialized verdadeiros, comida Fed, Anything, porém CanDispatch falso).

`interaction-idle-dispatch-regression-20261009-172451-526`: PASS completo. Cinco colonos saudáveis skills20, necessidades atendidas e Anything apenas no setup descartável; obras pendentes externas removidas apenas para isolamento inicial. Depois do setup: granito original movido intacto por HaulToCell nativo, fora dos pontos planejados; blueprint emitido, recursos entregues e FueledStove concluído por trabalho normal. Draft e cadáver proibido preservados nos controles anteriores. `Saves/InteractionClearanceComplete.rws` registra a conclusão. Esses recursos/alterações de setup não são aplicados à colônia integrada. 57 políticas passaram. Regressão de prioridades/agenda e retomada integrada ainda necessárias.
`work-schedule-idle-dispatch-regression-20261009-172542-296`: 14 PASS após a correção, incluindo proteção do cozinheiro, saúde/doctor, fome/sono/recreação, meditação, emergência/recuperação, overrides manuais, persistência e sono nativo em cama.

`integrated-native-clearance-resume-20261009-172652-697` não iniciou continuação: validador rejeitou o granito antes do despachante poder retirá-lo. Manifesto `VALIDATION_FAILURE_CLEARABLE_INTERACTION`, checkpoint intacto. Validação agora tolera somente falha nativa do ponto de interação cujo conjunto de obstáculos é composto integralmente por itens transportáveis e não proibidos. Mantém rejeição de terreno, colisões, paredes/estruturas sólidas e outros motivos; emissão continua usando CanPlaceBlueprintAt sem bypass, portanto depende do transporte real.

`interaction-clearance-plan-regression-20261009-172833-551` tinha assertiva incorreta: cadáver humano é Standable e não bloqueia esse ponto nativo. Não classificar sua aceitação pelo validador como falha funcional. A guarda do cadáver anterior só demonstra que ele não foi desproibido, não transporte de obstáculo proibido bloqueante. Fixture substitui assertiva de rejeição física por parede sólida real; granito movido e fogão construído continuam exigidos. Rodada `interaction-clearance-plan-v2` em execução. Fora deste controle, compatibilidade com item de mod que simultaneamente bloqueie e aceite proibição ainda não está comprovada.
`interaction-clearance-plan-v2-20261009-173001-330` PASS: granito removido intacto e fogão construído nativamente; plano aceita item removível e rejeita parede sólida. `construction-plan-clearance-regression-20261009-173055-396` cinco PASS: reservas sem reutilização, preservação nativa/manual, fallback, quartos/serviços e apoio de lâmpadas. Retomada integrada mantém checkpoint original e prazo.

`integrated-interaction-plan-resume-20261009-173158-126`: nova falha real de planejamento detectada no carregamento — WallLamp em (180,122) tinha porta como apoio, rejeitada pela API nativa `CannotSupportAttachment`. Manifesto `FUNCTIONAL_FAILURE_LAMP_SUPPORT`, checkpoint preservado. Planejador de interiores agora escolhe apoio sólido que aceite anexos, ou parede não cancelada prevista; portas/estruturas que não aceitam anexo não servem de apoio. Migração reposiciona somente lâmpadas ainda não emitidas/concluídas/canceladas pelo jogador. Fixture lógica de interiores recebeu paredes previstas explícitas, pois apenas borda geométrica não prova apoio.

`lamp-door-support-regression-20261009-173451-347`: seis PASS, incluindo lâmpada não emitida com porta nativa reposicionada para parede prevista e preservação de plano cancelado. Apoio ausente/cancelado, orçamento de pedra, reserva de blueprints e dependências dos serviços continuam aprovados. Compilação sem avisos/erros. Retomar novamente do checkpoint original; ainda não há PASS integrado nem save final aprovado para exportação.
