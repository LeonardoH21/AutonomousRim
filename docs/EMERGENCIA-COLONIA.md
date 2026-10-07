# Emergência da colônia

**Emergência automática** começa ligada. O detector permanece observando cada mapa da colônia mesmo quando a resposta automática está desligada. A resposta atua por mapa, preservando a rotina de outros assentamentos sem ameaça. O painel mostra fase, decisão, classificação, coordenadas das fontes/colonos, vulneráveis, resgates adiados e falta de abrigo.

O botão **Combate cooperativo** desligado também desliga a resposta de emergência, para não recrutar colonos contrariando essa escolha. Ligar **Emergência automática** permite novamente defesa/retirada automática durante perigo, mesmo sem combate contínuo habilitado. **Desligar todas** libera os controles da IA e restaura as prioridades que ela ainda possui.

## Detecção e fases

A cada 60 ticks, o scanner identifica humanos hostis, manhunters temporários/permanentes, insetos, mechanoides ativos, outras criaturas hostis, colmeias visíveis e estruturas hostis consideradas ameaças pelo motor nativo. Fontes hostis de condições de jogo também são consideradas. Mortos, incapacitados, animais pacíficos e fontes de combate dormentes não são tratados como atacantes ativos. Incêndios próximos de colonos ou estruturas da colônia também ativam emergência. O detector usa a situação atual do mapa; não deduz o evento apenas por uma carta de raid.

| Fase | Condição e comportamento |
| --- | --- |
| Danger | Ameaça imediata ou incêndio próximo; defesa/retirada, atendimento e suspensão de trabalho secundário |
| Securing | A ameaça desapareceu; exige 600 ticks consecutivos sem perigo e mantém as suspensões |
| Recovery | Medicina, descanso, alimentação, limpeza, transporte e cultivo retornam antes de pesquisa, produção secundária, decoração e expansão |
| Normal | Após pelo menos 3.000 ticks de recuperação e sem incapacitamentos, sangramento, ferimentos temporários ou necessidades críticas; prioridades anteriores restauradas e automações habituais retomadas |

Nova ameaça interrompe imediatamente confirmação/recuperação no próximo scan. As fases, tempos, prioridades originais e ordens próprias são serializados; um teste completo de carregar uma emergência em andamento ainda precisa de validação específica.

## Coordenação

O scanner localiza colonos e seleciona combatentes armados e capazes; marca incapacitados, colonos frágeis e não combatentes como vulneráveis. A decisão global considera número/força de inimigos e aliados. Estruturas hostis e desvantagem elevada favorecem retirada em vez de avançar para atacar. A estratégia usa o [controle cooperativo de combate](COMBATE-COOPERATIVO.md), com cobertura, passagens estreitas e retirada. Não constrói novas fortificações durante a batalha.

Suspende novos projetos de construção/decoração, coleta planejada, caça da IA, allow gradual, trocas de equipamentos e produção não urgente. Prioridades temporárias desabilitam trabalho secundário e interrompem trabalhos automáticos já em andamento. O otimizador nativo de roupas também fica suspenso. Bills existentes são preservadas; sua execução depende das prioridades permitidas. Cooking durante perigo somente é autorizado com estoque crítico e colono em local fechado e seguro; recuperação permite alimentação novamente. Ordens explicitamente dadas pelo jogador não são canceladas.

Ordens de trabalho nativas passam por uma checagem de rota durante a emergência. Caminhos com fogo e exposição próxima a hostis são rejeitados; combate e movimentação tática têm suas próprias avaliações. Apagar incêndios continua usando a aproximação nativa. Esse filtro é conservador e pode deixar trabalho pendente em vez de enviar alguém por uma área perigosa.

Resgate usa o trabalho nativo para carregar um incapacitado até uma cama, exigindo cuidador capaz, cama válida, acesso/reservas e rotas seguras. Não tenta resgate exposto perto de inimigos. Vulneráveis sem ordens manuais podem ser enviados a salas fechadas acessíveis; o caminho não pode avançar para mais perto das fontes hostis ao escapar de uma área já exposta. Sem abrigo/rota/cama, o painel informa o impedimento. Não há teleporte nem garantia de conseguir salvar um colono cercado.

Prioridades são uma camada temporária separada das prioridades habituais. Mudanças manuais são preservadas. A comparação usa o valor numérico realmente armazenado, porque o modo de caixas de seleção do RimWorld apresenta todo trabalho habilitado como prioridade 3; isso não deve ser confundido com uma edição manual.

## Validação nativa

Compile/instale o mod e o complemento `Tests/AutonomousRim.RuntimeChecks`, feche o jogo e execute:

```powershell
.\scripts\CombatTest.ps1 -EmergencyChecks
.\scripts\CombatTest.ps1
```

O perfil `.tools/emergency-tests` é isolado dos saves/configurações do jogador. A suíte prepara entidades/terreno e controla fontes e lesões para testar transições; não é um teste de vitória em raids. Ela verifica classificação, bloqueios globais, supressão de otimização de roupas, transporte real até cama médica, rejeição de rota exposta (incluindo arma com alcance superior a 25 células), retorno gradual, nova ameaça durante recuperação, manutenção da recuperação enquanto há lesão temporária e restauração sem perder uma edição manual. A suíte de combate inclui emergência global no cenário de retirada.

### Resultado em 06/10/2026

Suíte final de emergência: `Player-20261006-235841.log`, **11 grupos de verificações passaram**. Inclui classificação dos cinco tipos de ameaça, exclusão de fauna pacífica, resgate até cama, rejeição de rota perigosa, retomada/interrupção da recuperação, manutenção por lesão temporária e restauração das prioridades/edição manual. Não houve exceção ou erro de referência na execução.

Regressão final de combate: `Player-20261006-235954.log`.

| Cenário | Ticks | Mortos | Incapacitados | Com ferimentos | Resultado |
| --- | ---: | ---: | ---: | ---: | --- |
| Passagem estreita, 3 contra 1 | 299 | 0 | 0 | 1 | Ameaça neutralizada; interceptação |
| Cobertura, 3 contra 2 | 692 | 0 | 0 | 1 | Ameaças neutralizadas; 22 amostras de cobertura |
| Retirada com emergência global, 3 contra 9 | 1.800 | 0 | 0 | 3 | Todos dentro do abrigo com porta fechada; ameaças permanecem |

O terceiro cenário comprova retirada por aquele intervalo, não vitória nem defesa indefinida. Ferimentos incluem os preexistentes e a lesão intencional. Os 35 checks existentes passaram; compilação sem erros/avisos. A DLL instalada corresponde à verificada: SHA-256 `BE0FE25A0CBACC2EF44911B20D0731A46E8911E81DD1B7083AF8D7FDF9AD2DE1`. Nenhum save/configuração do jogador foi alterado pelos testes.

## Limites

A força inimiga é uma estimativa, não uma simulação de todos os resultados possíveis. O modo não evacua para outro mapa, não forma caravanas automaticamente e não garante segurança quando não há abrigo. Portas/abrigos podem ser destruídos. A detecção não antecipa inimigos ainda não materializados de eventos/drop pods nem efeitos especiais desconhecidos de mods. Raids completas de cada DLC, desempenho em grandes colônias, resgates sob fogo e aparência visual da HUD ainda precisam de testes mais amplos.
