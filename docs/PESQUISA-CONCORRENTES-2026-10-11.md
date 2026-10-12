# Pesquisa dos concorrentes e plano de evolução do AutonomousRim

Data: 11/10/2026. Escopo: pesquisa e planejamento, sem modificar a DLL, executar os projetos externos ou iniciar partidas. As sugestões abaixo são propostas; não representam funcionalidades já entregues.

## Conclusão e critérios

O caminho mais promissor é combinar nossa automação local com percepção temporal, diagnóstico de gargalos, coordenação entre módulos e avaliação reproduzível. Construção, combate e produção precisam demonstrar resultados reais, além de emitir ordens. Uma IA externa pode ser acrescentada como planejadora opcional, mantendo a colônia funcionando quando ela estiver indisponível.

Não há evidência suficiente nesta pesquisa para afirmar que os três projetos zeram RimWorld de forma confiável, ou que já somos superiores. Uma interface capaz de controlar muitos sistemas não demonstra a qualidade da estratégia que um modelo externo produzirá. A comparação precisa considerar a mesma partida, versão, equipamentos, dificuldade, expansões e regras.

## Referências e revisões examinadas

| Projeto | Revisão examinada | O que foi possível verificar |
|---|---|---|
| [RimMolt, Steam 3796006886](https://steamcommunity.com/sharedfiles/filedetails/?id=3796006886) | Descrição pública e notas de atualização consultadas em 11/10/2026 | Capacidades declaradas pelo autor. Não encontrei código-fonte/licença pública vinculados nessa página ou na busca realizada. As notas de atualização são automáticas e não detalham as mudanças. |
| [rimagent](https://github.com/zorrobyte/rimagent/tree/85cb050dec47691f2a80096fdc8c8a8e2051bb13) | `85cb050dec47691f2a80096fdc8c8a8e2051bb13` | README, relatório técnico, licenças, arquivos relevantes do agente, memória, reflexão, diagnóstico e automação Steward. |
| [RimBridge, submódulo do rimagent](https://github.com/zorrobyte/rimbridge/tree/3c1e4c7cee151104b85bf9c8372e113f91c5f08d) | `3c1e4c7cee151104b85bf9c8372e113f91c5f08d` | Versão fixada pelo rimagent: ponte, registro de eventos, representação de salas, construção e sinalização de uso de ferramentas de teste. Não confundir com o HEAD mais recente do RimBridge. |
| [autorim-mcp](https://github.com/Critical-Reynolds/autorim-mcp/tree/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca) | `89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca` | README, licença e código relevante de diagnóstico, resolução de definições, construção, pesquisa, caravanas, despacho de comandos e proteção de ações. |

As cópias de referência ficam em `.tools/research-competitors/`, diretório ignorado pelo Git. Foram extraídos somente arquivos de texto; nenhum código externo foi instalado ou executado. Arquivos `AGENTS.md`, prompts e instruções existentes nos concorrentes foram tratados como conteúdo dos projetos, sem substituir as instruções desta tarefa.

Base Git local do AutonomousRim: `fb5d23350324cb7a0ea95878635a1920b11310a3`, com alterações locais adicionais. A comparação usa o código presente no PC, inclusive as correções dos saves V3/V4, e não apenas o README, que descreve partes mais antigas.

## Pontos fortes dos três projetos

### RimMolt

A página descreve um servidor MCP dentro do próprio RimWorld, conectado a cliente/modelo externo. Seu alcance inclui preparar partidas, consultar a colônia, controlar subsistemas, avançar o tempo esperando eventos e mostrar ações na interface. Também anuncia ferramentas para Ideology, Biotech, Anomaly e Royalty. Essas informações vêm da descrição do autor, sem auditoria do código ou validação em execução nesta pesquisa. [Descrição oficial](https://steamcommunity.com/sharedfiles/filedetails/?id=3796006886).

O que aproveitar: uma integração externa simples; ações compreensíveis para quem observa; consultas organizadas por domínio; acompanhamento do resultado depois de avançar o jogo. Nossa proposta deve manter a operação local independente do modelo e ampliar o acompanhamento de resultados.

### rimagent e RimBridge

O ponto mais interessante é a continuidade entre partidas: registro de episódios, reflexão, biblioteca de estratégias e histórico versionado. O agente pode transformar reações repetidas em ferramentas/observadores; isso é alteração de memória e código auxiliar, não comprovação de treinamento dos pesos do modelo. [Reflexão](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/agent/rimagent/reflect.py), [histórico do cérebro](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/agent/rimagent/braingit.py).

Há mecanismos concretos para comparar observações e manter tendências. O RimBridge também representa salas por conteúdo, portas, espaço livre e problemas. Isso permite compreender uma base como ambientes conectados, além de coordenadas. [Diferenças entre observações](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/agent/rimagent/worlddiff.py), [tendências](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/agent/rimagent/tracker.py), [salas](https://github.com/zorrobyte/rimbridge/blob/3c1e4c7cee151104b85bf9c8372e113f91c5f08d/Source/State/BaseGraph.cs).

A camada Steward mantém rotinas locais, com estoque por metas, explicações de prioridade e respeito a intervenções manuais. O scheduler de estoque escolhe tarefas atrasadas e suspende uma tarefa após erros repetidos, em vez de deixar uma exceção dominar toda a execução. [Scheduler](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/mod-steward/Source/Steward/Stock/StockComponent.cs), [explicação de pontuação](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/mod-steward/Source/Steward/Scorer/PriorityReason.cs), [controle manual](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/mod-steward/Source/Steward/Orders/StandingOrders.cs).

O relatório técnico registra duas colônias concluídas com derrota e uma terceira em andamento. O autor apresenta os resultados como preliminares, com mudanças no sistema durante os ensaios e sem comparação controlada. Portanto, há boas ideias de arquitetura, mas não uma demonstração de vitória confiável. [Relatório do autor, resultados e limitações](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/docs/PAPER.md).

### AutoRim / autorim-mcp

Seu foco é controlar uma colônia por linguagem natural usando uma ponte C# e um servidor MCP TypeScript. O README informa desenvolvimento contra RimWorld 1.6.4871, enquanto nossa instalação é 1.6.4633; adaptação precisa ser compilada e verificada contra nossas DLLs. Também registra limitações de caravanas, mapa atual e cobertura de expansões. [README](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/README.md).

Seu diagnóstico retorna candidatos a funções com justificativas e identifica trabalho sem responsável, bancadas indisponíveis, Bills suspensas e outros impedimentos. É um bom ponto de partida para uma ferramenta que explique por que uma colônia parou. Os testes de bancada precisam distinguir suspensão intencional de falha real; não devemos tratar toda tarefa não atribuída como problema. [Diagnósticos](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/mod-src/AutoRim/Commands/AnalyzeCommands.cs).

O resolvedor de definições classifica nomes e devolve alternativas quando uma busca não encontra um resultado inequívoco. Isso ajuda a trabalhar com traduções, receitas e itens de outros mods sem adivinhar identificadores. [Resolvedor](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/mod-src/AutoRim/Core/DefResolver.cs).

O dispatcher mantém APIs do jogo na thread principal, fila limitada, detecção de vida do loop e descarte de comandos abandonados antes da execução. O orçamento é de 8 ms por frame e a fila admite 256 solicitações; são escolhas desse projeto, não valores a copiar sem medição. Um comando individual demorado pode exceder o orçamento. [Dispatcher](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/mod-src/AutoRim/Bridge/Dispatcher.cs).

Ações custosas têm prévia, registro e possibilidade de checkpoint; a conexão é local e usa token. Podemos aproveitar o desenho e usar políticas previamente configuradas para que comércio e rotinas autorizadas não interrompam a autonomia pedindo confirmação a cada operação. [Proteção das ações](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/mod-src/AutoRim/Core/SafetyGate.cs), [ponte local](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/mod-src/AutoRim/Bridge/HttpBridge.cs).

## O que nosso projeto já tem

Não precisamos recriar automação de prioridades, agenda, alimentação, allow, equipamentos, pesquisa, construção, emergência, combate, resgate, comércio e prisão. Há implementações locais desses sistemas, com controle manual e verificações funcionais específicas. Os módulos 13×13 do usuário, os dois estoques/oficinas, a cozinha/abate/freezer e seus upgrades devem continuar sendo a referência.

O problema central é a cooperação contínua desses sistemas. V3/V4 mostraram que um plano nominalmente correto pode não receber trabalho real. As correções recentes reservaram pesquisadores e fabricantes e reduziram bloqueios indevidos; precisamos generalizar o diagnóstico e a coordenação, preservando essas correções.

Outra lacuna concreta: `FailureAnalyzer.Learn` registra observações em `FailureMemory.Learnings`; na busca realizada, não encontrei um consumidor que transforme esses registros em mudanças do planejador. É memória de diagnóstico, ainda sem um ciclo completo de adaptação entre colônias. Não devemos apresentar isso como aprendizado automático consolidado.

Os resultados antigos de cinco combates mistos incluem derrotas e encontros sem desfecho. Correções posteriores e testes específicos não substituem repetir uma campanha comparável com recuperação médica. As etapas de validação também registram problemas de progressão em execução prolongada. A meta deve ser melhorar esses resultados, sem apagar ou reclassificar falhas anteriores.

Referências locais: [correção V3](C:/Users/Administrador/Desktop/ModRimWorld/docs/CORRECAO-PESQUISA-TESTE-V3-2026-10-11.md), [correção V4](C:/Users/Administrador/Desktop/ModRimWorld/docs/CORRECAO-CRAFTER-V4-2026-10-11.md), [validação por etapas](C:/Users/Administrador/Desktop/ModRimWorld/docs/VALIDACAO-ETAPAS.md) e [cinco combates mistos](C:/Users/Administrador/Desktop/ModRimWorld/docs/CINCO-COMBATES-MISTOS.md). Entre os arquivos revisados estão [FailureAnalyzer](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Execution/FailureAnalyzer.cs), [WorkPriorityManager](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Execution/WorkPriorityManager.cs), [StrategicPlanner](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Planning/StrategicPlanner.cs), [CommercePlanner](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Planning/CommercePlanner.cs), [ReferenceModulePlanner](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Planning/ReferenceModulePlanner.cs), [CombatTacticalPlanner](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Planning/CombatTacticalPlanner.cs) e [AnalysisWorker](C:/Users/Administrador/Desktop/ModRimWorld/Source/AutonomousRim/Core/AnalysisWorker.cs).

## Lista de 55 melhorias

Legenda: **Ampliar** = já existe uma base e precisamos melhorá-la; **Novo** = mecanismo geral ou capacidade que não encontrei implementada de forma completa. P0 é essencial para confiabilidade; P1 consolida a colônia; P2 amplia estratégia/desempenho; P3 fica para depois da estabilidade. Cada item traz uma condição de aceitação para orientar a implementação futura.

### Primeiro: impedir que a colônia pare

1. **Monitor de progresso por objetivo — P0, Ampliar.** Medir mudança real em pesquisa, construção, Bills, equipamento e transporte. Se houver trabalho viável durante uma janela compatível com sua duração, detectar ausência de progresso e investigar. Aceitação: V3/V4 e um novo caso bloqueado mostram a causa; sono e pausa voluntária não geram falso travamento.

2. **Diagnóstico completo das Bills — P0, Novo.** Verificar ingrediente fixo, material permitido, raio de busca, acesso, energia, combustível, habilidade, trabalhador e área de interação. Aceitação: cada impedimento preparado produz um motivo específico e deixa de aparecer quando resolvido.

3. **Coordenador único de ordens — P0, Novo.** Os módulos propõem tarefas; um coordenador resolve disputas pelo mesmo colono, bancada ou item. Médico, cozinheiro, pesquisador e crafter não devem ser disputados por ordens incompatíveis. Aceitação: nenhum job válido é interrompido repetidamente por outro módulo sem uma urgência registrada.

4. **Grafo único de necessidades — P0, Novo.** Uma peça faltante gera dependências explícitas: tecnologia, bancada, recurso, fabricante, produção e equipamento. Obras e comércio consultam o mesmo estado. Aceitação: uma necessidade atravessa a cadeia até ser satisfeita, com impedimento e responsável visíveis.

5. **Reserva lógica de materiais — P0, Novo.** Dividir o estoque disponível entre tarefas aprovadas, considerando Frames, Bills e entregas. Impedir que expansão ou comércio consumam o aço reservado para equipamento urgente. Aceitação: nenhuma unidade é prometida simultaneamente a duas demandas incompatíveis; reservas expiram ou são liberadas ao cancelar.

6. **Funções estáveis com substitutos — P0, Ampliar.** Preservar os especialistas recentes e indicar suplentes por habilidade, saúde e carga real. Aceitação: a saída do crafter/doctor ativa um substituto capaz, respeitando incapacidades e prioridades manuais.

7. **Emergências por alcance e gravidade — P0, Ampliar.** Consolidar a distinção entre perigo local, ameaça global e recuperação individual. Aceitação: ferido seguro ou fogo remoto não paralisa a indústria; fogo essencial ou ataque imediato mobiliza a resposta adequada.

8. **Evitar mudanças incessantes — P0, Ampliar.** Usar períodos mínimos de permanência e margens para trocar funções, alvos e metas; urgências podem romper essa permanência. Aceitação: reduzir interrupções inúteis sem atrasar resgate, incêndio ou resposta ao ataque.

9. **Verificar resultado de cada ação — P0, Ampliar.** Diferenciar proposto, emitido, aceito, em andamento, concluído e falhou. Aceitação: “colete concluído” exige produto real e, para fechar o loadout, equipamento efetivo no colono.

10. **Auditoria ao carregar e ao desbloquear tecnologia — P0, Novo.** Revalidar responsáveis, referências, filtros, bancada e demanda depois de load/research. Aceitação: retomada não perde produção nem repete ordens antigas, e libera o próximo trabalho quando a pesquisa necessária termina.

### Segundo: entender a colônia ao longo do tempo

11. **Registro de eventos estruturado — P1, Novo.** Guardar evento, tick, mapa, objeto, módulo e resultado, com retenção limitada. Aceitação: reconstruir a sequência que levou a um gargalo, sem depender de mensagens dispersas no log.

12. **Tendências e projeções — P1, Novo.** Registrar produção/consumo, pesquisa por hora, energia e estoque útil. Aceitação: avisar sobre queda sustentada de reserva e estimar prazo de esgotamento usando tempo de jogo efetivamente transcorrido.

13. **Decisões explicáveis — P1, Ampliar.** Mostrar fatores de pontuação, candidatos descartados e motivos. Aceitação: a HUD explica por que Bravo foi escolhido para crafting e por que a alternativa não foi escolhida.

14. **Modelo de salas conectadas — P1, Novo.** Representar portas, corredores, temperatura, funções e acesso. Aceitação: localizar cômodo isolado, rota bloqueada e equipamento sem célula de interação; um freezer a −2 °C deve ser considerado correto, não “sala fria defeituosa”.

15. **Catálogo dinâmico de definições — P1, Ampliar.** Relacionar itens, receitas, bancadas, pesquisa e uso real, com IDs estáveis e nomes localizados. Aceitação: um item de mod conhecido pelo jogo participa da análise sem ser confundido com outro nome semelhante.

16. **Memória aproveitada entre colônias — P2, Novo.** Exportar casos e resultados, associando bioma, versão, mods e dificuldade. Aceitação: uma observação validada influencia uma política compatível; notas isoladas permanecem hipóteses, sem virarem regras gerais automaticamente.

17. **Políticas versionadas e reversíveis — P2, Novo.** Separar preferências do usuário, estratégia proposta e estratégia comprovada. Aceitação: comparar versões nos mesmos cenários e voltar à anterior sem perder saves ou histórico. Preferir parâmetros declarativos validados ao carregamento de código arbitrário durante a partida.

18. **Histórico reproduzível de decisões — P1, Novo.** Registrar entradas relevantes, versão da política, ordem e resultado para investigação. Aceitação: explicar uma decisão em um checkpoint; não prometer reprodução exata de toda a simulação aleatória só pela seed.

19. **HUD de progresso e bloqueios — P1, Ampliar.** Acrescentar velocidade de progresso, última ação efetiva, responsável, impedimento e próxima dependência nas abas existentes. Aceitação: encontrar “pesquisa parada porque bancada indisponível” sem abrir arquivos técnicos.

20. **Estado compartilhado consistente — P0/P1, Novo.** Todos os módulos trabalham sobre uma versão identificada da observação, com idade do dado. Aceitação: uma compra, morte, novo mapa ou alteração de prioridade invalida decisões antigas pertinentes antes de aplicar ordens.

### Terceiro: base, alimentação e equipamentos eficientes

21. **Escolha de terreno pelo custo total — P1, Ampliar.** Além de apoio para paredes/pisos, comparar tempo de limpeza, mineração segura, conexão à base e transporte. Aceitação: preferir área viável e próxima, explicando quando outra precisa ser escolhida. Escavação exige avaliar teto e risco de desabamento.

22. **Prévia completa antes de uma etapa de construção — P1, Ampliar.** Validar células, materiais, portas, calor e acesso do lote antes de emitir blueprints. Aceitação: apontar o obstáculo e preparação necessária; aplicação parcial mantém registro e não gera duplicatas ao tentar novamente.

23. **Catálogo dos módulos do usuário com versões — P1, Ampliar.** Transformar os layouts existentes em templates com rotação, fase inicial/intermediária/final e upgrades associados à pesquisa. Aceitação: reproduzir os dois estoques adjacentes, cozinha/abate/freezer e módulos aprovados, sem trocar o desenho por um layout genérico.

24. **Paredes compartilhadas e circulação garantida — P1, Ampliar.** Reservar entradas, corredores e interação das bancadas durante toda a sequência de obras. Aceitação: não duplicar paredes comuns nem fechar o construtor dentro do quarto.

25. **Otimização de trajetos — P2, Novo.** Medir deslocamento entre recursos, oficinas, cozinha, freezer e hospital antes de propor expansão. Aceitação: reduzir distância por tarefa em comparação ao mesmo mapa sem otimização, mantendo o template aprovado.

26. **Climatização baseada no funcionamento real — P1, Ampliar.** Considerar cômodos conectados, vents, saída de calor, temperatura externa, demanda elétrica e recuperação após falha de energia. Aceitação: freezer mantém a meta de −2 °C, dormitórios compartilhados usam aquecimento suficiente e geradores não aquecem inadvertidamente a comida. Pisos de madeira continuam conforme a preferência atual; proteção contra incêndio deve ser planejada ao redor disso.

27. **Preparação sazonal preditiva — P1, Ampliar.** Usar dias até a estação desfavorável, colheita restante, combustível e roupas adequadas, além do alvo atual de reserva. Aceitação: completar estoque útil antes do inverno, sem esperar que a temperatura já tenha caído.

28. **Reduzir deterioração alimentar — P1, Ampliar.** Priorizar ingredientes que estragam primeiro dentro dos filtros, dietas e acesso permitidos; separar ingrediente utilizável de alimento proibido/contaminado. Aceitação: diminuir desperdício sem alterar silenciosamente restrições do jogador ou usar ingrediente incompatível.

29. **Armazenamento com capacidade medida — P1, Ampliar.** Considerar prateleiras, posições livres, stack limits, gêneros e reservas de oficina/freezer. Aceitação: detectar lotação e adicionar/expandir armazenamento previsto, sem criar stockpile avulsa para esconder o problema.

30. **Fechar automaticamente a cadeia de equipamento — P0/P1, Ampliar.** Integrar falta real por colono com demanda, produção e aquisição. Aceitação: cada peça percorre pesquisa → recurso → fabricação/compra → transporte → uso, sem avançar checkpoint apenas porque foi pesquisada. Manter peças reserva e reposição por condição/qualidade quando houver orçamento.

### Quarto: vencer combates e recuperar a equipe

31. **Separar ameaça observada de ataque em andamento — P1, Ampliar.** Distinguir espera, ataque, cerco, defesa de área e contato imediato. Aceitação: ameaça dormente é acompanhada e preparada sem bloquear a colônia indefinidamente; mudança de postura hostil ativa defesa.

32. **Comparação de equipamento por cobertura corporal — P1, Ampliar.** Refinar dano, penetração, qualidade, condição, partes expostas e concentração de ataques. Aceitação: evitar considerar colete/capacete proteção uniforme de todo o corpo; só aumentar agressividade quando a vantagem estimada e a rota justificarem.

33. **Plano de equipe com funções explícitas — P1, Ampliar.** Atribuir linha de tiro, interceptação, pressão, reserva e médico conforme o encontro. Aceitação: duas funções melee e funções ranged cooperam sem depender de todos avançarem ao inimigo mais próximo.

34. **Diagnóstico de melee ocioso — P0/P1, Ampliar.** Diferenciar ausência de alvo seguro, bloqueio de caminho, disputa de célula, job recusado e espera deliberada. Aceitação: eliminar inatividade sem motivo; manter espera justificada quando avançar seria perigoso.

35. **Flanqueamento coordenado — P1, Ampliar.** Combinar janela de ataque, exposição da rota e suporte, reservando células distintas. Aceitação: os dois melee conseguem pressionar o mesmo alvo ou proteger lados diferentes sem bloquear um ao outro.

36. **Preservar disparos dos ranged — P0/P1, Ampliar.** Evitar que troca constante de posição/alvo interrompa preparação do tiro, salvo perigo maior. Aceitação: medir disparos e dano reais, demonstrando ganho de atividade no mesmo cenário em vez de apenas contar ordens de ataque.

37. **Resposta específica ao tipo de ataque — P2, Ampliar.** Ajustar o plano para breaches, sappers, cerco, infestação e inimigos rápidos/especiais. Aceitação: defesa não presume que todos usarão a killbox e considera o acesso real ao hospital e aos civis.

38. **Retirada com destino e alternativas — P1, Ampliar.** Preparar defesa secundária, rotas, portas e ponto de reorganização. Aceitação: combatentes chegam a proteção utilizável sem congestionamento; ordem de recuo sozinha não conta como retirada bem-sucedida.

39. **Resgate com custo para o restante da equipe — P1, Ampliar.** Refinar o cálculo existente de tempo até morte, percurso, tratamento e força que permanece na defesa. Aceitação: em cenário com dois inimigos restantes e um aliado sangrando, escolher quem carrega, quem trata e quem segura o combate, ou registrar por que uma tentativa imediata seria inviável.

40. **Avaliar vitória pela recuperação posterior — P1, Ampliar.** Acompanhar sangramento, infecção, incapacidade e retorno das funções por até 72 horas de jogo nos ensaios. Aceitação: diferenciar vitória com feridos recuperados de combate encerrado com colonos que morreram depois. Não exigir ausência absoluta de ferimentos para considerar boa decisão.

### Quinto: estratégia ampla, desempenho e campanha completa

41. **Economia por retorno e prioridade — P2, Ampliar.** Comparar produtos pelo comprador disponível, preço efetivo, trabalho, matéria-prima e risco. Aceitação: financiar recursos faltantes sem desviar o crafter do equipamento urgente ou retirar comida/medicina da colônia.

42. **Caravanas com carga, interrupções e retorno — P2, Ampliar.** Estender a formação/transporte nativos existentes com demanda confirmada, manifestos, suprimentos, tempo de viagem e segurança. Aceitação: formação interrompida ou compra indisponível gera plano alternativo; aquisição só fecha demanda após entrega utilizável.

43. **Prisão com decisão estratégica e capacidade — P2, Ampliar.** Calcular valor de recrutar/converter/liberar versus camas, alimentação, medicina, funções e relações. Aceitação: não lotar a prisão nem deixar resgate de invasor atrasar tratamento urgente de colono.

44. **Gestão de quests e diplomacia — P2, Novo.** Comparar prazo, deslocamento, risco, recompensa e dependências com as metas atuais. Aceitação: aceitar uma oportunidade executável e recusar outra incompatível com a capacidade, registrando o motivo.

45. **Ideology como parte das necessidades — P2, Novo.** Integrar preceitos, papéis, vestuário exigido e rituais ao plano, preservando a ideologia existente. Aceitação: resolver uma exigência concreta sem equipar algo que cause outro problema previsto ou editar a ideologia automaticamente.

46. **Royalty com uso tático e logístico — P2, Novo.** Planejar permissões, capacidades psíquicas disponíveis, requisitos e recuperação. Aceitação: usar uma capacidade adequada ao risco real e respeitar seu custo/estado; a agenda de meditação já existente serve de base.

47. **Biotech além de equipamentos normais — P3, Novo.** Adicionar políticas específicas para mechs, manutenção, energia, colonos com necessidades diferentes e responsabilidades de cuidado. Aceitação: um cenário de expansão funciona sem tratar todos os agentes como adultos humanos com a mesma rotina.

48. **Anomaly com contenção e limites — P3, Novo.** Distinguir risco e requisitos reais das entidades e pesquisas presentes na instalação. Aceitação: não iniciar uma cadeia perigosa sem capacidade de contenção e resposta verificada.

49. **Odyssey e múltiplos mapas — P3, Novo.** Inventariar as capacidades disponíveis da expansão na versão instalada e organizar operações por mapa/destino. Aceitação: nenhum executor aplica ordem no mapa errado nem abandona funções essenciais ao transferir equipe. Não presumir suporte apenas porque a DLL reconhece que a DLC está ativa.

50. **Orçamento de CPU medido por módulo — P1/P2, Ampliar.** Medir média, percentis de tempo e alocações, com ticks por segundo na mesma situação. Aceitação: otimização mantém comportamento e reduz o custo registrado; separar o tempo do mod do restante do jogo.

51. **Paralelismo onde existe ganho comprovado — P2, Ampliar.** Aproveitar o worker já existente para cálculos puros de snapshots; avaliar novas tarefas somente depois de medir. Aceitação: resultado obsoleto é descartado, a thread principal não espera e regras permanecem equivalentes com o modo paralelo desligado. Sem fixar núcleo ou acessar Unity/pathfinding nativo fora da thread principal.

52. **Planejador externo opcional por MCP — P3, Novo.** Expor observações compactas e metas de alto nível a um modelo, com orçamento de tempo/custo. Aceitação: desconexão ou lentidão não para necessidades, combate e trabalho local. Ordens passam pela mesma validação nativa e pelo coordenador.

53. **Prévia e limites de ações custosas — P1/P2, Ampliar.** Registrar custo/efeito e executar dentro de políticas definidas pelo usuário; integrar checkpoints existentes quando apropriado. Aceitação: repetição de um comando não duplica uma compra ou demolição; o relatório distingue preparação de uma ação já aplicada. Consultas genéricas não devem dar ao planejador acesso irrestrito para alterar qualquer estado do jogo.

54. **Benchmark comum e regressões por etapa — P0, Ampliar, transversal.** Fixar saves, seeds, versão, DLCs, equipe, equipamento e critérios. Aceitação: cada mudança tem evidência comparável de progresso e limitações. Elite Six é nossa fixture acelerada; comparações com cenários normais devem ficar em uma trilha separada, sem alterar a preferência atual de testes.

55. **Campanha até uma condição de vitória — P3, Ampliar.** Depois de validar sobrevivência, inverno e equipamentos, acrescentar execução da rota final escolhida, logística, defesa e detecção de conclusão. Aceitação: mostrar sucesso no próprio jogo e histórico completo sem recursos/curas/spawns de teste. A nave continua adiada enquanto as etapas anteriores não forem confiáveis.

## Aproveitamento de código: o que faz sentido adaptar

| Referência | Prioridade de adaptação | Cuidados para nosso projeto |
|---|---|---|
| `autorim-mcp/.../Commands/AnalyzeCommands.cs` | Alta, sobretudo ideias dos diagnósticos | Focar tarefas realmente demandadas; acrescentar bloqueio de ingrediente, prioridade concorrente e acesso. Não substituir nossa distribuição por um ranking isolado. |
| `autorim-mcp/.../Core/DefResolver.cs` | Alta quando existir consulta textual | Portar busca/ambiguidade e IDs estáveis; acomodar português e filtros de conteúdo ativo. Evitar escolher automaticamente um item diferente só por nome parecido. |
| `rimagent/.../Scorer/PriorityReason.cs` | Alta | Adaptar fatores estruturados para nossa pontuação e HUD, mantendo especialistas estáveis. |
| `rimbridge/.../State/BaseGraph.cs` | Alta, como referência de modelo | Portar relações e consultas; thresholds precisam ser específicos da função da sala. Seu critério genérico de frio não serve para nosso freezer. |
| `rimagent/.../worlddiff.py` e `tracker.py` | Alta, como desenho a portar para C# | Usar IDs persistentes, valores com timestamp e alertas por domínio; não depender de Python para cada atualização local. |
| `rimagent/.../Orders/StandingOrders.cs` | Alta como padrão de coordenação | Aproveitar posse de valores e respeito a intervenções; consolidar os mecanismos próprios em vez de ter dois gestores para o mesmo campo. |
| `rimagent/.../Stock/StockComponent.cs` | Média/alta | Aproveitar orçamento, alternância e isolamento de falhas; preservar nossos estoques por dias, produção e aquisição já existentes. |
| `autorim-mcp/.../Bridge/Dispatcher.cs` | Média, ligada à integração opcional | Filas, liveness e descarte; orçamento precisa ser medido. Timeout não pode ser tratado como prova de que uma ação que já começou não ocorreu. |
| `autorim-mcp/.../Core/SafetyGate.cs` | Média, adaptada à autonomia | Prévia/auditoria/checkpoint sem impor confirmação manual a toda rotina previamente autorizada. |
| `rimagent/.../reflect.py`, `braingit.py` e `scorecard.py` | Média, após benchmark | Memória e comparação por versão. Não copiar a pontuação linear como objetivo: premiar riqueza pode incentivar acumulação que não melhora sobrevivência. |
| `rimagent/.../tools/watchdog.py` | Baixa para a versão inicial | Aproveitar diagnóstico e verificação antes de aceitar alteração. Não carregar uma DLL alterada ou código gerado durante a partida de produção. |
| RimMolt | Ideias de experiência e integração | Código/licença não verificados; implementar os conceitos de modo próprio em vez de copiar DLL, imagens ou material sem autorização identificada. |

Os caminhos completos das referências estão fixados pelos links das seções anteriores. A adaptação deve registrar origem, revisão, trecho usado e mudanças em `THIRD_PARTY_NOTICES.md`, mantendo os avisos correspondentes.

### Abordagens que não devemos copiar integralmente

- O combate de `Order_Combat` do Steward prioriza rally/cover e, quando sobrepujado, atribui o inimigo mais próximo a cada combatente. É útil para classificação e liberação de controle, mas não basta para substituir nossa cooperação melee/ranged. [Código](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/mod-steward/Source/Steward/Orders/Order_Combat.cs).
- A rotina de resgate do Steward oferece bons diagnósticos e fallback de cama, mas escolhe principalmente um carregador próximo e médico disponível. Preservar nosso cálculo de risco de combate e ampliar a previsão de recuperação. [Resgate](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/mod-steward/Source/Steward/Orders/Order_Rescue.cs).
- O AutoRim envia caravanas imediatamente com o que os pawns carregam, sem manifestos completos no fluxo de formação. Nossa caravana nativa já validada não deve perder essa capacidade. [Caravanas](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/mod-src/AutoRim/Commands/CaravanCommands.cs).
- Metas fixas de estoque como madeira por número de colonos servem para começar, mas precisamos considerar tarefas aprovadas, bioma, capacidade e transporte. Nosso cálculo de comida em dias deve continuar, com previsão mais completa. [Metas iniciais](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/mod-steward/Source/Steward/Stock/DefaultStockPlan.cs).
- Não copiar a distribuição completa de trabalho Free Will por cima da nossa. Seu código explica pontuação e estado, mas prioridades muito voláteis contrariam a necessidade observada nesta equipe. Aproveitar justificativas e sinais; medir a mudança antes de promover uma política.
- Gerar e carregar ferramentas Python livremente, como o rimagent permite na biblioteca do agente, adicionaria outro ambiente e mais superfícies de falha. Na primeira versão, preferir estratégias declarativas com parâmetros limitados e executores conhecidos. [Registro de ferramentas](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/agent/rimagent/registry.py).

## Licenças e compatibilidade

Confirmei o texto MIT em [rimagent](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/LICENSE), [RimBridge](https://github.com/zorrobyte/rimbridge/blob/3c1e4c7cee151104b85bf9c8372e113f91c5f08d/LICENSE) e [autorim-mcp](https://github.com/Critical-Reynolds/autorim-mcp/blob/89fadf37e5af4d4e5ca24e7b822d8b3fbb8084ca/LICENSE). Essa licença permite adaptação mantendo os avisos de copyright e licença nas cópias/porções substanciais. O Steward também identifica código derivado de Free Will e Colony Manager/Redux, com avisos que acompanham esses trechos. [Créditos adicionais](https://github.com/zorrobyte/rimagent/blob/85cb050dec47691f2a80096fdc8c8a8e2051bb13/THIRD_PARTY_NOTICES.md).

Não interpretar a licença do repositório como autorização para distribuir os arquivos do jogo, arte de terceiros ou conteúdo integral de wiki que não tenha licença verificada. Nesta pesquisa não houve incorporação de código ao mod. As referências baixadas servem para inspeção e planejamento.

## Ordem de execução proposta

1. **Entrega A: diagnóstico e progresso.** Itens 1, 2, 9, 10, 11 e 19, com regressões 54 desde o começo. Aproveitar os saves V3/V4 preservados. Resultado: identificar a primeira causa de bloqueio e mostrar progresso efetivo na HUD.
2. **Entrega B: coordenação e demanda.** Itens 3–8, 20 e 30. Resolver disputa de trabalhadores, recursos e pesquisa/produção sem reescrever todos os módulos simultaneamente. Resultado: conjuntos da etapa atual são produzidos e usados sem paralisar sobrevivência e pesquisa.
3. **Entrega C: base funcional e inverno.** Itens 14, 21–29. Completar módulos aprovados, acesso, energia e estoques, depois validar sazonalidade. Resultado: base conectada e utilizável, não apenas desenhos colocados no mapa.
4. **Entrega D: combate e recuperação.** Itens 31–40, começando pelos problemas de inatividade e manutenção de fogo. Resultado: melhorar encontros equivalentes e acompanhar feridos até recuperação, com regressões de resgate.
5. **Entrega E: economia e metas dinâmicas.** Itens 12, 13, 15, 41–44; fechar deficits por recurso local, fabricação, compra e oportunidade. Considerar techprints reais e requisitos de bancada na aquisição para checkpoints avançados.
6. **Entrega F: memória e desempenho.** Itens 16–18 e 50–51. Resultado: ajustes comparáveis e ganho medido, com alternativa síncrona preservada. Medição básica de 50 pode começar antes; expansão do paralelismo depende dela.
7. **Entrega G: expansões e integração opcional.** Itens 45–49, 52–53, por DLC e capacidade testada. Resultado: cobertura incremental e operação local independente do modelo externo.
8. **Entrega H: vitória.** Item 55 somente depois de campanha de sobrevivência, progressão militar e logística aprovadas. A execução final não pode usar as facilidades das fixtures.

Pausas por limite de uso devem deixar um registro da entrega, itens concluídos, evidências, versão/hash e próxima verificação. Não marcar uma entrega como concluída apenas por compilar ou passar um cenário isolado.

## Benchmark que permite demonstrar vantagem

O item 54 acompanha todas as entregas; não é trabalho adiado para depois de todos os recursos novos.

| Frente | Critérios propostos |
|---|---|
| Trabalho | Tempo produtivo/ocioso injustificado durante janelas de trabalho; continuidade de funções; motivo das interrupções. Sono, recreação e recuperação necessária não contam como desperdício. |
| Equipamento | Tempo para fechar cada checkpoint, peças efetivamente equipadas, consumo de materiais, qualidade e condição; gasto comercial e falhas de entrega. |
| Base | Percentual de obra realmente concluído; salas conectadas; bancadas utilizáveis; piso/teto corretos; transporte; energia e temperatura em uso. |
| Comida | Menor reserva útil em dias, desperdício, episódios de fome e custo de trabalho, considerando dietas, acesso e deterioração. |
| Combate | Resultado, disparos, dano, tempo melee ocioso, cooperação, retirada, resgate, incapacitações e mortes até 72 horas depois. |
| Campanha | Sobrevivência em 20/60/120 dias, inverno, progressão militar, independência de intervenção e conclusão da rota final quando implementada. |
| Desempenho | TPS, duração média/P95 das análises, alocações, fila pendente e resultados obsoletos; mesmo save, velocidade escolhida 3× e observação da velocidade realmente entregue pelo motor. |
| Integração LLM opcional | Tempo sem modelo disponível, ações recusadas, custo/latência e funcionamento contínuo dos executores locais. |

Usar duas trilhas identificadas: a fixture Elite Six com as regras aceleradas já combinadas, e campanhas comuns para avaliar generalização. Spawn/research instantânea ficam apenas em preparação de fixtures identificadas. Depois do início de uma campanha válida, não gerar material, curar participantes, ajustar skills ou apagar ameaças para fabricar sucesso.

Para comparar aos concorrentes: usar saves/configurações equivalentes quando houver compatibilidade; registrar a IA/modelo externo e seu orçamento; definir intervenção manual e condição de vitória; repetir em várias seeds e publicar falhas junto dos acertos. Se um projeto não rodar na versão instalada, registrar incompatibilidade, sem contabilizá-la como derrota de gameplay. Esta pesquisa não executou esse benchmark e não atribui ranking de vitória.

## Primeiro pacote recomendado para a próxima implementação

Começar por um **diagnóstico de progresso com coordenação de demandas**, usando V3/V4 como regressões, em vez de tentar entregar as 55 mudanças de uma vez. Estruturas candidatas, ainda não implementadas: observação versionada, demanda com dependências, motivo de bloqueio, responsável por ordem e verificação de resultado.

O critério central: ao aparecer “faltam dois coletes”, a IA precisa mostrar quem fará, quais recursos e bancada serão usados, por que está aguardando e o que mudou até os coletes estarem vestidos. A mesma cadeia deve funcionar para pesquisa, quartos e resgates, com urgências próprias e respeito ao controle manual.

## Implementação iniciada em 11/10/2026

O primeiro pacote da Entrega A está implementado: diagnóstico nativo de Bills, progresso efetivo de pesquisa/produção/obras, histórico persistente e painel na HUD. Os itens 1, 2, 9, 10, 11 e 19 têm cobertura incremental; não foram marcados integralmente concluídos. Escopo, limites, testes, hashes e próximo checkpoint estão em [Entrega A — diagnóstico e progresso](C:/Users/Administrador/Desktop/ModRimWorld/docs/ENTREGA-A-DIAGNOSTICO-PROGRESSO-2026-10-11.md).

O primeiro pacote da Entrega B acrescenta coordenação de fabricantes/pesquisador/construção com receitas viáveis, orçamento conservador dos próximos lotes e respeito à sobrevivência e às alterações manuais. Cobertura incremental dos itens 3–8/20/30. O grafo completo de dependências, responsáveis/prazos persistentes e obtenção de recursos por coleta/comércio continuam pendentes. Evidências, falhas preservadas e limites: [Entrega B — coordenação](C:/Users/Administrador/Desktop/ModRimWorld/docs/ENTREGA-B-COORDENACAO-2026-10-11.md).

A continuação B2 acrescenta demandas persistentes por destinatário/checkpoint/peça, separando fabricação de entrega equipada, e coleta de ingredientes independente da construção. Revisão de metas é informativa; dependências recursivas, abastecimento completo de bancadas e arbitragem comercial ainda precisam expansão. Evidências e limites: [Entrega B2 — cadeia de equipamentos](C:/Users/Administrador/Desktop/ModRimWorld/docs/ENTREGA-B2-CADEIA-EQUIPAMENTOS-2026-10-11.md).

A continuação B3 acrescenta a cadeia nativa dos dois tipos de componentes, combustível de bancadas e demanda industrial na fila comercial. Fabricação de componente → SMG equipada, madeira → abastecimento → espada e regressões nativas passaram; fabricação avançada completa e compra/entrega reais desses novos pedidos permanecem pendentes. [Entrega B3 — insumos industriais](C:/Users/Administrador/Desktop/ModRimWorld/docs/ENTREGA-B3-INSUMOS-INDUSTRIAIS-2026-10-11.md).

B4 trata continuidade de peças inacabadas e pausa/limpeza industrial por comida e emergência. A cobertura é incremental, sem declarar o grafo universal ou os 55 itens concluídos. [Entrega B4 — retomada industrial](C:/Users/Administrador/Desktop/ModRimWorld/docs/ENTREGA-B4-RETOMADA-INDUSTRIAL-2026-10-12.md).
