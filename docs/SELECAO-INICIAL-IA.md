# Escolha automática da equipe inicial

Na tela padrão de personagens, o botão **Escolher com IA** aparece abaixo da lista de colonos. Abra a janela e clique em **Buscar equipe por até 5 min**. A IA conta quantos personagens o cenário permite levar e quantos estão disponíveis, avalia os existentes e gera novos candidatos usando o gerador nativo. **Parar e manter equipe** encerra antes do prazo; **Voltar** também interrompe e mantém o resultado. A partida continua sendo iniciada pelo jogador.

A pontuação cobre construção, cozinha, cultivo, fabricação, medicina, pesquisa, social e mineração. Considera níveis, paixões, trabalhos incapazes, capacidades, velocidade de trabalho, saúde, condições crônicas/dependências, idade e risco de traços. Traços suprimidos não recebem a penalidade explícita de piromania. As capacidades e estatísticas nativas incorporam efeitos de genes/traços/saúde; não existe conhecimento completo de regras privadas de todos os mods.

Há um bônus de distribuição de especialistas distintos, evitando contar o mesmo ótimo colono como seis trabalhadores simultâneos. A avaliação de combate segue a divisão do mod: até dois melee e os demais ranged, com habilidades e incapacidade de violência. Suplentes também entram na pontuação. Aptidão é uma nota comparativa, não nível nativo nem garantia de produtividade.

A busca usa melhorias locais com trocas entre selecionados e reserva e rolagens alternadas por todos os slots. Mantém melhorias entre equipes válidas; pode reduzir a nota para corrigir uma equipe inicial inválida para o cenário. Candidatos rejeitados têm posses e relações descartadas por rotinas nativas, e as requisições de geração acompanham as trocas. A validação respeita o relatório nativo de cenário (estágios, espécies, xenótipos, mutants e skills exigidos) e os tipos de trabalho que exigem um colono capaz.

Nenhuma habilidade, traço, gene ou equipamento é editado pela seleção: apenas sorteio normal e escolha dos personagens. Os personagens atuais podem ser substituídos quando o jogador inicia a busca. O botão não inicia nada sozinho e o limite de colonos do cenário não é aumentado.

O prazo usa relógio real de até 300 segundos, com uma geração por atualização elegível, espaçamento mínimo de 50 ms e teto adicional de 5.000 gerações. A geração ocorre na thread principal: uma chamada nativa iniciada antes do prazo precisa terminar antes de parar. Geradores especialmente lentos de mods podem produzir pausas. A busca não garante o máximo matemático entre todas as combinações possíveis; entrega a melhor equipe encontrada pela heurística nesse tempo.

A janela é da tela nativa Page_ConfigureStartingPawns. Prepare Carefully/Character Editor e outras telas próprias não receberam adaptação nesta revisão. Tutorial e listas maiores que 40 candidatos são bloqueados. Fechar a janela interrompe; não existe busca em segundo plano durante a partida.

## Validação

Teste funcional em perfil isolado: oito personagens temporários, três selecionados e 64 gerações reais. Verifica nota não decrescente para grupos válidos, reserva, quantidade/identidades, posses, restrições de cenário, parada manual, limite de 300 segundos por relógio controlado e descarte nativo. Os seis personagens da colônia EliteSix carregada são preservados. O relógio controlado valida o prazo sem exigir cinco minutos completos de sorteio no teste; não é uma medição de FPS nem inspeção visual da janela.

Resultados: `outputs/StartingTeam/results-2026-10-13.json`.
