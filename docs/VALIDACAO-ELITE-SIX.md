# Validação por etapas — Enhanced World / Elite Six

Novos ensaios usam Alpha, Bravo, Charlie, Delta, Echo e Foxtrot originais, todas as DLCs, Cassandra/Medium, salvamento livre e velocidade 3×. Fatores de cenário: mineração ×5, colheita ×3, construção ×2 e pesquisa ×3. Ensaios antigos de cinco colonos não aprovam este ambiente.

| Etapa | Estado comprovado | Ainda necessário |
|---|---|---|
| Ambiente, perfis, fatores e recarga | PASS nativo; BASE/TESTE exportados | Deep drilling e diferentes culturas não foram exercitados |
| Fila de construção e transporte | PASS nativo; parede e madeira reais; fila manual preservada | Estabilidade da campanha inteira |
| Construção e sobrevivência | Parcial: 408 paredes, 1.234 tarefas, cerca de 60 horas | Todos os cômodos, móveis, pisos e 20 dias |
| Alimentação e reservas | Parcial: cozinha, refeições e estimativa em dias | Ajustes por escassez/excesso, freezer cheio, inverno e reserva estratégica |
| Pesquisa, oficina e equipamento | Parcial: duas bancadas usadas; arco fabricado/equipado | Armaduras, checkpoints tecnológicos e distribuição completa |
| Comércio local e segurança | PASS: troca, reservas, controle manual, fogo, save/load e desligamento | Produção agrícola comercial orgânica |
| Prisão | Ensaio adaptado para seis; execução em andamento | Captura, tratamento, conversão, recrutamento, soltura e rebelião no novo ambiente |
| Comércio orbital e caravana | Adaptados; não executados neste ambiente | Compra real, entrega e retorno dos seis originais |
| Cinco combates e recuperação | Adaptados; não executados neste ambiente | Vantagem/equilíbrio/desvantagem, melee/ranged, animais/mechs e atendimento |
| Prioridades, agenda, resgate e emergência | Há observação parcial e ensaios históricos | Cobertura específica no ambiente Elite Six |
| Inverno, energia e expansão | Pendente | Reservas, climatização, expansão e recursos com demanda real |

## Continuidade da campanha

Checkpoint imutável anterior aos ensaios isolados:

`C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\enhanced-queued-construction-fix-resume-20261009-232335-917\Saves\BeforeEliteMechanicStages.rws`

SHA256: `89199D6E7E1E4BA1BF0B11ACAD2F0DFD40864722406366C49ECEC1EE2EB29A8C`.

Não voltar ao BASE para reiniciar a campanha integrada. Retomar este checkpoint, conservando início, seis referências originais, recursos e resultados do observador. As fixtures de comércio/prisão/combate usam cópias separadas do BASE; seus itens e instalações controlados não entram na colônia de sobrevivência.

```powershell
& .\scripts\EnhancedWorldTest.ps1 -Stage Integrated -ResumeSave 'C:\Users\Administrador\Desktop\ModRimWorld\.tools\validation\enhanced-queued-construction-fix-resume-20261009-232335-917\Saves\BeforeEliteMechanicStages.rws'
```

O jogo deve estar liberado entre ensaios. Registrar resultado e checkpoint antes de mudar de etapa; reprovações permanecem registradas mesmo após correção. Resultados completos, hashes e limitações constam em `docs/VALIDACAO-ETAPAS.md`.

## Evidência nova de comércio

`enhanced-commercesafety-20261009-232953-198`: sete PASS e DONE; seis IDs conferidos contra BASE no `CommerceRoundtrip.rws`. Executou código `7008217`. As guardas adicionais de identidade/fatores de `201753e` serão exercitadas nas etapas seguintes.
