# Validação por etapas — iniciada em 08/10/2026

Objetivo: testar todas as funcionalidades, corrigir falhas e permitir retomada sem repetir etapas aprovadas. Usar perfis isolados e preservar saves originais. Testes de mecanismos podem preparar cenários controlados; testes de autonomia devem usar trabalho, recursos e passagem do tempo nativos, velocidade 3×.

## Estado de retomada

Em andamento: etapa 4, prisão. Etapas 0–3 concluídas no escopo indicado; construção completa atual é etapa 7. Revisão inicial `1f66fbf`, correções básicas `3863a07`, correção da cela em validação.

| Etapa | Escopo | Estado / evidência |
|---|---|---|
| 0 | Compilação, cálculo e políticas | 57 verificações passaram; complemento de testes compilado sem erros |
| 1 | Carregamento, XML, allow, comida, equipamento e construção básica | PASS: carregamento, allow, Bills, jobs reais de equipamento e desligamento; log `Player-20261008-204405-289.log`. Construção antiga excluída desta etapa; layout modular terá ensaio próprio |
| 2 | Prioridades, agenda, emergência e resgate | PASS: 14 verificações de trabalho/agenda, emergência com recuperação/restauração e resgates nativos com/sem cama |
| 3 | Progressão, pesquisa, produção e armazenamento | PASS: sete grupos, incluindo fabricação nativa de capacete, substituição de parede por pedra, prateleira construída e salvar/carregar. Progressão prolongada ainda na etapa 7 |
| 4 | Prisão: construção, captura, alimentação, tratamento, conversão, recrutamento, liberação, controle manual e salvar/carregar | Em execução; corrigido cache nativo da cela que bloqueava captura. Capturas reais observadas; demais resultados aguardando |
| 5 | Comércio: produção, interação local/orbital, orçamento, entrega, caravana, controles e persistência | Pendente; criar ensaio específico |
| 6 | Combate: cenários variados, colaboração melee/ranged, retirada e resgate | Pendente |
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

