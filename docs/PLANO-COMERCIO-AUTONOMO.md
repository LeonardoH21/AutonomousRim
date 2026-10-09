# Próxima sessão — comércio e economia para equipamentos

Status: planejamento solicitado em 08/10/2026 e implementação inicial realizada após o comando do usuário. A validação por etapas foi autorizada posteriormente e está em andamento. Ver [fluxo entregue e limites](COMERCIO-AUTONOMO.md) e [evidências dos testes](VALIDACAO-ETAPAS.md). Trocas nativas locais, orbitais e viagem de caravana têm ensaios controlados; a economia sustentável de uma colônia, da fabricação à compra e equipamento, ainda requer validação integrada.

Plano complementar: [prisão, captura, conversão e recrutamento](PLANO-PRISAO-RECRUTAMENTO.md). Considerar alimentação dos prisioneiros e novos colonos nas reservas; preservar médicos/carcereiros necessários ao selecionar negociadores e caravanas.

Objetivo: transformar excedentes agrícolas e produção comercial, inicialmente baseados de smokeleaf, em materiais ou equipamentos que destravem os checkpoints militares existentes. Usar comércio, fabricação e transporte nativos; não criar mercadorias, comerciantes, recursos ou pesquisas artificialmente.

## Ordem de implementação

1. **Demanda única de compras.** Integrar com LoadoutProgression e DefenseProductionPlan. Calcular o déficit do checkpoint atual descontando estoque utilizável, peças equipáveis, ingredientes reservados e compras já em trânsito. Incluir infraestrutura que bloqueia fabricação/pesquisa, sem exigir recursos de todos os tiers de uma vez.
2. **Orçamento e reservas.** Separar prata disponível, reserva para comida/medicina e verba de evolução. Não vender recursos necessários às obras essenciais, inverno ou equipamento atual. Evitar reserva duplicada entre fabricação e comércio.
3. **Leitura de oportunidades reais.** Identificar comerciantes visitantes, orbitais disponíveis e assentamentos acessíveis. Ler estoque, itens aceitos, prata e condições pela API nativa instalada. Registrar indisponibilidade e tentar de novo com intervalo; nunca presumir que qualquer comerciante compra drogas ou vende todos os materiais.
4. **Negociador e acesso.** Escolher colono apto por capacidade efetiva de negociação, disponibilidade, saúde e segurança. Preservar ordens manuais, médicos necessários e defesa. A interação precisa ocorrer com alcance, reservas e jobs reais.
5. **Primeira entrega: comércio local.** Executar troca com comerciantes presentes usando a demanda e o orçamento. Revalidar estoque/preço antes de concluir; impedir transações duplicadas, compras além do orçamento e venda de itens protegidos. Não depender de uma caravana expedicionária para a primeira versão funcionar.
6. **Plantação comercial.** Expandir smokeleaf aproveitando o núcleo e os terrenos válidos, sem deslocar arroz/batata, algodão ou healroot necessários. Dimensionar pela verba faltante, produtividade, estação, mão de obra e espaço. Colheita comercial fica abaixo da sobrevivência.
7. **Produção de baseados.** Descobrir receitas, bancadas, habilidades e custos pelas defs reais. Criar bills próprias com metas dinâmicas, respeitando bills do jogador. Produzir lotes conforme demanda de compra, estoque, capacidade de venda e trabalho disponível; reduzir ou pausar quando suficiente.
8. **Estoque comercial e política de uso.** Reservar prateleiras próximas da produção para mercadorias e matéria-prima, respeitando conservação. Aplicar uma política própria aos colonos gerenciados para não consumir o estoque comercial automaticamente; preservar políticas manuais e restaurar ao desligar. Não prometer impedir mental breaks ou outros comportamentos nativos fora da política.
9. **Pesquisa e instalações comerciais.** Acrescentar pré-requisitos reais de produção, comunicação e comércio orbital à rota estratégica. Construir bancada, console e beacon apenas quando desbloqueados, necessários e financiáveis. Cobrir mercadorias e prata com beacon e garantir energia.
10. **Comércio orbital.** Usar console, negociador e estoque coberto, com a mesma validação de preços e reservas. Registrar recebimento e transporte dos produtos comprados antes de contabilizá-los como disponíveis para fabricação.
11. **Compra orientada ao bloqueio.** Prioridade: sobrevivência urgente → gargalo de infraestrutura → conjunto militar atual → pequena reserva. Para equipamento, comparar comprar a peça pronta com fabricar: preço, materiais, tempo, qualidade, condição, compatibilidade e disponibilidade. Considerar componentes, componentes avançados, aço, plasteel, ouro e outros ingredientes realmente exigidos.
12. **Escolha econômica adaptativa.** Começar por smokeleaf, mas comparar outras receitas desbloqueadas quando houver dados reais de preço, aceitação, trabalho, clima e insumos. Não fixar uma droga como a melhor em todos os mapas; não expandir produção sem perspectiva de venda. Limitar riqueza parada e preservar capacidade de defesa.
13. **Expedições comerciais.** Após comércio local/orbital, planejar caravanas nativas para déficits persistentes: rota, carga, transportadores, comida, descanso, estação, perigos e defesa restante. Evitar enviar colonos indispensáveis ou lançar expedição sem capacidade de retorno; replanejar diante de ameaças. Estoque de destino desconhecido deve aparecer como incerteza, não como compra garantida.
14. **Checkpoints integrados.** Déficit identificado → produção comercial → mercadoria pronta → venda → material recebido → fabricação/compra de equipamento → equipamento vestido → checkpoint liberado. Compra ou pesquisa isolada não conclui a etapa. Persistir plano, reservas, negociações e caravanas no save sem repetir ações ao carregar.
15. **HUD e controle.** Botão de comércio automático independente; mostrar verba, reserva, produção comercial, próxima compra, comerciante, impedimento e última troca. Logar compras/vendas e alterações de plano. Ao desligar, liberar apenas ordens e políticas da IA; transações já concluídas não são desfeitas.

## Critério para encerrar a implementação

Entregar o fluxo completo de demanda, produção, armazenamento, venda, recebimento e integração com equipamentos, incluindo persistência, controles e documentação. Informar claramente o que está implementado e o que ainda não foi validado. O limite de uso da conta não é prazo nem garantia de conclusão; registrar o ponto de retomada se for interrompido.

O pedido posterior de validar todas as funcionalidades substituiu o adiamento inicial dos testes. A implementação e os ensaios controlados não comprovam a economia de uma partida inteira. Disponibilidade de recursos/comerciantes, clima e sobrevivência continuam condicionando a evolução; não garantir a compra de todos os materiais em qualquer mapa.

## Referências consultadas

- [Smokeleaf joint](https://www.rimworldwiki.com/wiki/Smokeleaf_joint)
- [Trade](https://rimworldwiki.com/wiki/Trader)
- [Money making guide](https://mail.rimworldwiki.com/wiki/Money_making_guide)

Usar estas referências como orientação de jogo; confirmar receitas, custos, aceitações, preços e chamadas de API nas defs/DLLs locais durante a implementação.
