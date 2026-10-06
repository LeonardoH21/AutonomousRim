# Revisão da execução de construção — 6 de outubro de 2026

O pedido desta revisão está centrado em construção pelas regras normais: reduzir ociosidade e filas bloqueadas, com meta aproximada de 40% de melhoria. Intervalos menores não demonstram, por si só, esse ganho. O desempenho deve ser medido em ticks de jogo, comparando entregas equivalentes.

## Problemas encontrados

- O executor antigo parava no primeiro projeto incompleto, incluindo um módulo cancelado ou bloqueado.
- O orçamento exigia todos os materiais restantes do módulo antes de abrir qualquer blueprint.
- Toda construção dependia da leitura completa de 600 ticks; o planejamento era recalculado nesse mesmo ciclo.
- Uma área grande e contínua era obrigatória mesmo para o depósito inicial. Entulho também invalidava o local.
- A zona de estoque só era criada depois de paredes e teto. O depósito não podia receber itens durante sua própria construção.
- Prioridades empatadas permitiam transporte secundário enquanto os materiais das obras aguardavam.
- As verificações anteriores de layout aceleravam estruturas e forneciam recursos no cenário descartável. São fixtures de geometria/mecânicas; não demonstram uma partida normal autônoma.

## Execução atual

O planejador cria e guarda os módulos. Replaneja quando muda a população, a disponibilidade de pesquisa relevante ou quando uma busca sem local precisa ser repetida. O executor revisa os trabalhos a cada 120 ticks, sem recriar o plano. A leitura geral passa de 600 para 360 ticks; allow permanece limitado a duas pilhas a cada pelo menos 600 ticks. O cooldown de equipamento permanece protegido.

Até três projetos ocupam slots de execução, limitados também pelos construtores habilitados. Cada ciclo abre até seis blueprints, no máximo dois por projeto, até seis pendentes por projeto, e mantém até dezoito blueprints/frames distintos. Obras compartilhadas reutilizam o mesmo blueprint/frame. Uma etapa bloqueada cede o slot sem apagar seu investimento. O limite é do agendador da IA: trabalhadores nativos podem terminar frames preservados enquanto um módulo aguarda slot.

Estados persistentes: planejado, ativo, aguardando materiais, bloqueado, pausado e concluído. A HUD mostra estado, prioridade e motivo. Uma obra pendente desaparecida ou alterada pausa o módulo, permitindo que outros prossigam. O motivo é apresentado como possível cancelamento/remoção, sem afirmar quem a removeu. Estruturas já concluídas têm esse histórico salvo: perdas posteriores bloqueiam o módulo e pedem revisão, sem serem confundidas com cancelamento manual. Se forem reconstruídas normalmente, o executor volta a reconhecer sua conclusão.

O orçamento financia cada lote e desconta todas as obras já abertas, inclusive do jogador. Materiais inacessíveis não financiam novos trabalhos. Etapas secundárias preservam 60 de madeira, 50 de aço e dois componentes; estruturas essenciais podem usar essa reserva. Pisos e decoração permanecem depois das entregas funcionais.

O estoque recebe uma zona utilizável antes da conclusão da cobertura. Isso não protege itens do tempo: a HUD distingue armazenamento utilizável de depósito concluído. Se o bloco compacto não cabe, salas independentes são procuradas sem demolir a base nem alterar terreno.

Em inícios sem camas suficientes, um módulo crítico de apoio antecipa as camas, bancadas da cozinha, mesa, assentos e xadrez já previstos no layout definitivo. Paredes e cobertura continuam sendo construídas separadamente. Não são móveis adicionais nem gratuitos: o apoio usa os custos, habilidades e trabalhos reais, e os demais módulos reconhecem/reutilizam as peças no mesmo local. Isso reduz espera por descanso, preparo de comida e recreação. Materiais compartilhados não são contados duas vezes na coleta.

## Trabalhadores, coleta e recuperação

Com gestão de trabalho habilitada, construção recebe prioridade 1 entre os trabalhadores capazes; auxiliares também recebem Hauling 1 e o construtor principal Hauling 2. Assim, auxiliares podem terminar estruturas simples financiadas antes de transporte secundário, com os requisitos nativos de habilidade. Alterações manuais posteriores são preservadas. Sem esse controle habilitado, as prioridades existentes continuam sendo requisito para a execução.

Quando um frame essencial já tem todos os materiais e somente o construtor principal cumpre sua habilidade mínima, a gestão tenta distribuir mineração e corte entre os demais trabalhadores. Cozinha pode passar para outro cozinheiro com habilidade pelo menos 3. O construtor recebe cozinha 2 nessas situações; comida urgente e caça em andamento permanecem protegidas. O executor só considera interromper preparo de comida menos prioritário com uma reserva de pelo menos um dia. Isso evita concentrar cozinha, coleta e eletrônicos no único colono capaz de terminar esses frames, sem editar habilidades nem acelerar seu trabalho.

Um WorkGiver da IA seleciona trabalhos nativos nas obras ativas. Em Hauling, sua prioridade interna 120 fica abaixo de reabastecimento (140) e acima de cadáveres (90) e transporte geral (15). Em Construction, prioridade 85 fica abaixo de cobertura (100). Isso muda a seleção dentro de um trabalho já habilitado, sem mudar as regras de execução. Trabalhos de entrega, acabamento de frames e cobertura são obtidos pelos WorkGivers nativos, com `forced=false`. Ordens emitidas não ignoram forbidden, áreas, reservas, habilidade, custos ou transporte. Sono, comida, recreação, saúde, horários de descanso e ordens manuais são protegidos. Móveis com qualidade são direcionados primeiro ao melhor construtor disponível; não há garantia de exclusividade contra o agendador nativo.

Falta real de madeira, aço ou componentes pode gerar até quatro novas designações por ciclo, limitadas a dezesseis pendentes. Árvores e minérios precisam existir, estar visíveis e acessíveis, dentro das áreas permitidas, com trabalhadores e trabalho habilitados. A madeira usa `HarvestPlant`, a ordem nativa de colher madeira; `CutPlant` apenas remove vegetação da obra. Mineração usa a designação na célula do minério. A IA não cria o recurso: colheita e mineração fazem a coleta normal. Esses trabalhos em andamento não são interrompidos pelo executor, evitando perder progresso de coleta. Combustível dos geradores entra nessa demanda.

Uma obra aguardando o restante dos materiais continua recebendo entregas parciais e acabamento de frames já financiados pelos WorkGivers nativos. Esperar o orçamento total não paralisa os investimentos existentes. Ao faltar um item essencial maior, sua quantidade é preservada nos lotes seguintes, evitando que pequenas paredes consumam cada nova pilha antes de uma bancada conseguir ser financiada.

Durante as obras, o transporte de uma reserva alimentar usa o mesmo Hauling nativo, antes de entregas de construção dentro do WorkGiver da IA. Refeições prontas com ingrediente humano só podem receber allow automático quando todos os colonos consumidores têm o traço/preceito que as aceita; políticas alimentares continuam sendo verificadas. Essa exceção contempla suprimentos gerados nativamente para colônias com esses preceitos. A IA não cria refeições humanas, não libera carne humana crua/cadáveres e mantém a exclusão desses ingredientes nas ordens de cozinha/abate.

Após 1.800 ticks sem mudança registrada em um projeto iniciado, o diagnóstico consulta progresso dos frames, entregas, teto, vegetação, acesso, habilidade, materiais proibidos e reservas. Só reservas sem trabalho atual ou enfileirado válido são liberadas. Falhas de trabalhos da IA têm tentativas registradas e cooldown crescente. Um frame substituído pelo blueprint pela falha nativa é reconhecido; uma obra desaparecida por cancelamento não é recriada automaticamente.

A cozinha usa células de interação distintas para fogão e bancada de abate. Planos antigos com essa sobreposição podem deslocar somente a bancada ainda não emitida para uma posição válida dentro da própria cozinha, atualizando também o apoio inicial compartilhado. A validação usa a regra nativa de posicionamento; móveis construídos, frames, ordens emitidas e cancelamentos manuais não são deslocados. Custos repetidos do mesmo material (madeira como material e custo explícito da bancada) são somados antes da reserva. Não há demolição/relocalização automática de módulos estruturais bloqueados, nem correção por criação de recursos. Um impedimento que precisa do jogador é exibido enquanto outras entregas continuam.

## Freezer e energia

Com pesquisa nativa disponível, novos blocos incluem freezer 6×6, dois resfriadores a −4 °C e estoque alimentar com prioridade maior. Não há ventilação ligando o freezer ao corredor aquecido. A geração incorpora o consumo extra. O fallback de salas independentes pode incluir freezer com gerador/conexão próprios.

Energia pode ser construída antes de todos os quartos terminarem. Temperatura exterior perigosa eleva sua prioridade quando já existe uma estrutura fechada. Reabastecimento continua sendo tarefa nativa. Antecamara, isolamento duplo, ampliação térmica automática, outras fontes de energia e adaptação completa a biomas extremos continuam pendentes.

## Teste sem aceleração de obras

`scripts/NormalConstructionTest.ps1` usa um perfil isolado em `.tools/normal-construction`. O addon ativa os controles da IA e observa a partida Crashlanded. Não altera materiais, necessidades, pesquisas, habilidades, terreno ou progresso dos frames. A velocidade escolhida é a terceira velocidade normal do jogo; comparações usam ticks, não tempo de relógio.

O início é salvo antes de ligar as funções. A opção `-Peaceful` escolhe o preset normal Pacífico antes desse save, para isolar a construção de invasões; o jogo usa integralmente as regras desse preset. Não serve como prova de sobrevivência em dificuldades com invasões. A comparação sempre carrega o mesmo save e dificuldade para os dois executores. `-Peaceful -LoadStart` muda somente esse preset nativo antes de salvar novamente o início, com os mesmos colonos, habilidades e suprimentos. Uma colônia capaz de concluir eletrônicos precisa cumprir os requisitos nativos: construção 4 para o gerador a madeira e 5 para aquecedores/resfriadores. Um ensaio adicional começou com níveis 2/0/1 e avançou nas estruturas simples, mas aguardou habilidade para os equipamentos; não foi contado como aprovação completa. Treinamento automático de construção ainda não está implementado. `-LoadStart` repete a mesma situação; `-Baseline` carrega esse início e usa uma cópia congelada do executor anterior, com o mesmo plano e coleta nativa. Essa comparação isola a revisão da execução, não representa todos os sistemas da versão histórica.

O teste habilita também alimentação, autoequipamento, prioridades e allow, para que caça/preparo de comida possam atender necessidades durante as obras. O perfil temporário adia autosaves para fora da duração do teste: a tela nativa de carregamento pode bloquear Unity em batch mode, que não desenha OnGUI. Saves explícitos de início/fim e checkpoints continuam ativos. `-LoadCheckpoint` retoma uma execução interrompida pelo save nativo, sem reabastecer recursos ou necessidades. Diálogos obrigatórios aceitam nomes sugeridos nativamente; propostas opcionais usam sua saída/rejeição comum. Fechar essas janelas sem confirmar sua opção nativa provocava repetidas pausas, corrigidas no observador. As configurações e saves habituais do jogador permanecem separados.

O critério de aprovação exige depósito coberto, abrigo/quartos, camas, cozinha, freezer abaixo de zero e alimentado, e geração básica, concluídos pelos colonos. O teste grava resultados e o save final. Os arquivos do addon são removidos ao terminar; saves/configuração habituais do jogador não são utilizados.

A primeira partida, no preset Rough, concluiu depósito, três quartos, camas e cozinha; confirmou também a recuperação nativa da bancada com interação sobreposta. Um invasor incendiou e destruiu partes da base antes do freezer/energia terminarem. Essa partida não aprovou a infraestrutura completa nem pode comprovar a meta de desempenho.

A partida de recuperação no preset Pacífico passou: depósito, três quartos/camas, cozinha, freezer energizado a −3,3 °C, corredor e geração/climatização concluídos. Log local: `Player-20261006-153304-812.log`, em `.tools/normal-construction`; resultado `elapsedTicks=1104480`. Esse tempo inclui checkpoints de versões intermediárias e não é uma medição de velocidade da versão final. Não houve criação de recursos, edição de habilidades/necessidades ou conclusão artificial de obras. A comparação a partir do mesmo início permanece em validação. A meta de 40% só pode ser confirmada pelo resultado medido e não é uma promessa para qualquer mapa, população ou situação.
