# Plano de base autônoma

Especificação de prioridades definida em 05/10/2026. Este documento orienta a próxima implementação; a construção inicial de quartos, estoque e cozinha foi implementada após esta especificação; plantio e fabricação de roupas continuam pendentes; energia/climatização pesquisadas já integram novos blocos compactos. Refeições/abate em bancadas existentes e autoequipamento já estão implementados.

## Dimensões e organização

- Quarto individual: **5×5 internos**, sem contar paredes; uma cama por colono, iluminação e climatização quando necessárias. Uma estrutura isolada ocupa 7×7 com paredes. Quartos adjacentes podem compartilhar paredes.
- Cozinha: **4×4 internos**, com fogão e bancada de abate juntos, conforme pedido do jogador. Reservar os espaços de interação das duas bancadas, porta e circulação antes de aprovar o encaixe.
- Estoque seco: módulo inicial 6×6 internos, expansível. Separar alimentos perecíveis, cadáveres, materiais e equipamentos por filtros. Materiais que não deterioram podem ter estoque externo temporário.
- Geladeira/freezer: módulo inicial 6×6 internos junto à cozinha. Aumentar conforme ocupação; resfriador com saída de calor para área externa sem teto. Estoque alimentar temporário não depende de energia.
- Refeitório/recreação: módulo inicial compartilhado 6×6 internos, com mesa, assentos e recreação básica. Separação em duas salas é uma expansão futura.
- Plantações: módulos de **6×6 células**; ampliar o número de módulos conforme demanda. Não exigir que todos os módulos estejam ocupados nem fixar um número independente da população.
- Hospital: módulo inicial 6×6 internos, com duas camas comuns marcadas para uso médico. Camas hospitalares especializadas dependem de pesquisa e materiais.
- Oficina: módulo inicial 6×6 internos, próximo do estoque e afastado da cozinha/hospital.
- Corredores: manter circulação, espaços de interação, acesso de manutenção e reserva para expansão.

Esses tamanhos são padrões de projeto. Em uma base existente, aproveitar salas e bancadas funcionais antes de criar outras. Não demolir a construção do jogador para impor o modelo.

## Ordem de execução normal

| Prioridade | Objetivo | Entrega mínima e dependências |
|---|---|---|
| 0 | Emergências | Resgate/tratamento, fogo, fome iminente e temperatura perigosa interrompem expansão. Não emitir construção em área com hostis. |
| 1 | Escolher local e preservar acesso | Avaliar terreno, fertilidade, obstáculos, perigos, suporte de construção e rotas. Reservar setores, corredores e entrada defensiva futura. |
| 2 | Sobrevivência inicial | Abrigo provisório coberto, lugar de dormir para todos, estoque alimentar protegido, mesa/assentos e produção de comida disponível. Usar bancada/fogueira provisória se o fogão definitivo estiver bloqueado. |
| 3 | Iniciar agricultura | Primeiro módulo alimentar 6×6 e módulos adicionais conforme demanda. Executar em paralelo ao abrigo; crescimento tem atraso até a colheita. |
| 4 | Quartos e estoque | Criar quartos 5×5 por colono e estoque seco. Concluir cama, porta e cobertura antes de decoração. Migrar do abrigo provisório apenas quando o quarto estiver utilizável. |
| 5 | Cozinha definitiva | Construir cozinha 4×4 e as duas bancadas, com acesso ao estoque de ingredientes e local de refeições. Aproveitar as ordens de comida já existentes. |
| 6 | Energia e refrigeração | Planejar fonte, consumidores prioritários e cabos; construir geração/conexão antes de depender de fogão elétrico/freezer. Freezer somente onde pesquisa, componentes, potência e condições térmicas permitem. |
| 7 | Recreação e cuidados | Concluir sala compartilhada e hospital. Camas médicas provisórias podem aparecer já na prioridade 0/2 quando necessárias. |
| 8 | Oficina e reposição de roupas | Construir bancada apropriada e fabricar faltas reais de vestuário, respeitando política, clima, habilidade e recursos. Necessidade urgente de proteção térmica pode antecipar esta etapa. |
| 9 | Perímetro e killbox | Construir perímetro progressivamente, preservando a entrada planejada; depois instalar corredor de armadilhas, cobertura e posições defensivas. |
| 10 | Melhorias | Materiais resistentes, expansão de estoques/freezer, quartos novos, hospital especializado, redundância energética e defesas adicionais. |

As prioridades não são uma sequência que precisa terminar integralmente antes da próxima. A IA pode semear enquanto construtores fazem quartos e cozinheiros produzem comida. Uma dependência bloqueada não paralisa tarefas independentes.

## Regras de agricultura

1. Consultar as plantas realmente disponíveis na instalação, requisitos de habilidade/pesquisa, temperatura, fertilidade, luz e período de crescimento. Plantas adicionadas por mods não devem ser assumidas equivalentes sem analisar suas definições.
2. Começar por alimento de colheita rápida adequado ao terreno; arroz é candidato inicial. Batata é candidata em solo de menor fertilidade; milho pode complementar reservas quando há tempo para maturação.
3. Adicionar algodão para roupas quando existe déficit de tecido e quando isso não compromete o plantio alimentar.
4. Adicionar healroot quando há falta de remédios e um agricultor capaz de plantar; reservar quantidade segundo população/uso. Não bloquear o hospital à espera dessa colheita.
5. Não dedicar as primeiras áreas a plantas de luxo. Madeira plantada depende de bioma, plantas disponíveis e horizonte de consumo; no início analisar coleta permitida e acessível.
6. Estimar demanda usando fome real, dietas, rendimento e tempo até colheita, perdas e mão de obra. Um módulo 6×6 não garante sustento para qualquer população.
7. Zonas existentes do jogador são preservadas. Áreas novas precisam caber no terreno e manter acessos. Pausar semeadura incompatível com estação/temperatura e explicar o bloqueio.

## Energia, cabos e freezer

- Dimensionar demanda das estruturas já existentes e das próximas tarefas; priorizar conservação de comida, temperatura vital e produção alimentar.
- Selecionar geração apenas entre opções pesquisadas, construíveis e adequadas aos recursos locais. Distinguir geração nominal de disponibilidade: combustível, vento, sol e interrupções.
- Reservar aço/componentes para o objetivo prioritário antes de abrir ordens de craft não essenciais. Não contar o mesmo material disponível em vários projetos simultaneamente.
- Planejar cabos conectados à rede, com rota válida e acesso. Preferir passagem por paredes/corredores; não emitir segmentos duplicados. Considerar conduíte oculto quando disponível e viável.
- Baterias são opcionais conforme fonte/pesquisa; prever área protegida e manutenção, sem depender de uma única fonte intermitente para conservação crítica.
- Só declarar o freezer pronto após cobertura, portas, energia, resfriador e temperatura efetiva adequada. Se não for possível refrigerar, reduzir estoques perecíveis e ajustar produção à demanda.
- Cada consumidor deve explicar sua dependência: por exemplo, “aguardando 2 componentes”, “pesquisa indisponível” ou “sem potência disponível”.

## Fabricação e comida

**Decisão de roupa:** verificar o que o colono precisa, a política de vestuário, equipamento vestido, peças acessíveis no chão e ordens já pendentes. Só fabricar se não existir alternativa utilizável nem produção já suficiente. Uma peça do chão não pode satisfazer simultaneamente a falta de vários colonos.

Usar receitas da bancada e materiais realmente disponíveis, respeitando habilidade mínima, pesquisa, biocodificação e políticas. Preferir couro/tecido com desempenho adequado ao clima e conservar recursos críticos. Produzir reposição limitada por déficit; não criar ordens infinitas de roupas. O autoequipamento existente recolhe as peças concluídas.

Necessidades urgentes de frio/calor e falta de roupa essencial precedem upgrades de armadura. Armaduras são projetos separados, com pesquisa, orçamento e papel de combate. Não iniciar fabricação sem trabalhador capaz; apresentar essa dependência no painel.

**Comida:** reutilizar a gestão de refeições simples e abate já implementada. A construção disponibiliza infraestrutura; a gestão alimentar cria/atualiza as ordens. Evitar ordens duplicadas e preservar ordens editadas pelo jogador. Ingredientes, dietas, acesso, combustível/energia e cozinheiro apto fazem parte da avaliação. Receitas melhores entram após estabilidade de reservas, não apenas porque estão desbloqueadas.

## Perímetro e killbox

1. Reservar a entrada antes de fechar a base; incluir estoque, oficina, áreas vitais e plantações conforme orçamento e terreno. Manter acesso externo seguro e possibilidade de expansão.
2. Construir trechos do muro por etapas; não selar a última rota da colônia. Preferir material resistente ao fogo quando disponível, com defesa provisória enquanto faltam recursos.
3. Projetar corredor de aproximação e armadilhas a partir das regras reais de construção e pathfinding. Verificar legalidade de cada armadilha, espaçamento, interação e caminho de rearme. Não assumir que qualquer padrão geométrico é permitido.
4. Dar aos colonos rota de manutenção alternativa ao trajeto das armadilhas. Controlar zonas/caminhos sem apagar restrições do jogador. Evitar fogo amigo e bloqueio de portas.
5. Dimensionar área de tiro conforme alcance das armas disponíveis, cobertura dos defensores e entradas. Posições defensivas planejadas não substituem controle de combate, ainda não implementado.
6. Testar que existe caminho de entrada para ataques convencionais; portas de serviço não devem criar um atalho público preferível. Validar novamente depois de cada etapa construída.
7. Repor armadilhas consumidas segundo orçamento de material e urgência. Reparar brechas antes de melhorias decorativas.

A killbox busca direcionar ataques convencionais. Invasores que rompem paredes, escavam ou caem dentro da base podem ignorá-la; serão necessárias respostas táticas e defesas internas próprias.

## Contrato do planejador

Cada objetivo registra ID persistente, setor, prioridade, dependências, custo reservado, células ocupadas, responsável quando necessário, progresso e motivo de bloqueio. Estados: proposto → viável → emitido → em construção/produção → concluído; alternativas: bloqueado ou cancelado.

Conclusão depende de estrutura/tarefa efetivamente pronta, não apenas da emissão de blueprint. Rescans não podem duplicar paredes, zonas, móveis, cabos ou bills. Se faltar material, emitir somente a etapa financiável e avaliar coleta/mineração permitida; não abrir dezenas de projetos sem orçamento.

A automação será opcional por mapa. O painel deve exibir fila e motivos das escolhas. Identificar exclusivamente blueprints, zonas e ordens pertencentes à IA; desligar cancela tarefas pendentes ainda não editadas e preserva construções prontas e alterações do jogador. Expansão deve ter limites por ciclo e respeitar áreas permitidas.

## Ordem de implementação e validação

1. Scanner de infraestrutura/terreno e planejador de objetivos com custos, dependências e fila visível, inicialmente sem construir.
2. Executor opcional de abrigo, quartos, estoque e plantações. Validar paredes/portas/teto, footprint das bancadas, interação e acessos.
3. Cozinha, rede elétrica, freezer e integração com ordens de comida. Validar conexão, calor do resfriador e ausência de duplication após rescans.
4. Recreação, hospital e craft de roupas por déficit. Validar políticas, materiais, bills do jogador e utilização das roupas produzidas.
5. Perímetro e armadilhas. Validar rotas e legalidade antes de permitir fechar o perímetro; depois desenvolver posições/controle de combate.
6. Testes em colônia temporária com poucos recursos, pesquisas bloqueadas, bases existentes e múltiplos colonos; salvar/carregar durante construção, desligar automação e preservar edições manuais. Nenhuma validação deve usar saves normais do jogador.

## Referências de mecânicas

- [Bancada de abate](https://rimworldwiki.com/wiki/Butcher_table): o abate produz sujeira. Mantemos as duas bancadas na cozinha conforme pedido, mas limpeza e separação por divisória podem melhorar a segurança alimentar; separar é opção futura, não alteração automática do projeto.
- [Estruturas defensivas](https://rimworldwiki.com/wiki/Defense_structures): certos ataques ignoram killboxes.

Os limiares de orçamento, tamanho do freezer e quantidade de camas médicas são parâmetros iniciais de projeto, sujeitos a medição em testes.


## Implementação inicial e controles

A HUD Rim AI possui botões fixos para base automática, itens/autoequipamento, alimentação e prioridades, allow gradual dos itens do chão, além de prévia do plano e desligamento geral. A fila atual começa pelo estoque coberto, seguido de quartos, cozinha e sala social. Novos quartos reservam cama de casal e incluem cabeceira, cômoda e vaso. A implementação constrói quartos/estoque/cozinha/sala social em blocos compactos, corredor de duas células com duas saídas e, com pesquisa disponível, ventilação, geradores/cabos, aquecedores/resfriadores e iluminação. Conforto e pisos seguem as etapas básicas; pedra é selecionada quando há estoque suficiente. Veja [layout, estilo e limites de climatização](LAYOUT-COMPACTO.md). Cobertura e zona de estoque seguem a conclusão real, com lotes limitados e orçamento de material. Agricultura, freezer, hospital, craft e defesa continuam pendentes; obtenção automática de recursos, pesquisas e reforma de projetos antigos ainda não são executadas.
