# TESTE A1 — diagnóstico de desempenho

## Causa confirmada e correção

O A1 contém sete colonos, 31.555 coisas no mapa e 91 projetos. Com as automações ligadas, o Allow chamava a análise completa de equipamentos a cada 60 ticks, mesmo sem equipamento proibido a liberar. A leitura geral da colônia repetia a análise. Para cada peça e colono, as políticas recalculavam o checkpoint de equipamento, a seleção de melee e as comparações com os loadouts de todos os combatentes. O custo cresce conforme os equipamentos e colonos se acumulam.

`EquipmentEvaluationScope` reaproveita essas respostas apenas durante uma chamada síncrona de `EquipmentPlanner.Plan`. O contexto termina por `Dispose`, inclusive em exceções, e restaura contextos aninhados. Não conserva decisões entre ticks, jobs, carregamentos, alterações de saúde/equipamento ou edições de políticas. Fora desse contexto, as funções mantêm o cálculo original. Nenhuma API de RimWorld/Unity foi movida para outra thread.

O Allow só calcula propostas de equipamento quando há armas/roupas proibidas no chão e colonos elegíveis. As verificações de acesso, reservas, políticas, roupas forçadas, peças deterioradas e segurança antes de emitir jobs continuam vigentes. O intervalo de resposta do Allow não foi aumentado.

## Medições

Cópias isoladas do mesmo A1, carregadas no tick 977147, velocidade 3×, janela visível em 1920×1080. As comparações longas usam 3.000 ticks de aquecimento e 8.000 ticks de medição. A versão original foi compilada a partir de `028a240` em uma pasta separada; a versão corrigida foi restaurada no jogo após a comparação. Os tempos são inclusivos, portanto não devem ser somados entre módulos. A instrumentação também consome CPU.

| Execução longa, perfil isolado | TPS | FPS | CPU em MapComponentTick / 8.000 ticks |
| --- | ---: | ---: | ---: |
| Original, IA ligada | 137,9 | 16,5 | 37.122,9 ms |
| Corrigida, IA ligada | 190,6 | 87,6 | 8.646,3 ms |
| Corrigida, repetição | 285,3 | 30,6 | 8.147,3 ms |

A redução de CPU da IA foi de 76,7% a 78,1%. O tempo médio do Allow caiu de 158,39 ms para 12,36–12,46 ms; a leitura geral caiu de 162,10 ms para 14,43–15,33 ms. Isso confirma o gargalo do mod independentemente da variação de FPS.

As amostras curtas de 2.000 ticks oscilaram: original 33,6 FPS/156,7 TPS; corrigida 22,7 FPS/224,5 TPS; original com automações desligadas 44,7 FPS/296,8 TPS. Essa variação motivou os testes longos; não há garantia de um FPS fixo em uma partida longa. FPS mede desenho, TPS mede a velocidade de simulação, e a execução em 3× distribui tempo entre os dois.

Durante os testes também havia `Albion-Online` usando aproximadamente 48–54% de um motor 3D da GPU e `AMDRSServ` usando 31–37% de um motor de vídeo. Não encerramos esses programas. Isso é um fator adicional na comparação de FPS, não prova de que tenham causado toda a lentidão. O perfil isolado contém Harmony, RimWorld, as cinco DLCs e AutonomousRim; a conferência final também usa os mods ativos do jogador: Hugslib, Smarter Deconstruction and Mining, Prepare Carefully e More Biotech Style Genes. Nessa execução corrigida, medimos 31,8 FPS, 288,3 TPS e 8.438,5 ms em MapComponentTick por 8.000 ticks; as verificações de equipamentos e preservação de colonos/camas passaram. Não foi executada uma comparação da versão original com essa lista completa, portanto esse resultado não mede sozinho o ganho nesse conjunto de mods.

## Verificação funcional e integridade

- Comparação nativa entre o planejamento original sem contexto e o otimizado: propostas, ordem, colonos, peças e pontuações idênticos, com `considerForbidden` ligado e desligado.
- Repetição após alterar temporariamente condição e permissão de uma roupa, restaurando ambas antes de rodar o mapa; os resultados continuam iguais.
- Repetição longa preservou os sete colonos e IDs/posições de todas as camas de quarto originais. Energia continuou concluída, freezer ativo.
- Regressão de construção: entrega de madeira e parede construídas por jobs nativos; manutenção da construção enfileirada; desligar remove a ordem da IA e preserva a manual; seis perfis originais retidos no fixture V3.
- O save original A1 não foi sobrescrito. SHA-256: `0A84A8E96A31C9E690E77EF4E16D0A01B7E66EF7193A853F419381191184835E`.
- DLL corrigida instalada: `ABB40BF6D6C989E99E958C553B046BA4F64CF74A85914B6ABF4CEA68EE88EBE0`.

Resultados, hashes e verificações por execução: `outputs/A1/results-2026-10-14.json`. Logs completos ficam em `.tools/validation/`. O teste não cobre horas de partida, todas as combinações de mods ou combates. O arquivo preexistente não rastreado `L1PerformanceChecks.cs` foi preservado e não integra este pacote.

## Reproduzir

Compilar o mod e o addon de `Tests/AutonomousRim.RuntimeChecks`, instalar com o jogo fechado e executar `scripts/FunctionalStage.ps1` com `-Stage a1-check -Flag autonomousrimaonebenchmark -SuccessMarker '[A1Benchmark] DONE' -ResumeSave '<caminho do TESTE A1.rws>' -Visible`. O addon carrega uma cópia em perfil separado. Acrescentar `-UsePlayerMods` para copiar apenas a configuração da lista de mods ativos para o perfil de teste; a configuração do jogador permanece intacta. Não adicionar o addon à partida normal.
