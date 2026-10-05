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

## Limites desta versão

- O mod observa a colônia. Controle de combate, troca automática de equipamentos e construção ainda estão no roadmap.
- O valor de combate é uma heurística inicial baseada em habilidade, capacidades físicas, saúde e DPS teórico da arma. Ainda não simula cobertura, armadura do alvo, genes ou habilidades especiais.
- A reserva de comida conta itens armazenados, comestíveis para humanos e permitidos à facção, usando consumo aproximado de 1,6 nutrição por colono/dia. Ainda não considera deterioração, genes, animais, visitantes ou ingestão individual.
- O painel classifica hostis ativos entre humanos, mecanoides, animais e outros, além de distinguir armas de alcance e corpo a corpo. O risco é heurístico; a identificação do incidente e da tática de invasão ainda não foi implementada.
- As sugestões de armas são consultivas e verificam acesso e possibilidade de equipar. Não trocam equipamentos nem disputam/reservam uma arma entre vários colonos. Explosivos e ataques especiais ficam fora das sugestões.
- Foram aprovadas 13 verificações de cálculo de DPS e limites de classificação de risco, além da compilação sem erros ou avisos.

Os scripts `scripts/Build.ps1` e `scripts/Install.ps1` permitem repetir a compilação e instalação usando o parâmetro `-RimWorldDir`. O novo `scripts/SmokeTest.ps1` gera uma colônia temporária em `.tools/perception-test`, aguarda dois scans sem exceções e encerra a própria instância de teste. Ele usa apenas Harmony, o jogo base, as DLCs instaladas e AutonomousRim. Feche o jogo antes de executá-lo.
