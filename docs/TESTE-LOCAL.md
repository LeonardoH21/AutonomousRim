# Teste local — AutonomousRim

## Resultado em 05/10/2026

O mod foi compilado contra as DLLs da instalação RimWorld 1.6.4633, sem erros ou avisos, e instalado em `game/Mods/AutonomousRim`. O hash da DLL instalada confere com o arquivo compilado.

O teste de inicialização usou uma pasta de dados separada, Harmony, Core e todas as cinco DLCs. O jogo registrou `[AutonomousRim] Loaded successfully.` e completou o carregamento sem exceções ou erros XML. O primeiro teste sem gráficos produziu erros de texturas do motor; a verificação foi repetida com gráficos habilitados. As instâncias de teste foram encerradas.

Os saves e a configuração de mods do usuário não foram alterados. O código, os scripts e os testes são versionados no Git; o SDK local, os logs, os saves de teste e os arquivos compilados ficam fora do repositório.

## Verificação dentro de uma colônia

1. Abra o jogo e ative **AutonomousRim** no menu de mods, mantendo **Harmony** ativo. Reinicie conforme solicitado pelo jogo.
2. Abra uma colônia e clique em **Rim AI** na barra inferior.
3. Confira nomes, saúde, habilidades, equipamentos, traços e recursos. Deixe o jogo rodar por dez segundos na velocidade normal e confirme que o painel atualiza após mudanças na colônia.
4. Com modo desenvolvedor ativo, procure `[AutonomousRim] Scan:` no `Player.log`. Anote qualquer erro associado ao mod.

Uma segunda etapa automatizada gerou uma colônia nova com `-quicktest` no perfil separado. O scanner leu três colonos, sendo dois aptos para combate, e executou repetidamente sem exceções. A aparência do painel, a qualidade das sugestões e as transições de ameaças durante um combate ainda precisam de validação no jogo.

O teste ampliado dentro do RimWorld também passou: identificou uma arma proibida no chão, uma roupa no chão e roupas em cadáver; confirmou demanda real de comida; criou e removeu ordens de refeições/abate; marcou caça elegível; restaurou prioridades originais e preservou um ajuste manual. O complemento de validação foi removido após o teste. Ainda faltam testes prolongados de produção/consumo, interrupção sob ameaça e ciclo completo de salvar/carregar com automação ativa.

## Limites desta versão

- O mod observa a colônia e oferece duas opções por mapa no painel Rim AI: alimentação automática e prioridades de trabalho. Controle de combate, troca automática de equipamentos e construção ainda estão no roadmap.
- O valor de combate é uma heurística inicial baseada em habilidade, capacidades físicas, saúde e DPS teórico da arma. Ainda não simula cobertura, armadura do alvo, genes ou habilidades especiais.
- A reserva usa comida armazenada aceita pelos colonos, respeita políticas alimentares e descarta itens deteriorados. O consumo é calculado pela necessidade de fome dos colonos, incluindo seus modificadores. A estimativa não prevê deterioração futura, visitantes, animais, acesso físico ou a distribuição ideal de estoques entre dietas diferentes.
- O painel classifica hostis ativos entre humanos, mecanoides, animais e outros, além de distinguir armas de alcance e corpo a corpo. O risco é heurístico; a identificação do incidente e da tática de invasão ainda não foi implementada.
- As sugestões de armas são consultivas e verificam acesso e possibilidade de equipar. Não trocam equipamentos nem disputam/reservam uma arma entre vários colonos. Explosivos e ataques especiais ficam fora das sugestões.
- Foram aprovadas 24 verificações de cálculo, risco, caça e prioridades, além da compilação sem erros ou avisos.

## Alimentação e prioridades automáticas

Ative as opções desejadas em **Rim AI**. A alimentação cria ordens de refeições simples e abate nas instalações existentes; não constrói bancadas nem planta novas áreas. Precisa de ingredientes, infraestrutura utilizável e colonos com os trabalhos habilitados. As ordens existentes do jogador são preservadas. O alvo inicial é três dias de refeições. A caça exige um caçador com arma de fogo, trabalho de caça ativo, alvo alcançável até 60 células e estação de abate. Seleciona apenas animais selvagens não predadores e sem chance de revanche da espécie, com no máximo duas caças pendentes. Nenhuma nova caça é marcada quando há hostis. Jobs já iniciados seguem o sistema de tarefas do jogo.

As prioridades consideram habilidades, paixões, saúde e distribuição de especializações; tarefas de emergência e medicina recebem atenção especial. Alterações manuais do jogador são mantidas. Desligar a opção restaura as prioridades que ainda pertencem à IA. O modo global de prioridades numéricas permanece ativado; se o jogador o desligar, a gestão de trabalho fica pausada.

Armas e roupas visíveis caídas no chão, inclusive itens proibidos e roupas em cadáveres, aparecem no inventário do painel. Itens proibidos são identificados, mas não liberados automaticamente; cadáveres não são despidos pela IA.

Os scripts `scripts/Build.ps1` e `scripts/Install.ps1` permitem repetir a compilação e instalação usando o parâmetro `-RimWorldDir`. O novo `scripts/SmokeTest.ps1` gera uma colônia temporária em `.tools/perception-test`, aguarda dois scans sem exceções e encerra a própria instância de teste. Ele usa apenas Harmony, o jogo base, as DLCs instaladas e AutonomousRim. Feche o jogo antes de executá-lo.
