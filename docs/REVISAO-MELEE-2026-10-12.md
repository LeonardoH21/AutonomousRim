# Revisão de combate melee — 12/10/2026

## Problemas e correções

- Um alvo preferido sem aproximação viável impedia tentar outra ameaça. Agora são avaliados até quatro candidatos, com acesso, apoio, força local e exposição revalidados no mapa.
- A intenção da dupla era perdida entre análises e o segundo melee não influenciava a primeira decisão. As atribuições válidas persistidas em `CombatOrder.MeleeTarget` alimentam a coordenação; o próprio colono não conta como parceiro e quem está recuando não promete apoio.
- Um deslocamento antigo podia ser preservado antes de verificar a rota atual. A aproximação passa pela avaliação de segurança e reserva de células; manter um deslocamento exige o mesmo destino validado. O ataque começa na célula adjacente planejada, preservando aquecimento e ataques nativos já em andamento.
- Contato imediato podia ser recusado pelo limite do ponto inicial. A defesa adjacente acessível é avaliada antes do limite de aproximação.
- A exposição incluía tiros do próprio alvo durante o combate corpo a corpo. O cálculo mantém os tiros durante a chegada e o fogo cruzado de outros inimigos, mas remove os tiros do alvo durante o contato planejado.
- Um parceiro saudável já atacando o alvo pode sustentar uma aproximação de apoio de até 32 células, com vantagem mínima de 1,2 e saúde do atacante de pelo menos 80%. Continuam valendo acesso, saúde, exposição, número de inimigos, perímetro e recuo. Uma aproximação sem esse apoio mantém o limite de 24 células.
- Recusas têm motivo registrado, incluindo saúde, vantagem, distância, exposição, ausência de acesso ou espaço. O teste registra a explicação para investigar espera justificável e bloqueios reais.

## Ambiente e reprodução

RimWorld 1.6.4633, todas as DLCs instaladas, cópia isolada de `TESTE V3.rws`, cenário Enhanced World, Cassandra/Medium e seis colonos originais preservados (Alpha, Bravo, Charlie, Delta, Echo e Foxtrot). Suas habilidades, traços, implantes e fatores do cenário são conservados; não são substituídos por personagens artificiais com todas as habilidades 20. Dois melee usam espada/maça e armadura de placas; quatro ranged usam armas mistas, coletes e todos usam capacetes.

Velocidade nativa 3×. Spawns e edição do terreno/equipamento apenas na preparação das arenas. Danos, movimento, ataques e resultados de combate são nativos, sem cura, imortalidade ou morte artificial para declarar vitória. Os testes de batalha não validam recuperação hospitalar posterior.

```powershell
.\scripts\MeleeValidation.ps1 -IncludeProbe
# Retomar uma etapa específica sem repetir todas:
.\scripts\MeleeValidation.ps1 -FirstCase 4 -LastCase 6
```

Compilar produção e RuntimeChecks, instalar antes de iniciar e fechar o jogo do usuário. Cada etapa recebe perfil e processo próprios em `.tools/validation`, mantém `manifest.json`, `Player.log`, `result.txt`, `melee-metrics.txt` e save de resultado. O executor recusa binários inconsistentes ou alterados entre etapas. Derrota, retirada e combate sem desfecho são registrados como resultados distintos; concluir o processo não significa vencer.

SHA256 do save original: `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD`.

Produção validada: `B9A13FE6BDA85F8B4D93B162C04FEBF9265174C5BE5C83317D90210BFCA39187`.

RuntimeChecks correspondente: `FF652AA54B2A6836458849050AEC6AA35596DE31208E5B54BF6B99046D5FE597`.

## Critérios observados

- Prova específica: dois melee originais rejeitam o alvo distante inadequado, tomam `AttackMelee` contra a alternativa próxima e ambos causam dano nativo. Também verifica defesa acessível além do ponto inicial.
- `pairContactSamples`: amostras em que os dois executam `AttackMelee` no mesmo inimigo ativo, ambos adjacentes. É cooperação física, não apenas uma etiqueta de flanco.
- `damageByMelee`: aumento do registro nativo de dano de cada melee desde a preparação. Os campos antigos de dano/disparos no resultado completo incluem o histórico do save.
- `idleMeleeSamples`: espera defensiva fora de recuo; pode incluir decisões seguras justificadas.
- `stalledApproachSamples`: posição repetida entre amostras de deslocamento, indicador para investigar, não prova de imobilidade durante todo o intervalo.
- `firstMeleeDamageTick`: primeiro aumento observado do dano da dupla, amostrado a cada 60 ticks.

Foram realizadas comparações exploratórias com a versão anterior e revisões intermediárias. A primeira revisão aumentou a espera contra arqueiros e motivou a correção da exposição. Outra revelou uma recusa a 24,7 células com vantagem 28,47 enquanto o parceiro já lutava, motivando a aproximação de apoio. Execuções interrompidas deliberadamente para atualizar os binários são marcadas `INTERRUPTED_FOR_UPDATE` e não contam como testes aprovados. A aleatoriedade nativa de geração e combate impede tratar essas execuções como benchmark determinístico ou garantia para todas as partidas.

## Resultados da versão final

| Cenário | Resultado | Ticks | Mortos / derrubados | Espera melee¹ | Contato conjunto² | Dano Bravo / Foxtrot³ |
|---|---|---:|---:|---:|---:|---:|
| Arqueiros, 6 × 6 | VITORIA | 1807 | 0 / 0 | 0 | 9 | 139 / 134 |
| Campo aberto, 6 × 9 | VITORIA | 2427 | 0 / 0 | 4 | 0 | 54 / 179 |
| Portas e cobertura, 6 × 4 | VITORIA | 1224 | 0 / 0 | 4 | 3 | 128 / 13 |
| Dois flancos, 6 × 10 | VITORIA | 4587 | 0 / 0 | 41 | 0 | 95 / 103 |
| Animais agressivos, 6 × 8 | VITORIA | 2626 | 0 / 0 | 3 | 0 | 223 / 122 |
| Mechanoids, 6 × 6 | VITORIA | 3618 | 0 / 1 | 1 | 0 | 162 / 62 |

¹ Amostras de `Wait_Combat` de melee fora de recuo, a cada 60 ticks; não equivalem a segundos de ociosidade injustificada. No cenário de dois flancos houve recusa por ferimentos e perímetro de apoio.

² Amostras de dois ataques nativos ao mesmo inimigo ativo e adjacente. A cooperação física foi observada contra arqueiros e nas passagens; os outros cenários tiveram alvos separados, recuos e proteção de setores. Não se afirma que a dupla sempre concentrou ataques em todos os combates.

³ Dano novo de cada melee, descontado o histórico do save. Ambos causaram dano em todos os seis combates.

Seis vitórias e zero mortos ao término dos combates, com um colono derrubado contra mechanoids. Houve feridos em todas as arenas; a recuperação hospitalar posterior não faz parte da aprovação desta rodada. O log do teste de mechanoids registra início de estabilização de Bravo por Charlie, mas a preparação encerra a automação ao declarar o desfecho, portanto não comprova tratamento concluído.

O teste específico da dupla passou nas três verificações. Com a produção final, ambos deram dano nativo na ameaça compartilhada em 30 ticks (Bravo 26, Foxtrot 21), com duas amostras de contato conjunto.

Proveniência integral e métricas: `outputs/CombatMelee/2026-10-12/results.json`. Logs e saves finais permanecem nos perfis abaixo:

- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-after-balanced-20261012-053923-182`
- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-after-mixed-20261012-054039-748`
- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-doors-advantage-20261012-054202-347`
- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-two-flanks-disadvantage-20261012-054302-699`
- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-manhunters-20261012-054501-382`
- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-mechanoids-20261012-054637-920`
- `C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\melee-cooperation-probe-20261012-053824-747`
