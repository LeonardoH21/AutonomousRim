# Combate cooperativo

Ative **Rim AI → Combate cooperativo: LIGADO**. A [emergência automática](EMERGENCIA-COLONIA.md) começa ligada e também aciona este controlador quando há perigo imediato. Desligar combate desliga essa resposta automática para preservar a escolha do jogador. O controlador de combate avalia a cada 15 ticks; o botão também avalia imediatamente. O painel mostra as funções atuais e a quantidade de colonos controlados, em retirada e sob controle manual. A [revisão de melee de 08/10/2026](REVISAO-COMBATE-MELEE.md) descreve a aproximação coordenada e os resultados mais recentes.

## Decisões

Somente colonos armados e capazes são recrutados, quando existe um hostil ativo próximo. Civis desarmados, incapazes de violência e colonos já recrutados pelo jogador não entram no grupo. Ordens manuais posteriores retiram o colono da automação durante aquele combate. Desligar o botão ou eliminar as ameaças libera os recrutamentos da IA e restaura o estado anterior de fogo à vontade.

Atiradores avaliam alcance mínimo/máximo da arma, linha de tiro nativa (incluindo tiros ao redor de esquinas), cobertura, aliados na trajetória, exposição, terreno e aproximação de inimigos corpo a corpo. Ordens de mira e rajadas são mantidas quando seguras. Destinos são reservados para evitar que vários colonos tentem ocupar a mesma posição.

Combatentes corpo a corpo guardam passagens e protegem os atiradores. A interceptação exige proximidade, acesso e apoio local; não persegue o alvo indefinidamente. A escolha de alvo considera inimigos atacando aliados próximos. Os pesos são heurísticos: não há garantia de que a posição escolhida seja a melhor de todo o mapa.

Ferimentos, sangramento, dor, perda de sangue, exaustão e desvantagem local acionam retirada. A velocidade inimiga amplia o espaço necessário para proteger atiradores. Rotas de retirada usam o pathfinder nativo e rejeitam fogo e aproximação perigosa de hostis. Salas fechadas sem inimigos recebem preferência; portas continuam sujeitas ao ataque nativo. Um colono lento ou um grupo em grande desvantagem não interrompe a fuga para atirar. Recuperação exige tempo, distância e uma relação favorável de forças.

## Testes nativos

`scripts/CombatTest.ps1` usa perfil separado em `.tools/combat-tests`, instala temporariamente o complemento de teste e remove-o ao terminar. Não carrega nem sobrescreve o save do jogador. Compile o mod e `Tests/AutonomousRim.RuntimeChecks`, instale o mod e execute o script com o jogo fechado.

Cada cenário cria três participantes novos (dois rifles e uma espada, habilidades 10), equipamentos e terreno controlado. Há dois controles protegidos: um civil desarmado e um colono recrutado manualmente. Projéteis, dano, caminhos, inimigos e portas usam a simulação real. Não há cura, recarga de necessidades ou eliminação de inimigos durante as batalhas. A terceira velocidade é selecionada; o próprio RimWorld pode temporariamente reduzir a velocidade em combate.

1. Um inimigo com faca já próximo de uma passagem estreita: verificar interceptação, dano real e neutralização.
2. Dois adversários com revólver: verificar cobertura, dano real e neutralização.
3. Nove adversários com facas e um defensor previamente ferido na perna: verificar retirada de todos para um abrigo com porta fechada, durante 1.800 ticks. Este cenário valida escape imediato, não eliminação dos nove inimigos nem sobrevivência prolongada dentro do abrigo.

Todos exigem zero participantes mortos ou incapacitados, preservação do controle manual e liberação dos recrutamentos da IA ao desligar. Os saves ficam no perfil separado. `wounded` conta ferimentos presentes, incluindo os preexistentes; no terceiro há uma lesão intencional. Contadores de cobertura, interceptação e retirada são amostras de avaliação, não quantidade de ações distintas.

### Resultado de 06/10/2026

Execução final: `Player-20261006-233835.log`, RimWorld 1.6.4633, Core + Harmony + AutonomousRim + complemento temporário de teste.

| Cenário | Ticks | Mortos | Incapacitados | Com ferimentos ao final | Resultado |
| --- | ---: | ---: | ---: | ---: | --- |
| Passagem estreita, 3 contra 1 | 284 | 0 | 0 | 1 | Ameaça neutralizada; 5 amostras de interceptação |
| Cobertura, 3 contra 2 | 1.079 | 0 | 0 | 0 | Ameaças neutralizadas; 34 amostras de cobertura |
| Retirada, 3 contra 9 | 1.800 | 0 | 0 | 3 | Todos dentro do abrigo, porta fechada; inimigos ativos |

As três verificações de controle manual/civil e desligamento passaram. Não houve exceção da simulação nem erro de referência nos saves nesta execução. A compilação terminou sem erros ou avisos; os 35 checks existentes de políticas também passaram. A DLL instalada foi conferida contra a DLL testada (SHA-256 `F0990C8182698C72C0DC77F97298C110D409E02D352F9F1F5D7D104F92D5CFE6`). Esses resultados registram uma execução controlada, não uma taxa estatística de sucesso.

## Limites conhecidos

Iterações exploratórias registraram incapacitações em corredores mal posicionados e na fuga em campo aberto de um colono ferido. Elas motivaram correções de cobertura, mira, reserva de posições e retirada. Os três cenários controlados não comprovam segurança em qualquer combate, especialmente ataques simultâneos por vários lados ou ausência de abrigo.

A checagem geométrica de aliados reduz linhas de tiro arriscadas, mas não elimina fogo amigo por dispersão ou movimento durante a rajada. Armas com projéteis explosivos não entram na automação. Habilidades especiais, escudos, mechanoides, emboscadas planejadas, DLCs e alterações de combate de outros mods não foram validados por esta suíte. A HUD não foi verificada visualmente e a continuidade das ordens durante save/load de uma batalha precisa de teste específico.
