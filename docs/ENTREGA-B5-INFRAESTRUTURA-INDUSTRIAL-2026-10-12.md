# B5 — Bancadas por demanda e insumos próximos

## Comportamento implementado

O checkpoint de equipamento e as faltas de componentes alimentam uma revisão a cada 600 ticks. Com automação de base e equipamento ligadas, sem ameaça/incêndio e com pelo menos três dias estimados de comida, a IA adiciona até duas bancadas pesquisadas por revisão dentro de Estoque/Oficina/Fabricação já utilizáveis. Costura manual, forja a combustível, machining e fabrication são as alternativas nativas deste pacote. Não abre outro módulo, não substitui mobília de referência e não duplica uma bancada já construída ou planejada, inclusive cancelada pelo jogador. As adições industriais têm prioridade High; prioridades de projetos anteriores são preservadas.

Bancadas elétricas só são adicionadas com transmissão elétrica construída ou planejada a até seis células; o teste não prova energização automática de todas as bancadas. Demanda sem espaço, pesquisa, apoio ou transmissão continua aguardando a infraestrutura normal.

Até quatro bancadas com Bills próprias ativas são avaliadas. Cada uma recebe até duas ShelfSmall, com no máximo duas adições por revisão e distância máxima de oito células. O piso deve suportar o móvel, a posição deve ser acessível e as células de trabalho, portas e corredores centrais ficam livres. Zonas do jogador diferentes da zona do módulo bloqueiam a adição. Apenas células da zona automática do módulo são retiradas para acomodar o móvel. A criação inicial de estoque também exclui móveis e reservas incompatíveis para evitar sobreposição.

Os filtros acompanham os insumos preferidos das receitas e a permissão de combustível da bancada. Prioridade Important supera prateleiras gerais Preferred. Comida, medicina e couro humano ficam excluídos. Esses espaços limitam capacidade em pilhas, não reservam quantidades nem substituem o algoritmo de ingredientes do jogo. Construção, transporte e produção continuam por jobs nativos.

Assinatura persistida inclui itens permitidos, prioridade, intervalos de qualidade/condição/chance mental e filtros especiais. Alteração manual faz a IA abandonar definitivamente o controle daquela prateleira; não a copia nem cria outra em seu lugar. Apenas prateleiras emitidas pelo plano recebem configuração automática inicial. Desligar base/equipamento, ameaça ou baixa reserva reduz prioridade de prateleiras intactas a Preferred; os itens e móveis permanecem. Filtros intactos podem acompanhar novas receitas quando a demanda retorna.

Status de armazenamento na HUD inclui bancadas/prateleiras prontas versus planejadas e quantidade sob controle manual. Não foi feita validação visual da HUD neste pacote.

## Validação

Compilação de produção sem avisos/erros. Runtime compila; NU1900 informa indisponibilidade do feed de vulnerabilidades NuGet, não falha de compilação.

Ensaio isolado copia V4, mantém os seis colonos originais e usa modo pacífico/3×. Setup concede pesquisa, sala e insumos, remove equipamento necessário para representar demanda real; após START não concede móveis, materiais, jobs ou produtos. Contratos cobrem toggles, host pausado, zona do jogador, terreno sem apoio, deduplicação e cancelamento. Testemunho exige construção nativa da forja e das duas prateleiras de costura, tecido transportado para prateleira e calças fabricadas, seguido de edição manual e save/load.

PASS `industry-infrastructure-final-20261012-033621-879`: 23 verificações, com testemunho nativo completo e save/load. DLL `F6C42D666DCEF384C6C8F1B55AF1827D5DDDEF2AC9CA735EB532B3D7F4F8ECB9`; runtime `FE9F2C456828921F26987E7D11CB088CE9A2276F19700F58EA2DFC38245E1AB8`.

PASS `industry-infrastructure-resume-regression-20261012-033825-769`: 21 contratos de componentes, combustível/ingredientes, criador de inacabados, segurança, Bills manuais e rotas comerciais. Mesmas DLLs. Ensaio de contratos, não prova de compra ou entrega comercial.

PASS `industry-infrastructure-parallel-regression-20261012-033859-926`: 13 verificações e cadeia nativa de mineração → espada fabricada → equipada → save/load, com base desligada e designação manual preservada. Apesar do nome da pasta, a flag desse ensaio foi `autonomousrimequipmentchaintest`: não é evidência de pesquisa/construção simultâneas.

PASS `industry-infrastructure-coordination-20261012-034127-720`: quatro peças fabricadas, coletes ranged 0 → 3, pesquisa +388,4756, três novos móveis e banquinho nativo concluído, em 50.958 ticks (limite mantido em 60.000). Flag `autonomousrimgearproductiontest` com `autonomousrimcoordinationregression`, mesmas DLLs. Sem insumos, pesquisa, necessidades ou jobs concedidos nesse ensaio além do blueprint de banquinho documentado no setup.

Total: 57 verificações explícitas nos três primeiros ensaios, mais a regressão nativa de equipamento/pesquisa/construção. V3/V4 pessoais conservam hashes `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD` e `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`.

O teste nativo principal registra duas entregas de HaulToContainer incompletáveis com recuperação automática; atingiu todos os critérios sem exceção de driver. Não descrever o log como totalmente livre de tentativas recuperadas. A sobreposição de zonas observada no primeiro setup não voltou na execução final.

Falhas de preparação preservadas:
- `industry-infrastructure-native-20261012-033029-208`: interrompido após identificar sobreposição da zona com móveis e demanda de calças não representada no vestuário inicial.
- `industry-infrastructure-native-revised-20261012-033304-174`: interrompido para corrigir o observador que exigia duas prateleiras no mapa inteiro; a forja nova corretamente recebeu mais duas. Antes da interrupção confirmou forja/prateleiras construídas, tecido transportado e produção nativa.

## Próximo checkpoint

Cultivo de tecido pela demanda, equipamentos avançados e compra/entrega industrial completas. Não há benchmark de campanha, solver universal de receitas ou prova de todos os loadouts nesta entrega.

Verificação final: DLL instalada igual à compilada, addon temporário removido e nenhum RimWorld de teste permanece aberto. Código e relatórios locais sobre fb5d233, sem novo commit/push neste pacote.
