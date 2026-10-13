# Continuidade da colônia — primeira implementação

Esta revisão entrega mecanismos executáveis para recuperação, coordenação, dependências e cuidados, além de avaliação inicial de oportunidades/DLCs. Não representa suporte completo a todas as mecânicas dos dez tópicos.

| Tópico | Implementado nesta revisão | Próxima extensão |
|---|---|---|
| Recuperação | Até três tentativas para Bills da IA e pesquisa elegíveis sem progresso; trabalhador ocioso, job nativo, verificação por trabalho restante/pontos/produto. Preserva filtros, horários, habilidade e ordens manuais. Construção conserva seu watchdog. | Correções específicas de combustível, energia e rotas bloqueadas; ensaios com cada falha. |
| Cadeia de metas | Pesquisa, bancada, ingredientes e receita intermediária para Bills automáticas, com coleta/compras e instalação nas oficinas/estoques aprovados. | Produtos variáveis, receitas especiais de mods e demanda total da campanha. |
| Responsáveis | Dono atual de medicina, caravana, prisão, comércio, combate, equipamento, construção, produção e pesquisa; medicina/combate podem interromper produção automática, preservando controle manual. | Arbitragem de equipes/rotas em múltiplos mapas. |
| Inverno | Prazo até estação fria, colheita já pronta, déficit de roupas, previsão de madeira; até 15 dias de antecedência ativa políticas de comida/roupas/coleta. | Temperatura mínima prevista por bioma e capacidade real dos cozinheiros/crafters até o prazo. |
| Reposição | Peças abaixo de 65% não satisfazem checkpoint; fabricação ignora tais peças no contador; reserva de uma armadura/capacete por modelo usado quando conjunto completo e recursos sobrando. | Orçamento plurianual, reservas de armas/cintos e ensaio específico de troca por deterioração. |
| Defesa especial | Identifica cerco, duties de sapper/breach, ataque à parede e ameaça dentro de sala da área Home; prioriza invasores e permite atualizar âncora local em invasão interna. | Contra-cerco com artilharia e ensaios completos de sapper/breacher/drop pods/infestação. |
| Pós-batalha | Janela persistente de 72 horas, mortes, infecções e aptidão para retornar; resgate/tratamento usa rotinas nativas existentes. | Medir sobrevivência durante as 72 horas completas e resistência/imunidade a infecções. |
| Logística | Transportes nativos concluídos, duração observada, destino, deslocamento direto e sugestão de insumos próximos; mantém prateleiras nas salas aprovadas. | Medir percurso real e comparar antes/depois da sugestão. |
| Quests/diplomacia | Ofertas, prazo, equipe livre, recompensas de itens ligadas às faltas, sinais de combate/viagem e relações de facções. | Avaliação quantitativa de obrigações por tipo e aceitação/seleção de recompensa. |
| DLCs | Papéis/preceitos, requisitos de roupa, poderes e permissões reais na HUD; suporte a Stun com validações nativas, foco, entropia, alcance e efeito. | Uso de permissões, produção de roupas de papéis, outras habilidades; depois mechs/contenção. |

## Limites e segurança de execução

- O grafo lê até 12 raízes por rodada e 64 nós, profundidade seis e oito Bills intermediárias. Faz rodízio das raízes; aposentadoria de intermediárias aguarda a volta completa. Ingredientes de comida/medicina continuam com seus alocadores nativos. Quantidades correspondem ao próximo lote; não são reserva garantida de toda a campanha.
- Itens em trânsito reduzem duplicação de pedidos, mas não são tratados como ingredientes fisicamente entregues. Estoque considerado exclui podridão, itens proibidos e neblina; disponibilidade final depende da busca nativa.
- Recuperação não rouba trabalho produtivo de outro colono. Três tentativas sem avanço conservam o diagnóstico, em vez de repetir ordens indefinidamente; novo progresso abre outro episódio.
- A ordem manual idêntica pode manter o ID antigo no RimWorld. O ponto de entrada nativo é observado para liberar controle de combate da IA; despachos do próprio mod têm um escopo separado. Recuperação de produção usa StartJob sem transformar ordem automática em playerForced.
- Janela pós-batalha começa na transição registrada de ameaça para mapa seguro. A observação periódica não deve apagar essa transição antes de notificá-la.
- Logística amostra o job a cada 30 ticks e registra somente conclusão Succeeded. Tempo inclui coleta, espera e trabalho; distância é deslocamento direto, não pathfinding. Jobs muito curtos podem não entrar na amostra. Até 32 agregados são salvos.
- A previsão da colheita inclui apenas plantas já colhíveis. Não promete que uma plantação ainda em crescimento ficará pronta antes do inverno.
- Quests desconhecidas exigem revisão; não existe aceitação automática genérica. Ideologias/papéis não são reatribuídos; permissões não são gastas automaticamente nesta fase.

## Desempenho

Em segurança, a leitura completa de ameaças passa de 15 para até 120 ticks. Uma sentinela de inimigos ativos continua a cada 15 ticks, e decisões de combate continuam rápidas quando há ameaças/ordens. Os novos módulos têm intervalos separados e estão no profiler opcional. Nenhuma nova thread acessa objetos de RimWorld/Unity. A configuração de aprendizado muda para learning-v2/continuity-v1 para não promover evidência de uma estratégia anterior como se fosse equivalente.

Esta mudança reduz leituras redundantes; não comprova aumento de FPS/TPS em campanha longa. O benchmark do mesmo save com/sem automação permanece necessário para essa conclusão.

## Validação

Os resultados finais e os hashes estão em `outputs/Continuity/results-2026-10-13.json`. As etapas utilizam cópias isoladas de TESTE V3, cenário EliteSix, os seis personagens originais e velocidade 3×. Spawns/pesquisas administrativas são apenas montagem da situação; fabricação, ataques, atordoamento, transporte, resgate e tratamento verificados ocorrem por jobs nativos.

- Continuidade: bloqueios de Bill/pesquisa, controle manual, retomada automática, fabricação de blocos e persistência após save/load.
- Tática/logística: ameaça atacando parede, cast nativo de Stun com efeito observado e madeira entregue ao estoque antes de contar transporte concluído.
- Melee: dois melee originais atacam juntos e causam dano nativo ao mesmo inimigo.
- Resgate: paciente transportado/tratado, sangramento cessado, médico liberado e ordens manuais preservadas, inclusive a aceitação nativa de ordem idêntica.

Esses testes funcionais não equivalem a quatro colônias independentes, uma campanha longa, QA visual da HUD ou validação de todos os tipos de ataque/quest/DLC. As próximas extensões estão indicadas na tabela, sem marcar comportamento apenas planejado como concluído.
