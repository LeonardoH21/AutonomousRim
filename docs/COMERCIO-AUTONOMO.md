# Comércio automático — implementação inicial

Implementado em 08/10/2026. **Sem testes unitários ou de jogo nesta sessão, conforme pedido do usuário.** Compilação contra RimWorld 1.6.4633 é verificação de integração de tipos, não comprovação de uma economia funcional em partida.

## Como ativar

Abra **Rim AI → Comércio automático**. A opção inicia desligada, inclusive nos saves antigos. **Expedições comerciais** têm controle separado dentro do painel e também começam desligadas. Desligar todas desliga comércio; formação própria é cancelada, e uma expedição já em viagem recebe retorno no ciclo seguinte, salvo tomada de controle manual.

## Fluxo implementado

- A demanda usa o checkpoint militar atual, custos de infraestrutura essencial, materiais ainda necessários às obras emitidas, comida e medicina. Desconta estoque permitido e compras em trânsito. Uma pequena fila de novas obras evita financiar o projeto inteiro antes do abrigo.
- Reserva padrão de prata: máximo entre 150 e 50 por colono presente. Alimentação crítica/pacientes podem liberar verba emergencial para compras de sobrevivência realmente oferecidas. Infraestrutura e equipamentos não recebem essa liberação.
- Comerciantes visitantes e naves orbitais são lidos da partida. Seleção considera recursos oferecidos, negociação efetiva, saúde, ordens manuais, acesso/reserva, título exigido pela facção e disponibilidade do médico.
- Um job próprio faz caminhada, reserva e interação até o comerciante/console. Só então configura e executa `TradeSession`/`TradeDeal` nativos. Não abre diálogo para a automação e não interfere em uma negociação manual ativa. Preços, prata dos dois lados e quantidades são revalidados no momento.
- Vende somente smokeleaf joints, flake e yayo permitidos e disponíveis. Não vende equipamentos, recursos industriais, alimentos, medicina, colonos ou prisioneiros. Transfer groups contendo pilhas proibidas/reservadas são recusados para não vender uma pilha manualmente protegida.
- Compras seguem sobrevivência → infraestrutura → equipamento/ingredientes. Compara preço de equipamento pronto com custo estimado de receita, verificando condição, proteção/utilidade e biocoding. Uma compra de equipamento adia materiais militares até nova análise. Componentes comprados reduzem parte da demanda por seus precursores.
- Inicialmente prepara smokeleaf, com bills `Do until X`: primeiro lote até 100; com negociação recente pode aumentar até 500 conforme déficit financeiro. Pausa em escassez e emergência. Produção existente/manual permanece manual. Bill da IA editada pelo jogador deixa de ser ajustada.
- Plantação comercial só é acrescentada com reserva de três dias, agricultor apto e estação válida. Não substitui zonas existentes nem ocupa a reserva de expansão do núcleo. Primeiro alvo até 36 células, expansão até 144 quando há negociação recente. Parcelas comerciais adicionais são de baixa prioridade.
- Instalações são adicionadas dentro dos cômodos aprovados: crafting spot/laboratório, console, beacon e duas prateleiras pequenas. Preserva eixos, portas e células de interação; aproveita rede existente/planejada, com conduíte normal sob parede e oculto fora dela. Prateleiras comerciais aceitam produtos, folhas e prata. Pesquisa dos pré-requisitos entra na rota estratégica.
- Copia a política padrão de drogas para colonos gerenciados, retirando consumo recreativo/agendado e carregamento dos produtos comerciais. Mantém tratamento de dependência. Políticas personalizadas e alterações posteriores são preservadas; ao desligar, restaura somente cópias ainda pertencentes à IA.
- Depois de negociar, pode escolher entre smokeleaf/flake/yayo conforme preço nativo, aceitação, receitas/bancadas/habilidades disponíveis e estimativa de trabalho de cultivo/fabricação. Não muda com um grande estoque do produto anterior. Objetos não spawnados usados para estimar preços são referências, nunca mercadorias gratuitas.
- Registra compras orbitais e de caravana como em trânsito. Não considera equipamento comprado como equipado nem pesquisa como checkpoint concluído. Demanda é reavaliada após a entrega real.

## Expedições

Requerem déficit por dois dias, cinco colonos aptos, ausência de sangramento/ameaça, reserva de sete dias e clima ameno no mapa. Selecionam dois colonos, preservando o melhor médico e ao menos três defensores capazes, com cobertura de cultivo/cozinha. Excluem helpers/lodgers e ordens manuais. Colonos reservados para formação não recebem novos trabalhos dos gestores comuns.

Consideram assentamentos não hostis, sem mapa carregado, alcançáveis na mesma camada, com estimativa de até dois dias de ida. Carregam refeições compatíveis que durem a viagem, reserva para retorno/demora, medicina excedente, prata acima da reserva e mercadorias aceitas. Usam formação e transporte nativos; não teleportam colonos ou itens.

Estoque do destino é desconhecido até a chegada. A transação reduz compras se ultrapassariam 90% da capacidade de carga e retorna depois da visita. Recalcula reserva alimentar com velocidade real durante viagem; ameaça na base, ferimentos, indisponibilidade/hostilidade do destino ou desligamento provocam retorno. Mudança de rota/pausa/formação pelo jogador libera o controle. Checkpoint e demanda originais ficam registrados para que a ausência dos combatentes não libere prematuramente a evolução.

## Persistência e limites ainda não validados

Saves guardam controles, bills, políticas, destino/equipe/formação, etapa, entregas e histórico de até doze trocas. A HUD mostra orçamento, demanda, alvo de produção, entregas, última negociação e estado da expedição.

Ainda não foi executada uma troca real, fabricação/colheita comercial, viagem ou round-trip de save/load desta revisão. Não há garantia de sucesso em combate/emboscada de caravana. A triagem usa clima local, alcance e tempo de viagem; não prevê todas as ameaças/climas da rota. Animais de carga, rotas alternativas e recuperação de caravana imóvel ainda não têm gestor próprio.

A comparação comprar/fabricar usa valor de ingredientes, não simulação completa do tempo de trabalho/qualidade. Infraestrutura orbital começa com um beacon; corredores/capacidade e cobertura integral de grandes depósitos podem exigir expansão futura. Entregas orbitais são reconhecidas pela diferença de estoque permitido com prazo de um dia; consumo simultâneo ou item entregue proibido pode atrasar reconhecimento. Produção suspensa não remove plantações já semeadas ou estruturas concluídas. O checkpoint natural até marine/cataphract continua não validado.

O plano de prisão é documentação separada; não foi implementado nesta revisão de comércio.
