# Percepção, memória e explicações — pontos 11–20

Implementação incremental sobre os pacotes anteriores. O histórico antigo do save continua sendo lido; eventos antigos sem identificação aparecem como legado. Não substituir o planejamento de salas 13×13 nem o controle manual do jogador.

## Entrega

| Ponto | Implementado | Limite real |
| --- | --- | --- |
| 11 | Eventos com tick, mapa, módulo, objetivo, tick da observação, contexto e política. Transições de produção, construção, pesquisa, equipamento e emergência. | Não é um registro de cada job de cada mod. Mantém os últimos 64 eventos. |
| 12 | Série de até 128 checkpoints; saldo de nutrição, refeições, produção nativa em lotes, pesquisa e energia armazenada. Estima esgotamento alimentar quando o saldo cai. | Consumo previsto usa necessidades nativas. Variação do estoque inclui compras, perdas e transporte; não atribui causalidade. Energia agrega baterias, sem diagnosticar todas as redes isoladas. |
| 13 | Pesquisa selecionada registra estado/foco do planejador; demandas de equipamento registram destinatário, responsável previsto/atual e motivo. Funções mostram habilidades relevantes e critérios. | Não é uma explicação completa dos pesos de todos os algoritmos de combate/equipamento. Uma demanda ou responsável previsto não prova job iniciado. |
| 14 | Salas nativas, funções, área, teto, portas/destinos, proibições, portas abertas, temperatura e saídas de ar. Inclui ambientes de interesse da colônia, até 64, e mostra até 16 na HUD. | Grafo físico não garante acessibilidade individual. Acesso, perigo, área permitida e reservas continuam sendo verificações nativas. Não simula transmissão térmica. |
| 15 | Catálogo de todos os ThingDefs de itens carregados: produtos fixos de RecipeDefs, pesquisas e ThingDefs de bancadas com AllRecipes. HUD mostra rotas dos equipamentos pendentes. | Produtos variáveis, receitas especiais e máquinas que não usam WorkTable exigem adaptadores específicos. Não cria automaticamente loadout para todo item de mod. |
| 16 | Arquivo compartilhado nas configurações nativas do mod, com exportação explícita de uma janela observada de um dia, sem ameaças, caídos, fome ou alteração de população. Identidade de colônia persiste no GameComponent. | Nenhum ajuste é aprendido/aplicado automaticamente. Duas colônias compatíveis mostram repetição descritiva; não comprovam causalidade. Não existe campanha comparativa de aprendizado concluída nesta entrega. |
| 17 | Política padrão observation-v1 e food-buffer-v1 (+1 dia sobre a reserva sazonal). Troca e versão anterior persistem; botão restaura a política. Compara janelas compatíveis registradas. | Versionamento inicial restrito à política alimentar. Restauração é explícita na HUD, não rollback automático de combate/construção. |
| 18 | Checkpoints copiam valores e texto de recursos/trabalhadores; eventos guardam o contexto disponível naquela decisão. Séries e eventos persistem após salvar/carregar. | Não é replay integral nem save adicional de cada decisão. Histórico é limitado e não recupera eventos já descartados. |
| 19 | Aba Diagnóstico ampliada com tendências, política/restauração, evidências, salas, catálogo e próxima ação por impedimento. Mantém páginas anteriores e seus responsáveis/metas. | Verificação de código e runtime; teste em batchmode não comprova aparência visual da HUD. |
| 20 | Ciclos econômicos com decisões programadas e reconciliação de equipamento renovam uma leitura compartilhada; coordenação invalida cache ao mudar o tick dessa leitura. Jobs ainda validam alvos no estado nativo. | Não é um snapshot imutável universal consumido por todos os módulos. Combate/emergência têm cadência própria; dados escalarizados do histórico não são objetos de jogo usados para emitir ordens. |

## Compatibilidade e memória

- Contexto inclui versão do RimWorld, bioma, storyteller, DifficultyDef e valores primitivos da dificuldade, além de SHA-256 dos arquivos XML/DLL dos mods carregados. O hash é calculado uma vez por processo; falha de verificação bloqueia exportação da evidência.
- Mod settings externos, scripts de outros formatos, mudanças dinâmicas após o carregamento e todos os parâmetros ambientais não são integralmente representados. Comparação é descritiva; não aplicar ajustes automaticamente com base nela.
- Um GameComponent identifica a colônia entre mapas e após load. Copiar/reabrir o mesmo save não cria uma nova colônia independente.
- A série não atravessa outra política/contexto nem conta tempo anterior ao carregamento atual. Janelas com lacunas acima de 1.200 ticks não são exportadas. Checkpoints periódicos e eventos têm limites fixos; leituras econômicas adicionais não inundam a série.
- Exportação é uma ação explícita na HUD. O arquivo nativo de configurações armazena até 128 janelas, sem nomes de pessoas ou referências de objetos; novas políticas não são aplicadas a outras colônias.

## Referências

Conceitos de diferenças entre observações e apresentação de salas foram consultados no [worlddiff do rimagent](https://github.com/zorrobyte/rimagent/blob/main/agent/rimagent/worlddiff.py) e no [BaseGraph do rimbridge](https://github.com/zorrobyte/rimbridge/blob/3c1e4c7cee151104b85bf9c8372e113f91c5f08d/Source/State/BaseGraph.cs). Implementação própria usando tipos nativos de RimWorld; nenhuma dependência de Python na rotina do mod.

## Validação

Quatro etapas sobre cópias isoladas do V4, com os seis personagens originais e os critérios anteriores preservados:

- PASS `perception-memory-final-20261012-044715-023`: 27 verificações de eventos, janela de tendências, compatibilidade/deduplicação de evidências, troca/restauração alimentar, catálogo com definições sintéticas adicionais, salas nativas e save/load.
- PASS `perception-native-parallel-20261012-044803-878`: quatro peças fabricadas nativamente; coletes equipados 0 → 3; pesquisa +353,1579; três novas construções e banquinho do fixture concluído em 42.653 ticks. Prazo original de 60.000 ticks; nenhum recurso, pesquisa, necessidade ou job concedido por essa etapa.
- PASS `perception-coordination-20261012-045134-942`: 15 verificações de especialistas, suplentes, reservas parciais, exceções críticas e controle manual.
- PASS `perception-progress-regression-20261012-045215-128`: 34 verificações de ingredientes, energia, pesquisa/acesso, turnos, watchdog, produto real e save/load.

São 76 verificações distintas, além do testemunho de execução simultânea. Essas quatro etapas usaram DLL `FE27CF3FD2F11EC8343F873ADC2BAE6EB11143D2D6ADC21123AB41BFA152715E` e runtime `514DF0C03D34DCAFDE1036DD985F1B1D733CC223CF4B8AB0615DD404E7E522A2`.

Depois delas, mudou somente a apresentação da HUD: checkpoint resumido por padrão e botão para expandir o contexto completo. Compilação sem erros e PASS `perception-final-install-20261012-045606-314`, repetindo as 27 verificações na DLL final instalada `C163F87E046C87E3B2B530A693D39F1BA15C4B67EE59BCBC30306CE69A2C185F`. Não contar a repetição como 27 verificações novas. A rodada não valida aparência visual da janela.

Preparações preservadas nos manifests/logs:

- `perception-memory-20261012-043533-228`: falhou porque a leitura explícita renovava o contexto, mas a cadência descartava o checkpoint novo. Corrigida retenção de leituras explícitas e substituição da leitura no mesmo tick.
- `perception-memory-revised-20261012-044210-352`: teste de texto esperava label minúsculo; `LabelCap` nativo capitaliza a primeira letra. Corrigida comparação, incluindo assertiva de pré-requisito.
- `perception-memory-roundtrip-20261012-044455-675`: 27 PASS, mas detectou hashes curtos zero das definições sintéticas do fixture. A preparação passa a usar o atribuidor nativo de hashes; a rodada final passa sem a colisão. Não atribuir esse problema ao catálogo de definições normais carregadas pelo jogo.

A DLL instalada foi comparada com a compilada. Addon temporário removido e nenhum processo de teste restante. V3 SHA-256 `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD` e V4 `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE` permanecem intactos. Sem novo commit/push.

O teste de evidências cobre construção/filtros/deduplicação em memória; não é uma campanha real comparando políticas entre colônias, nem teste ponta a ponta de exportação pela HUD para o arquivo global de configurações. Não declarar aprendizado automático ou superioridade de política com esses resultados.
