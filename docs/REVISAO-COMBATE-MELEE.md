# Revisão do combate melee — 08/10/2026

## Comportamento implementado

- Escolha de alvos considera inimigos atacando aliados, oportunidade de encostar em atiradores, saúde e força local.
- Dois melee podem compartilhar o alvo e escolher células de aproximação de lados diferentes. Destinos reservados evitam disputar a mesma célula. Isso é coordenação de aproximação; não representa uma inteligência completa de cerco.
- Aproximação usa caminhos nativos, verifica fogo, obstáculos, desvios, outros inimigos melee no caminho e exposição aos atiradores ao longo do trajeto. A distância de perseguição continua limitada.
- Apoio exclui combatentes em retirada ou feridos. Inclui outro melee próximo capaz de chegar ao alvo e atiradores com linha de tiro atual ou uma posição de tiro próxima alcançável.
- Alvo e destino têm um período de persistência para reduzir interrupções de caminhada, preparação e golpes. Mudança de perigo, ferimentos ou alvo inválido permite interromper a aproximação.
- Contra um grupo melee maior, o interceptor favorece manter a linha defensiva. Se o parceiro recua, fica ferido ou incapacitado e ele fica sozinho perto de vários inimigos, também recua, incluindo grupos de atiradores.
- Se a retirada não encontra uma posição segura e existe um inimigo em contato, o colono recebe um ataque melee nativo, em vez de ficar esperando apanhar.
- Um inimigo melee já encostado, mais rápido que o colono e sem outro interceptor cobrindo a saída, provoca defesa em contato. A IA deixa de repetir tentativas de fuga que não rompem a distância do atacante.
- Comandos posteriores do jogador continuam retirando o colono da automação. Desligar o controlador libera apenas os recrutamentos da IA.

## Equipamentos e limites da avaliação

O scanner lê armas e roupas de ambos os lados, incluindo qualidade/material por meio dos stats nativos. Considera DPS, penetração, proteção cortante/contundente ponderada pela cobertura corporal, saúde, velocidade e skills. Ataques naturais sem arma usam os stats melee do pawn. O tipo principal de dano considera a ferramenta dominante da arma, evitando classificar uma espada como contundente apenas por ter um golpe com o pomo.

Proteção é uma estimativa para decidir risco. Não reproduz cada sorteio de acerto, parte atingida, dano ou penetração do motor. Armadura nunca significa imunidade. Explosivos/ataques especiais continuam impedindo uma carga otimista. Escudos, habilidades especiais, todos os mods/DLCs e defesa contra todas as raças não foram validados nesta rodada.

## Processamento em outra thread

Foi introduzida uma única thread de análise, com fila limitada a quatro trabalhos. Ela recebe apenas cópias imutáveis de números/flags e calcula avaliações de alvos. Pawn, Map, Unity, ordens e pathfinding ficam na thread principal. O sistema não fixa afinidade a um núcleo físico do processador.

Mudança na composição do grupo e primeira resposta são síncronas. Resultados têm versão/validade; dados antigos ou fila indisponível mantêm a alternativa síncrona. O controlador não espera pela thread. Falha no cálculo libera o trabalho pendente e a alternativa síncrona continua disponível.

Em produção, a thread só é utilizada a partir de **256 pares aliado/inimigo**; combates pequenos usam cálculo direto. O fixture força o limiar para 1 para verificar o caminho paralelo com quatro colonos. Nas repetições de desenvolvimento, cerca de 20–100 cálculos de ranking consumiram aproximadamente 0,6–3 ms **somados**, enquanto o executor nativo consumiu centenas de milissegundos. Essa etapa isolada não era o gargalo do combate pequeno. Não há ganho de FPS comprovado nem promessa de acelerar o motor do RimWorld.

## Condições dos testes

Perfis isolados, sem carregar ou alterar saves do jogador, no RimWorld 1.6.4633 com Core, Harmony, AutonomousRim e complemento temporário de runtime. Modo Pacífico e velocidade 3 selecionados; hostis são criados explicitamente no início para testar combate sem raids aleatórias do storyteller. O motor pode reduzir a velocidade efetiva durante a luta.

Quatro colonos: dois melee com espada longa/maça e armadura de placas de aço, dois ranged com armas mistas e Flak Vest. Todos começam com capacete de aço, camisa/calça de tecido, qualidade Normal, todas as skills 20, sem traços, hediffs ou skills desabilitadas. Food, Rest, Recreation e Mood começam cheios. Não há reposição desses valores, cura ou equipamento novo durante a batalha. Ferimentos e incapacitações posteriores são resultados reais do motor.

A arena, cobertura, abrigo e participantes são preparados pelo fixture. Isso testa mecânicas de combate, não aquisição econômica dos equipamentos, construção ou sobrevivência de uma colônia inteira. O abrigo não tem suporte médico. Resultados ao encerrar a observação não comprovam sobrevivência posterior ao sangramento.

Os casos terminam em neutralização, derrota da equipe, retirada de todos para abrigo com porta fechada após 1.800 ticks, ou limite de 6.000 ticks. Sementes fixam seleção/configuração do encontro, mas a geração nativa dos pawns/mundo não é idêntica entre processos. Repetições não são comparação controlada de FPS, dano ou taxa de vitória.

## Falhas encontradas durante o desenvolvimento

As primeiras execuções mostraram apoio superestimado, interrupção de aproximações quando o atirador reposicionava, carga para fora da linha defensiva e um melee isolado após recuo do parceiro. Foram ajustados o apoio efetivo, persistência de ordens, manutenção da linha, retirada da dupla e risco do caminho.

Resultados negativos foram preservados: a execução de desenvolvimento `20261008-142811-case3` conseguiu retirada, mas `20261008-143022-case4` terminou com quatro incapacitados. Posteriormente, `20261008-143747-case4` e `20261008-144005-case2` venceram sem mortes/incapacitações. As versões seguintes ainda registraram perda/ausência de desfecho em portas e combate misto: `20261008-144229-case1`, `20261008-144928-case1` e `20261008-145107-case5`. Uma execução da regressão básica falhou no cenário 2 (`Player-20261008-144332.log`); a seguinte passou nos três (`Player-20261008-145234.log`). A rodada final abaixo é separada e utiliza a mesma DLL nos cinco casos.

## Rodada final do controlador

DLL do mod utilizada nos cinco casos: SHA-256 `7F188BF61666B7986D828567EDDE4817CD3D534994EC7638E01EC61E45828FC8`. A mesma DLL foi instalada e verificada na pasta do jogo. Todos os encontros registraram apenas os humanos previstos na transição inicial de emergência, sem mechs, insetos ou estruturas extras.

| Caso | Encontro | Ticks | Resultado | Mortos | Incapacitados | Feridos | Inimigos restantes | Perfil local |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Portas/paredes, quatro revolvers | 6.000 | Sem desfecho | 0 | 1 | 4 | 2 | `20261008-150524-case1` |
| 2 | Quatro arqueiros, aproximação/cooperação | 3.102 | Vitória | 0 | 0 | 1 | 0 | `20261008-150206-case2` |
| 3 | Desvantagem contra oito rifles | 6.000 | Sem desfecho | 0 | 1 | 3 | 7 | `20261008-150657-case3` |
| 4 | Quatro facas, proteção dos ranged | 1.814 | Vitória | 0 | 2 | 3 | 0 | `20261008-150317-case4` |
| 5 | Dois revolvers e duas maças | 6.000 | Sem desfecho | 0 | 2 | 3 | 2 | `20261008-150408-case5` |

**Duas vitórias e três encontros sem desfecho; seis incapacitações entre vinte participantes independentes.** Nenhuma morte até o encerramento, mas houve sangramento importante em sobreviventes: isso não comprova sobrevivência posterior. A vitória contra facas também não representa proteção suficiente da equipe, pois ambos os melee caíram. Não houve retirada segura dos quatro no encontro contra oito rifles.

O caso 2 registrou seis amostras de flanqueamento, ataques melee e dano nativo. Ordens defensivas e em retirada também são amostradas; uma amostra de espera não demonstra, sozinha, que o colono travou. A amostragem de 60 ticks não registra necessariamente cada golpe individual. O contador de dano melee do fixture foi posteriormente fixado à função inicial para evitar atribuir dano de ranged incapacitado ao melee quando a arma cai; o quadro acima não utiliza esse contador.

As falhas de terreno com portas, separação sob pressão e retirada contra muitos atiradores continuam sendo prioridades de evolução. A implementação resolve decisões específicas, mas estes resultados **não aprovam combate autônomo confiável em todos os encontros**. A rodada anterior, antes da última regra de defesa em contato, também foi preservada: casos 1/3/4 sem desfecho, caso 2 vitória com uma morte e caso 5 vitória sem incapacitações. Não foi utilizada para substituir resultados negativos da rodada final.

A regressão básica `Player-20261008-151139.log` foi repetida após correção do isolamento do fixture: na execução anterior `Player-20261008-150804.log`, destruir uma ruína despertou quatro mechs adicionais, contaminando o encontro nominal de dois inimigos. O complemento agora remove os participantes extras antes do combate, aplica Pacífico e verifica hostis não previstos, sem apagar inimigos durante a batalha ou enfraquecer as verificações de sobrevivência/controle manual.

Os três cenários básicos passaram com a DLL final: interceptação em 104 ticks, cobertura em 3.616 ticks e retirada dos três para abrigo com porta fechada em 1.800 ticks. Nenhum participante morreu ou caiu nesses três casos. Os sujeitos de controle manual/desarmado e a liberação dos drafts da IA passaram nas verificações existentes. São fixtures suplementares com skills de combate 10; os cinco encontros da tabela usam todas as skills 20.

A compilação do mod e do complemento terminou sem erros/avisos; os 49 checks de política passaram, incluindo resultado idêntico do cálculo puro em execução direta e em outra thread. Saves de encerramento são diagnósticos; reabertura não foi validada nesta rodada e a regressão básica registrou avisos de referências no XML. Nenhum save foi exportado para a pasta principal do jogador.

Também foi executado o caso 2 com `-Synchronous`, em `20261008-151320-case2`: sem desfecho em 6.000 ticks, zero mortos/incapacitados, quatro feridos e dois inimigos restantes. Registrou zero trabalhos na thread e não apresentou exceções de execução. Como os pawns e sorteios nativos diferem entre processos, a diferença para a vitória do caso paralelo não comprova vantagem tática nem desempenho da thread. A igualdade do cálculo para os mesmos snapshots foi verificada nos checks puros.

## Repetir os testes

Com o jogo fechado, compile e instale o mod, compile `Tests/AutonomousRim.RuntimeChecks` com o caminho das DLLs do jogo e execute:

```powershell
foreach ($combatCase in 1..5) {
    .\scripts\CombatTest.ps1 -TrialCase $combatCase -MeleeRevision
}
.\scripts\CombatTest.ps1
```

`-Synchronous` desliga a thread no fixture. Cada caso mantém log, `result.txt` e save de encerramento em `.tools/melee-revision/<data-hora>-caseN`. Os testes básicos usam `.tools/combat-tests`. O complemento temporário é removido ao encerrar o processo criado pelo script. Logs/saves ficam locais e não entram no Git.
