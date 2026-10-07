# Construção normal do núcleo com quatro pátios

O teste solicitado usa a planta de `BASE-NUCLEO-PATIOS.md`: paredes compartilhadas de uma célula, quartos com interior 6×6, cozinha e abate separados com interior 4×4, sala de baterias 4×8, corredores em cruz e quatro pátios de cultivo. Os interiores e corredores cobertos recebem madeira; os caminhos abertos recebem concreto.

## Regras do teste

- Cinco colonos iniciais com todas as habilidades em nível 20, conforme autorização do usuário. Traços, histórias, incapacidades, saúde e necessidades seguem o jogo.
- Cenário nativo Crashlanded, dificuldade Peaceful e mapa nativo de floresta temperada plana, com 325×325 células. Essas são opções de início; o terreno gerado não é transformado.
- Velocidade 3×, inclusive depois de carregar um ponto de retomada.
- Sem recursos gerados, pesquisas concluídas artificialmente, preenchimento de necessidades, construção instantânea ou alteração de trabalho/custo dos objetos.
- Árvores são cortadas/colhidas, rochas são mineradas e sucata é desmontada pelos trabalhos normais dos colonos.
- O teste solicita os 14 quartos da planta completa. A geração normal do mod solicita quartos conforme a população.

## Executor e observador

Na primeira tentativa prolongada, `SolarPanels` chegou naturalmente aos 600 pontos, mas a construção parou em 879/4.961 elementos durante recuperação. A reserva chegou a zero e Lawman morreu no dia 21. Essa tentativa não passou e foi preservada localmente como `CourtyardFailed-Day21.rws`; nenhum colono foi ressuscitado ou substituído. A segunda tentativa recomeçou de `CourtyardStart`, com os mesmos cinco colonos, recursos e condições iniciais.

As correções seguintes mantêm Cooking, armazenamento e caça segura disponíveis durante recuperação sem ameaças imediatas; roupas continuam suspensas. O bloqueio global de recuperação considera pacientes e ferimentos, enquanto humor e descanso individuais são atendidos por prioridades e agenda pessoais. A pesquisa necessária tem um especialista reservado quando há ao menos dois dias de comida. A fila elétrica prioriza os cabos reais entre geradores e freezer antes dos ramais secundários. Todas essas ações continuam usando trabalho e custos nativos. O observador encerra a tentativa imediatamente se perder um dos cinco colonos e exige exatamente cinco na aprovação final.

`CourtyardBasePlanner` escolhe uma posição de solo firme e valida os locais reais das paredes, móveis e pisos. Árvores, rochas mineráveis e estruturas neutras desmontáveis são aceitas como obstáculos removíveis. Construções e zonas existentes do jogador são preservadas. O plano reserva os setores para permitir crescimento dentro do núcleo.

A execução usa os limites normais do mod: até três projetos ativos, dezoito obras pendentes e seis novos blueprints por ciclo. Coleta, transporte, construção, cobertura, alimentação, descanso e pesquisa continuam sob as regras nativas.

O observador de teste apenas configura os cinco colonos inicialmente, acompanha objetos/terrenos reais e salva a partida. Ele não conclui obras. A aprovação final exige todos os objetos e pisos planejados, coberturas e zonas presentes, e os cinco colonos iniciais vivos, de pé e no mapa.

## Estado da validação

A segunda tentativa expôs um problema na inicialização do observador: a automação padrão executava um ciclo antes da configuração dos cinco colonos e criava sete zonas de um plano menor. Ao substituir as referências pelo plano completo, essas zonas ficavam órfãs e geravam transporte e plantio longe do núcleo. A inicialização agora desliga a construção antes desse primeiro ciclo. Para o save inicial isolado antigo, uma migração usa a exclusão nativa de zonas e refaz o plano antes de emitir qualquer obra; itens, plantas, terreno, colonos e pesquisas permanecem como estavam. O log confirmou a retirada das sete zonas e a retomada com os mesmos cinco colonos. O checkpoint da segunda tentativa foi preservado, e a aprovação final continua pendente.

Em 07/10/2026, a geometria completa passou na validação nativa: medidas internas, partições compartilhadas sem conflito, colocação de móveis e células de interação. A compilação passou sem erros/avisos e os 35 testes existentes de políticas passaram.

A construção prolongada está em andamento. O primeiro ponto de retomada contém 73 dos 4.921 elementos únicos concluídos após meio dia de jogo. Uma disputa de reservas durante desmontagem levou à inclusão da verificação nativa `HasJobOnThing` antes de solicitar trabalhos. A retomada também foi corrigida para manter 3× após carregar o save.

A fila de apoio prioriza as camas necessárias à população antes de móveis secundários; os quartos adicionais continuam em seus projetos. A preparação do terreno não ocupa vagas de construção nem bloqueia módulos sem relação com seus obstáculos. Células já limpas deixam essa fila para evitar que a grama que cresce novamente prolongue indefinidamente a preparação.

Após essas correções, o ponto de retomada próximo do terceiro dia contém 184 elementos concluídos. Estoque, cozinha, abate, apoio inicial, depósito de medicamentos, plantação inicial e despejo foram concluídos. A inspeção do XML confirmou seis camas nativas construídas — cinco requeridas e uma adicional já encomendada antes da mudança de fila. O refeitório e os primeiros quartos continuam em obra. Isso ainda não é a aprovação final da planta.

No quarto dia, a janela nativa `Dialog_NamePlayerFactionAndSettlement` pausou a simulação apesar de a velocidade selecionada continuar em 3×. O observador passou a verificar a pausa real, aceitar os nomes gerados pelo jogo e retomar a velocidade. O log confirmou essa janela no tick 258.200, seguido de novo avanço até 305 elementos e quatro quartos completos. Não houve alteração de recursos ou obras para contornar a pausa.

A conferência do piso incluiu mais 40 células sob portas internas e entradas do corredor coberto: madeira com custo e trabalho normais. O plano passou a ter 4.961 elementos únicos. A prioridade dos quartos de reserva foi reduzida; freezer, energia, hospital e pesquisa passam a depender dos quartos necessários à população, e não de todos os quartos futuros. Paredes novas nos locais das antigas ruínas deixam a lista de demolição, preservando essas construções e liberando os registros antigos de designações.

No oitavo dia, foram medidos 544 elementos concluídos e cerca de 4,2 dias de comida. Freezer, energia e pesquisa estavam ativos; a coleta nativa já havia aumentado o aço disponível. O teste completo permanece em andamento.

O teste também expôs espera por pesquisas. A rota passou a incluir os pré-requisitos reais dos objetos planejados e pode trocar uma pesquisa escolhida pela própria IA para liberar uma obra, mantendo o progresso anterior e respeitando escolhas manuais. Um especialista saudável pode dedicar-se a essa pesquisa enquanto outro atende pacientes; urgências de comida, incêndios e recuperação continuam protegidas. O identificador nativo `Batteries` foi corrigido no alvo complementar.

No décimo segundo dia, trabalhos nativos `Research` foram observados e o save registrou 168,47 pontos efetivamente estudados em `SolarPanels`, com 838 elementos construídos. Nenhuma pesquisa foi concluída artificialmente. Ainda faltam obras, pesquisas e acabamento antes de exportar o save final.

Esse avanço inicial não comprova conclusão da base nem sobrevivência prolongada. O save final só será entregue após a conferência de todas as obras reais.

## Repetir ou retomar

Compile o mod e o projeto `Tests/AutonomousRim.RuntimeChecks`, instale o mod com o jogo fechado e execute:

```powershell
.\scripts\CourtyardConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -TimeoutSeconds 14400
.\scripts\CourtyardConstructionTest.ps1 -RimWorldDir "C:\Path\To\RimWorld" -Resume -TimeoutSeconds 14400
```

O perfil isolado fica em `.tools/courtyard-construction`. O observador salva `CourtyardCheckpoint` a cada 6.000 ticks. Criar `checkpoint-request.txt` nesse perfil solicita um salvamento e uma pausa normais para reinício. `CourtyardFinished` só é produzido quando a validação final passa. Esses arquivos locais não são enviados ao Git por padrão.

Depois da aprovação final, `scripts/ExportCourtyardSave.ps1 -CopyToGameSaves` remove apenas os componentes do observador e a referência ao seu mod na cópia de exportação. O resultado `AutonomousRim-Nucleo-Completo.rws` mantém a colônia como foi salva e é copiado para a pasta normal de saves do jogador. O script recusa exportação antes da confirmação de conclusão.
