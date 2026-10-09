# Ambiente definitivo: AutonomousRim Enhanced World

Configuração solicitada em 09/10/2026: RimWorld 1.6.4633 rev1261, Harmony, AutonomousRim e todas as cinco DLCs. Cassandra Classic, **Medium / Adventure Story**, salvamento livre. Mapa inicial 325×325 gerado pelo jogo. Não é a antiga campanha de cinco colonos; seu checkpoint foi preservado.

## Saves disponíveis para carregar no jogo

Diretório: `C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\`

- `AutonomousRim_Enhanced_World_EliteSix_BASE.rws`: início limpo, antes dos minérios/plantas de validação; referência imutável dos próximos testes.
- `AutonomousRim_Enhanced_World_EliteSix_TESTE.rws`: mesma colônia inicial com as automações ativadas e velocidade 3×, para revisão manual. Não é uma base já concluída.

Cópias em `C:\Users\Administrador\Desktop\ModRimWorld\outputs\EnhancedWorld\`. Não sobrescrever esses arquivos com resultados de testes; usar cópias/novos nomes. Os saves exportados não dependem do observador RuntimeChecks nem do Prepare Carefully. Dependem do AutonomousRim para os tetos dos stats e da configuração de DLCs indicada acima.

BASE SHA256: `94B605291A81B328FC8C518B3962167222071638F5A9D090060B5473522062AF`.
TESTE SHA256: `7A10C151911AEC22398E823CE7FD75341C9C82725583E03565ABA164DBAA269C`.

## Seis colonos do pacote original

Alpha, Bravo, Charlie, Delta, Echo e Foxtrot, preservando habilidades, paixões, três traços, histórias, sexo, tipo corporal, idades, xenótipo Baseliner e **12 implantes por colono**. Não substituir por seis colonos com todas as habilidades 20. Nenhuma arma/roupa extra foi concedida aos colonos. Os itens normais no chão são os de Crashlanded vanilla; não usamos o antigo cenário local de oito colonos com recursos e equipamento adicionais.

O arquivo original usa `NameSingle`. Isso provocou `InvalidCastException` no gerador vanilla de parentes durante a geração do mapa, que espera `NameTriple` em humanos. A importação de testes mantém o mesmo apelido usando NameTriple, e há uma cópia compatível do preset. O ZIP e o preset original permanecem intactos. O jogo ainda pode aplicar a doença temporária de criptossono normal do cenário; não neutralizamos necessidades nem incidentes para os testes.

Preset original: `C:\Users\Administrador\Desktop\ModRimWorld\Tests\Fixtures\EliteSix\AutonomousRim_EliteSix_RW1.6_PC1.6.2.pcp`.
Preset compatível: `C:\Users\Administrador\Desktop\ModRimWorld\Tests\Fixtures\EliteSix\AutonomousRim_EliteSix_Compatible.pcp`.
Ambos copiados para `C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\PrepareCarefully\`. Para uma campanha nova pelo Prepare Carefully, carregar **AutonomousRim_EliteSix_Compatible**; o cenário sozinho não contém os perfis dos personagens.

## Fatores permanentes

| Stat | Fator do cenário | Alteração de teto |
|---|---:|---:|
| MiningYield | 5 | 1,25 → 100 |
| PlantHarvestYield | 3 | 1,5 → 100 |
| ConstructionSpeed | 2 | nenhuma |
| ResearchSpeed | 3 | nenhuma |

Somente os dois maxValue são alterados por `Patches/EnhancedWorldYieldCaps.xml`. Os quatro fatores são partes vanilla `ScenPart_StatFactor` e ficam serializados no save. Não alteramos minValue, fórmulas, MiningSpeed, PlantWorkSpeed, WorkSpeedGlobal, GlobalLearningFactor, ConstructSuccessChance, ResearchSpeedFactor, custos de materiais, custos das pesquisas ou raid points. Os implantes continuam influenciando cada stat antes do multiplicador.

O patch de teto é carregado com AutonomousRim em qualquer campanha; os fatores 5/3/2/3 só pertencem ao cenário Enhanced World. Sistemas que ignoram MiningYield não recebem multiplicador por este patch. Deep drilling vanilla consulta MiningYield. **Plantas de drogas usam DrugHarvestYield**, um stat separado com teto 1,5; ele permanece inalterado conforme a restrição expressa de modificar somente dois tetos. Portanto não prometer colheita 3× para drogas sem uma autorização/revisão específica desse stat.

Cenário fonte: `C:\Users\Administrador\Desktop\ModRimWorld\Scenarios\AutonomousRim_Enhanced_World.rsc`.
Cenário instalado: `C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Scenarios\AutonomousRim_Enhanced_World.rsc`.
Patch instalado: `C:\Users\Administrador\Downloads\RimWorld.v1.6.4633\RimWorld.v1.6.4633\game\Mods\AutonomousRim\Patches\EnhancedWorldYieldCaps.xml`.

## Validação executada

1. `enhanced-world-elite-six-compatible-20261009-221241-099`: importação de seis perfis, checagem dos fatores efetivos contra o mesmo colono sem fator, trabalho nativo Mine/Harvest, save/load. Bravo extraiu **226 aço** (40 × MiningYield 5,65) e Charlie colheu **23 arroz**, dentro do arredondamento esperado (6 × PlantHarvestYield 3,8985). Minério/planta foram fixtures descartáveis criadas após salvar BASE e removidas pela recarga do BASE; não foram exportadas.
2. `enhanced-world-playable-reload-20261009-221448-730`: carregou o BASE já exportado sem componentes de teste serializados; verificou cada skill/paixão/traço, história, sexo/idade/tipo corporal, cada implante na parte correta, cenário, difficulty Medium, salvamento livre e fatores. Ativou os módulos nativamente e salvou TESTE em 3×.
3. Materiais da parede continuam 5; MicroelectronicsBasics continua 3000 pontos. Construção/pesquisa verificadas pelo fator efetivo do stat; esta etapa não mede uma campanha inteira ou todas as tecnologias.

A primeira geração `enhanced-world-elite-six-20261009-221100-717` foi reclassificada como **INVALID_MAP_GENERATION_ERROR**, apesar de medir fatores corretamente, pelo erro NameSingle. Não usar seu save. Logs e manifests ficam em `.tools/validation/`.

Ao ativar todos os módulos no teste de recarga houve avisos `started 10 jobs in one tick` em Alpha/Delta, alternando HaulToCell/HaulToContainer. O ambiente está criado e carregável, mas esses avisos são uma pendência real para a próxima etapa de análise de tarefas; não equivalem a aprovação integral do funcionamento da IA.

## Uso nos próximos testes

Usar sempre cópias do BASE/TESTE com esses seis colonos e fatores para campanhas e testes de gameplay novos. `scripts/EnhancedWorldTest.ps1` é a entrada padrão: recompila antes de iniciar, aborta se o build falhar e não altera habilidades para 20. As fixtures históricas de contratos continuam registradas separadamente; seus resultados de cinco colonos não aprovam o ambiente de seis.

```powershell
& .\scripts\EnhancedWorldTest.ps1 -Stage Construction
& .\scripts\EnhancedWorldTest.ps1 -Stage Integrated
# Continuar um checkpoint do MESMO ambiente, mantendo colonos/recursos/ticks:
& .\scripts\EnhancedWorldTest.ps1 -Stage Integrated -ResumeSave 'CAMINHO_ABSOLUTO_DO_CHECKPOINT.rws'
```

`-Stage Setup` cria e valida um novo baseline isolado, sem sobrescrever seus saves pessoais; exportação para o usuário exige preservar o destino existente. O runner de campanha carrega BASE por padrão. Não usar peacefultrial nesse runner: a configuração definitiva solicitada é Cassandra/Medium. Não declarar o objetivo de validação completa encerrado: construção integral, combate, economia orgânica, progressão militar e inverno ainda requerem suas etapas.

## Arquivos de implementação criados/alterados

Todos sob `C:\Users\Administrador\Desktop\ModRimWorld\`:

- `C:\Users\Administrador\Desktop\ModRimWorld\Patches\EnhancedWorldYieldCaps.xml`
- `C:\Users\Administrador\Desktop\ModRimWorld\Scenarios\AutonomousRim_Enhanced_World.rsc`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\Fixtures\EliteSix\AutonomousRim_EliteSix_RW1.6_PC1.6.2.pcp`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\Fixtures\EliteSix\AutonomousRim_EliteSix_Compatible.pcp`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\Fixtures\EliteSix\EliteSix_design.json`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\Fixtures\EliteSix\README_EliteSix.txt`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\AutonomousRim.RuntimeChecks\EnhancedWorldSetup.cs`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\AutonomousRim.RuntimeChecks\EnhancedWorldChecks.cs`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\AutonomousRim.RuntimeChecks\CourtyardTrialSetup.cs`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\AutonomousRim.RuntimeChecks\ModularConstructionTrial.cs`
- `C:\Users\Administrador\Desktop\ModRimWorld\Tests\AutonomousRim.RuntimeChecks\PrisonChecks.cs` (guarda anterior/import necessário para compilar; sem nova execução desta fixture)
- `C:\Users\Administrador\Desktop\ModRimWorld\scripts\Install.ps1`
- `C:\Users\Administrador\Desktop\ModRimWorld\scripts\FunctionalStage.ps1`
- `C:\Users\Administrador\Desktop\ModRimWorld\scripts\EnhancedWorldTest.ps1`
- `C:\Users\Administrador\Desktop\ModRimWorld\docs\AMBIENTE-ELITE-SIX.md`
- `C:\Users\Administrador\Desktop\ModRimWorld\docs\VALIDACAO-ETAPAS.md`
- `C:\Users\Administrador\Desktop\ModRimWorld\.gitignore` (saves grandes permanecem locais; fontes do ambiente ficam versionadas)
