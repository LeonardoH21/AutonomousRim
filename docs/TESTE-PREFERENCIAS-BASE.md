# Preferências de base e verificação — 6 de outubro de 2026

## Implementado

- Freezers novos usam −2 °C; dois aquecedores ficam perto das entradas do bloco compacto, em 20 °C.
- Estoque geral sem alimentos; freezer somente alimentos humanos/animais com prioridade maior. Cadáveres ficam no despejo, não no freezer.
- Despejo externo 3×3 próximo à cozinha/oficina: somente pedaços de pedra e corpos animais, sem escória metálica ou corpos humanos.
- Hospital 5×5: duas camas comuns marcadas Medical e três células somente para medicina, em Critical.
- Oficina 5×5 com corte de pedras/costura manual conforme pesquisas; estoque de armas 4×4 posterior aos essenciais.
- Prateleiras pequenas após as estruturas essenciais, com pesquisa de Móveis complexos. Usam filtros do setor e prioridade acima do chão. Configuração única preserva edições posteriores.
- Comida x4 em 20/20: receita mista preferida quando houver ingredientes e Cooking 6, simples como alternativa. Limite compartilhado de refeições e exclusão de ingredientes humanos.
- Roupas em 3/3: calça, camisa e parka/duster conforme temperatura. Ordens nativas dependem de recursos e trabalhadores; allow também considera materiais de costura necessários.
- Allow de até oito pilhas por ciclo, preservando acesso, ameaças, necessidade e reproibição manual.
- Coleta nativa também atende aos módulos posteriores e às reservas de materiais das melhorias.

## Resultados

Build de produção e complemento de testes: zero erros/avisos. As 35 verificações de regras passaram.

O cenário explicitamente solicitado usa três colonos Crashlanded com as 12 habilidades inicialmente em 20, sem traços ou trabalhos incapazes e em terceira velocidade. Necessidades, deterioração de habilidades, materiais do cenário/mapa, transporte, custos, pesquisas e construção continuam nativos. Nenhuma criação de recursos ou conclusão instantânea de obras foi usada nesta rodada.

O teste funcional completo foi retomado de checkpoints durante as correções. Log final: `.tools/skilled-construction/Player-20261006-182431-871.log`. Marcadores `PREFERENCES PASS` e `PASS` confirmam estoque, três quartos com camas, cozinha, sala social, freezer, corredor, geração/climatização, hospital, oficina, sala de armas, medicina e prateleiras. Freezer no final: −1,7 °C, termostatos em −2 °C. Conforto dos quartos também está concluído no save; pisos ainda estavam em execução. O contador de 484 é de tarefas e inclui aliases compartilhados, não 484 edifícios distintos.

Foi encontrado um conflito entre lâmpada e cama médica. Planos novos colocam luzes fora de camas/bancadas e validam colisões entre módulos, além da validação nativa do terreno. O checkpoint recuperou a cama ainda não emitida dentro do próprio hospital, sem demolir a lâmpada ou mover obras financiadas. Bancadas de costura ainda não emitidas têm recuperação equivalente.

Validação adicional de plano novo e zonas nativas na DLL final: `.tools/skilled-construction/Player-20261006-185112-640.log`, `PASS`. Verificou reservas sem sobreposição, posicionamento das luzes, dois aquecedores, termostatos −2 °C e exclusão/inclusão real de refeições, ração e feno nos setores corretos. Projetos antigos com resfriadores ainda não configurados também adotam −2 °C; ajustes já configurados ficam preservados. Essa checagem abre somente blueprints limitados; não é outro teste de base concluída.

Costura e salvamento: rodada final em `.tools/skilled-construction/Player-20261006-184656-445.log`, com `PASS` para três ordens de roupa 3/3, estabilidade durante ticks, acompanhamento após salvar/carregar e remoção ao desligar. O teste encontrou e corrigiu a diferença entre o filtro inicial amplo de receitas de costura e os ingredientes efetivos gravados pelo jogo; assinaturas agora comparam somente ingredientes permitidos pela receita. Também corrigiu a remoção de ordens de comida ainda usadas por jobs: tarefas automáticas são encerradas antes de excluir a ordem, preservando comandos forçados do jogador. Camisa usa o def nativo `Apparel_CollarShirt`. Ingredientes de origem humana, incluindo couro, ficam excluídos das novas ordens.

SHA-256 final instalado: `C3DAE6E68AF34BC30DC2681BB370E68C1E59E6061B3C8FF29C6D23443A4B54CB`.

**Limites:** ordens e configurações de produção foram verificadas; esta rodada não prova produção contínua de cada roupa nem da receita mista durante estações do ano. Dois aquecedores não garantem capacidade térmica em todos os biomas. Airlocks e expansão térmica automática permanecem pendentes. A rodada usa habilidades 20 e retomadas entre versões: seus 252480 ticks não são benchmark limpo nem evidência de ganho de 40% com colonos comuns.

## Saves e repetição

Em `.tools/skilled-construction/Saves`:

- `AutonomousRim-Teste-Skill20-Inicio.rws`: três colonos, todas as habilidades em 20 e zero traços; automações inicialmente desligadas.
- `AutonomousRim-Teste-BasePronta.rws`: infraestrutura funcional concluída, com acabamento de pisos ainda em execução.

`scripts/ExportSkilledFixture.ps1` cria essas cópias removendo apenas componentes/metadados do complemento de testes. Não altera colonos, recursos ou construções. Os arquivos exportados não dependem de RuntimeChecks; precisam de Harmony, AutonomousRim e das DLCs listadas no save. Logs/saves ficam locais, não são versionados no Git.

A cópia de base pronta é atualizada a partir de `ConstructionFinishedUpdated.rws` após a rodada de salvamento/costura, com filtros atuais e as três ordens de roupa. Isso configura somente a colônia de teste gerada; zonas de partidas do jogador não são reescritas.

```powershell
.\scripts\NormalConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -SkilledFixture -Peaceful -TimeoutSeconds 2400
.\scripts\NormalConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -SkilledFixture -LoadStart -Peaceful -PlanOnly -TimeoutSeconds 180
.\scripts\NormalConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -SkilledFixture -CraftOnly -TimeoutSeconds 180
```

O perfil normal e o baseline continuam separados em `.tools/normal-construction`. O complemento é instalado temporariamente e removido após cada teste.
