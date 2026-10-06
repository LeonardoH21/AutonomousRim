# Reprodução do save TESTE DO MOD

## Falha reproduzida

Save original: `TESTE DO MOD.rws`, RimWorld 1.6.4633, cinco colonos e dificuldade Peaceful já definida pelo jogador. O arquivo foi salvo no tick 34, antes de terminar a chegada dos colonos.

Com todas as automações ativadas, a versão anterior permaneceu com zero projetos e zero estruturas após 6.000 ticks. Os colonos ficaram em tarefas de espera/passeio. O allow liberou pilhas; portanto, nessa reprodução, ele não estava totalmente inativo. Entretanto, sem projetos, não existia demanda de materiais de construção.

Causa: `BasePlanner.Plan` retornava imediatamente o resultado do planejador em anel, inclusive quando ele não encontrava uma clareira inteira de 84×64 células. Nunca chegava aos planejadores compacto e de salas individuais.

## Correção

- Tentar a planta em anel primeiro; quando não cabe no terreno, executar a alternativa compacta ou salas individuais. Planos em anel já salvos continuam preservados.
- Criar arroz inicial em lotes de 6×6, aproximadamente um lote por dois colonos, quando a alternativa não inclui cultivo. Esse cultivo não espera a conclusão de todos os quartos.
- Bills de comida contam itens por unidade, inclusive receitas de quatro refeições; ingredientes crus não satisfazem a meta de refeições prontas.
- Reservar um agricultor/cozinheiro apto quando há cultivo ou refeições pendentes, reduzindo a concorrência da construção por esse colono.
- Corredores aguardam as estruturas dos cômodos, mas não outros corredores. A chegada escalonada dos colonos criou dois blocos compactos; a dependência circular entre seus corredores bloqueava também os pisos.
- Salvar marcações de mineração por posição, pois a compressão nativa do mapa não conserva os IDs individuais dos minérios.

## Protocolo

`scripts/SavedColonyTest.ps1` copia o save para `.tools/saved-colony`, mantém a configuração local de mods e executa um observador de teste separado. Ativa as automações e usa `TimeSpeed.Superfast`, a terceira velocidade normal do jogo. Não altera atributos, saúde, recursos, dificuldade nem multiplicadores de ticks. Janelas que interrompem o relógio são registradas e fechadas durante o teste; as sugestões nativas de nome são aceitas.

O objetivo é chegar ao tick 1.200.235, equivalente a 20 dias completos após a chegada inicial no tick 235. A execução registra tarefas dos colonos, necessidades, reservas, projetos e estruturas a cada 6.000 ticks. A presença de estruturas é medida no próprio jogo, sem completar obras artificialmente. O observador salva `AutonomousRim-Teste-Progresso` diariamente; uma retomada com `-SourceSave` usa a mesma data final, sem reiniciar a contagem dos 20 dias.

Uma primeira execução corrigida chegou a 15,8 dias, com cinco colonos vivos e 481 estruturas, mas o processo encerrou antes do salvamento final. Esses 15,8 dias são evidência no log, não um teste completo nem progresso recuperável. A execução seguinte reinicia do arquivo original, inclui a correção dos corredores e adiciona checkpoints diários.

O Hospitality instalado apresenta um erro ao aplicar seu patch em `Pawn_MindState.Reset` durante o carregamento. Esse erro já estava presente no início e fica registrado no log; não foi removido da configuração do jogador.

## Resultado

Teste de 20 dias em andamento. O resultado final e o save exportado serão registrados após o término.
