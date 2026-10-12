# Entrega B3 — componentes, combustível e aquisição

Continuação da cadeia de equipamento. Escopo: insumos de até oito Bills próprias de equipamentos pendentes, produção de componentes industriais/avançados, combustível de bancadas e integração das faltas sem fonte local à fila comercial existente.

## Comportamento

- Previsão conservadora de um lote por ordem, usando filtros e contagens das receitas nativas. Ingredientes alternativos preferem estoque compatível; não é uma alocação exclusiva nem o alocador completo de misturas do jogo.
- Produção industrial própria usa Bills nativas `Do until X`, com metas limitadas a 24 componentes industriais e oito avançados. O produto precisa de receita e bancada compatíveis. A expansão avançado → industrial → materiais lê ingredientes, quantidades e rendimento das receitas, sem valores fictícios fixados para ouro/plasteel/aço.
- Uma fonte local conhecida só é considerada viável se houver trabalhador habilitado, área permitida, alcance de 60 células, ausência de proibição/fog e caminho seguro. O executor de coleta anterior emite corte/mineração limitados; ordens manuais permanecem.
- Bills de componentes já pertencentes à defesa são lidas sem tomar controle, modificar quantidade ou desfazer suspensão. Bills manuais/editadas impedem uma substituição automática. Bills industriais próprias persistem com assinatura após save/load.
- Componentes viáveis participam da coordenação de fabricantes e orçamento do próximo lote; os diagnósticos anteriores acompanham seu progresso nativo.
- Bancadas com ordens próprias ativas solicitam madeira conforme o alvo nativo de combustível, somente quando `allowAutoRefuel` e a condição nativa de abastecimento permitem. A coleta e o abastecimento são executados por jobs normais; não inserir combustível diretamente na bancada.
- Demandas sem fonte local ou produção disponível entram na fila comercial existente por tipo de item, usando a mesma deduplicação, estoque, entregas em trânsito, reserva de prata e orçamento. Não iniciar uma negociação só por atualizar o diagnóstico; execução depende do módulo comercial habilitado e de oferta real.
- Emergência, fogo ou comida abaixo de três dias suspendem Bills industriais intactas. Desligar equipamento limpa o plano derivado e remove somente Bills próprias intactas; controles manuais são preservados.
- O diagnóstico mostra quantidade faltante, meta e rota: estoque/entrega, coleta local, fabricação, controle manual, checkpoint pausado ou aquisição.

## Limites

É uma cadeia limitada aos dois tipos nativos de componentes. Não é resolução universal de receitas, alocação exclusiva por peça, criação automática de novas bancadas ou garantia de compra. Cultivo de tecido, combustíveis diferentes de madeira e expansão industrial de mods ainda precisam integração específica. A fila comercial preexistente também mantém suas metas gerais de infraestrutura e equipamento; este pacote não elimina compras que ela já solicitava.

Planos/rotas são reconstruídos a cada ciclo industrial de 600 ticks. Bills e suas assinaturas são persistentes. Metas máximas limitam esta produção própria, mas não rebaixam metas de outras Bills do jogador/defesa. Recursos em trânsito reduzem a demanda prevista; não contam como ingredientes disponíveis ao job.

## Validação

Produção compila sem avisos/erros. Runtime sem erros, com aviso NU1900 do feed de auditoria NuGet indisponível. Fixtures pacíficas autorizadas em cópias isoladas do V4, usando os seis originais e pesquisa/bancadas/energia/aço concedidos somente no setup; sem novas concessões após o início dos ensaios nativos. HUD ainda sem revisão visual neste pacote.

Falha preservada `industry-contracts-20261011-234331-942`: nome incorreto da bancada na fixture (`TableFabrication`); corrigido para `FabricationBench` da versão instalada. Não é uma aprovação de gameplay.

Falha preservada `industry-release-contracts-20261011-234641-211`: após remoção administrativa de estoque, o contador nativo ainda refletia o estado anterior e ocultou a demanda de componentes. Fixture passou a atualizar `ResourceCounter` antes das verificações. A revisão não relaxou os critérios.

PASS `industry-native-cache-contracts-20261011-234953-402`: 11 contratos de limite/deduplicação, participação na coordenação, propriedade compartilhada com defesa sem mudança de meta/suspensão, expansão de componente avançado em materiais nativos, controle manual, integração comercial e desligamento.

DLL B3 testada `FC43252A7CCEE06D28EDF2B9642FB1BB806E45F36ABE8E3E159064295F318086`; runtime final B3 `758A102B6FA0A5DF6687C89287E469CD3779B315DFFFDC3BC943C1D14E8D645B`. Base `fb5d233` com alterações locais, sem novo commit/publicação. A revisão B4 subsequente tem evidências e hashes próprios.

- FAIL preservado `industry-components-native-20261011-235028-012`: seis componentes, nenhuma SMG em um dia; os atiradores ainda tinham equipamento anterior e a fixture não isolava uma demanda real de arma.
- FAIL preservado `industry-components-demand-20261011-235751-078`: remover todas as armas regrediu o checkpoint para zero. A fixture final fornece arcos básicos no setup para manter o primeiro checkpoint concluído e exigir a SMG.
- FAIL preservado `industry-components-basic-weapons-20261011-235926-049`: cinco componentes, coletes em produção, energia e ordem da SMG ativas; limite de um dia terminou durante o sono. Save `IndustrySupplyFailure` preservado. A fixture também passou a usar `Bow_Short` sem stuff, conforme a definição nativa.
- PASS `industry-final-contracts-20261012-000435-473`: os 11 contratos repetidos na fixture corrigida.
- PASS `industry-two-cycles-20261012-000512-151`: nove componentes, SMG nativa concluída e equipada em Delta; produção atravessou descanso e competição com coletes. Base desligada, propriedade das Bills após save/load e preservação de edição manual verificadas. Limite explícito de dois dias; não prova fabricação de todos os sets nesse prazo.
- PASS `industry-fuel-native-20261012-001103-865`: corte de madeira e abastecimento nativos, espada fabricada/equipada, base desligada e save/load. Combustível não foi inserido no smithy pela fixture.
- PASS `industry-mining-regression-20261012-001250-282`: 13 verificações de minério → espada equipada e persistência, incluindo limpeza das marcações próprias.
- PASS `industry-parallel-regression-20261012-001450-753`: quatro peças, coletes ranged 0 → 3, pesquisa +223,8642 e banquinho concluído em 44.685 ticks, usando estoque original do V4 e apenas um blueprint inicial explícito.

A primeira fixture elétrica emitiu avisos de duas redes na célula do gerador durante sua preparação; sobreposição de conduítes foi removida da fixture final. Logs anteriores permanecem preservados. A execução de dois ciclos mostrou também um diagnóstico incorreto de ingredientes durante pausa de peça inacabada; retomada nativa ocorreu, mas a coordenação precisava reconhecer esses insumos já usados. Essa correção é a revisão B4.

## Próximo checkpoint

Criar/atualizar bancadas e cultivo de tecido conforme essas dependências, ligar revisão de prazo à urgência real, validar fabricação avançada completa com equipamento vestido e exercitar compra/entrega destes pedidos no comércio nativo. Manter os testes de corte/abastecimento, componente → arma e minério → equipamento como regressões.
