# Cinco combates com quatro colonos — 06/10/2026

Foram executados cinco encontros independentes no RimWorld 1.6.4633, com Core, Harmony, AutonomousRim e complemento temporário de teste. O usuário confirmou incluir desvantagem. A versão de combate/emergência permaneceu a mesma: commit `bbe99f4`, DLL SHA-256 `BE0FE25A0CBACC2EF44911B20D0731A46E8911E81DD1B7083AF8D7FDF9AD2DE1`.

**Os resultados não foram satisfatórios: nenhum encontro terminou em vitória ou retirada segura dos quatro combatentes.** A suíte concluiu os cinco cenários sem exceções de execução; isso não significa aprovação do comportamento de combate. Não foram repetidos encontros para substituir resultados ruins.

## Condições

- Quatro colonos novos, saudáveis e sem traços por teste: dois melee e dois ranged. Skills de Shooting/Melee no nível 10; demais características nativas não foram uniformizadas.
- Todos com Flak Vest, capacete simples de aço, camisa e calça de tecido, qualidade Normal. Sem escudos ou armadura completa dos membros.
- Melee: espada longa e maça de aço. Ranged nos casos 1/3/5: assault rifle e pump shotgun; casos 2/4: bolt-action rifle e revolver.
- Inimigos sorteados com sementes registradas. Humanos com skills entre 6 e 12 e chances de colete/capacete; animais e mechs preservam características/equipamentos nativos.
- Emergência automática ligada, demais automações desligadas. IA pode lutar ou recuar; nenhum participante recebe cura, reposição de necessidades, alteração de skills ou equipamentos durante a luta.
- Terreno de arena preparado; abrigo de granito coberto com uma porta, sujeito à destruição nativa. Sem camas médicas ou medicina nesse abrigo. Esses encontros não validam uma colônia completa nem isolam a capacidade de resgate médico.
- Velocidade 3 selecionada continuamente. O próprio jogo pode reduzir temporariamente a velocidade durante combate.
- Um processo/perfil novo por cenário, sem carregar ou modificar saves/configurações do jogador.

Cada cenário encerra com neutralização dos inimigos, incapacidade de todos os colonos, retirada dos quatro móveis para abrigo com porta fechada após pelo menos 1.800 ticks, ou limite de 6.000 ticks. As incapacitações e mortes são fotografias do encerramento; os testes não comprovam sobrevivência posterior ao sangramento.

## Resultados

| Teste | Condição / inimigos sorteados | Ticks | Resultado | Mortos | Incapacitados | Feridos | Inimigos restantes |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: |
| 1 | Vantagem planejada, 4×3; cobertura/passagens; 2 autopistols e 1 faca | 6.000 | Sem desfecho | 0 | 1 | 4 | 2 |
| 2 | Equilíbrio numérico, 4×4; cobertura nos dois lados; 1 bolt-action, 1 maça e 2 facas | 6.000 | Sem desfecho | 0 | 1 | 4 | 2 |
| 3 | Desvantagem, 4×6; dois flancos sem cobertura inicial; 2 autopistols, 1 maça e 3 facas | 4.311 | Derrota | 0 | 4 | 4 | 5 |
| 4 | 4×6 manhunters: 2 wargs, 3 wolves e 1 wild boar | 3.346 | Derrota | 0 | 4 | 4 | 3 |
| 5 | 4×5 mechs: 3 lancers e 2 scythers | 2.018 | Derrota | 0 | 4 | 4 | 5 |

Houve **14 incapacitações entre 20 participantes independentes**. Todos tinham ferimentos ao final. Zero mortes no instante em que as observações foram encerradas não deve ser interpretado como proteção suficiente ou sucesso da retirada.

| Teste | Disparos aliados / inimigos | Dano aliado registrado | Amostras de cobertura | Interceptação | Retirada |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 2 / 48 | 86 | 143 | 11 | 274 |
| 2 | 11 / 13 | 159 | 48 | 11 | 277 |
| 3 | 3 / 44 | 121 | 1 | 2 | 105 |
| 4 | 9 / 0 | 302 | 52 | 11 | 78 |
| 5 | 14 / 24 | 122 | 22 | 1 | 57 |

Disparos/dano usam os records do motor nativo. Amostras são avaliações de função/posição a cada 60 ticks, não ações distintas. Cobertura calculada em direção a um animal não protege contra suas mordidas. As contagens não permitem atribuir todo ferimento a uma causa específica nem comparar diretamente a dificuldade entre inimigos diferentes.

## O que os testes mostram

1. A vantagem planejada de terreno e quantidade no caso 1 não foi aproveitada: apenas dois disparos aliados, frente a 48 inimigos. Posicionamento, linha de tiro, alcance e manutenção de fogo precisam de investigação conjunta; os counters não isolam uma causa única.
2. Houve interceptação e retirada em todos os cenários, porém nenhuma retirada cumpriu o critério de quatro colonos móveis e protegidos. Ordenar recuo não basta para demonstrar proteção da equipe.
3. A defesa falhou contra dois flancos, animais rápidos e mechs. Os quatro colonos foram incapacitados nesses cenários.
4. Colete e capacete não substituem defesa de membros, controle de distância ou cuidado dos feridos. O abrigo desta suíte não fornece suporte médico; não se deve concluir, com estes dados, que o resgate falhou apesar de existir cama/medicina segura.

As próximas correções devem priorizar fogo de apoio/posicionamento, proteção dos atiradores contra aproximação, rotas de retirada com alternativa de saída e cuidado dos combatentes feridos quando houver condições seguras. Este relatório registra o estado observado, sem alterar o executor de combate para mascarar os resultados.

## Evidências locais e repetição

Execute com o jogo fechado, depois de compilar o complemento de runtime:

```powershell
.\scripts\FiveCombatTests.ps1 -Disadvantage
```

Sem `-Disadvantage`, os casos 3/4/5 têm menos inimigos e o caso 3 não possui o segundo flanco. Não foi essa a configuração usada nesta rodada.

| Caso | Semente da seleção | Log em `.tools/combat-five/caseN` |
| --- | ---: | --- |
| 1 | 20271108 | Player-20261007-000934.log |
| 2 | 20271209 | Player-20261007-001103.log |
| 3 | 20271310 | Player-20261007-001240.log |
| 4 | 20271411 | Player-20261007-001417.log |
| 5 | 20271512 | Player-20261007-001538.log |

Cada perfil contém `result.txt` com métricas/equipamentos/sorteio completo e `Saves/AutonomousRim-Combate-Misto-N.rws` com o encerramento. A semente reproduz a seleção dos inimigos, não toda a geração nativa dos pawns, clima, mundo ou todos os resultados da simulação. São cinco observações, não uma estimativa estatística de taxa de sucesso. Logs e saves permanecem locais e não são adicionados ao Git.
