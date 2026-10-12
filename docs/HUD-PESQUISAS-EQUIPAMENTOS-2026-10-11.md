# Painel de pesquisas e equipamentos — 11/10/2026

## Pesquisa e metas
Fila numerada de até seis pesquisas não concluídas, sem duplicatas, com pesquisa selecionada primeiro e requisitos pendentes em cada etapa. Mantém a ordem do planejador estratégico; se ainda não existe plano registrado, calcula uma prévia temporária sem selecionar pesquisa nem construir. Metas ordenadas por prioridade decrescente, com estado, impedimentos e recursos. Atualizar informações renova apenas a leitura.

## Equipamentos
Aba nova com a etapa atual dos loadouts, número de conjuntos completos, melee/ranged, peças disponíveis e fabricação pendente. Por colono: arma/roupa/armadura alvo, equipado (incluindo equivalentes aceitos pelo planejador), disponível para vestir, fabricação ou bloqueio manual/corporal. Pesquisa necessária e bancada compatível por peça; materiais agregados mostram necessário/disponível permitido/faltante.

Usa a alocação já calculada por LoadoutProgression: um item disponível não atende simultaneamente dois colonos, roupas de cadáver/danificadas são recusadas pelas políticas existentes e peças manuais não são removidas pelo painel. Não combatentes são identificados fora do plano de combate. Prévia de outras cinco etapas é somente informativa e não muda o checkpoint da IA.

Quantidades de materiais são estimativas das receitas nativas com seleção padrão de tecido/aço; incluem matérias-primas para componentes ausentes. Ingredientes alternativos, reservas para outras produções e capacidade de transporte podem mudar o orçamento real. Não é um novo executor de crafting/comércio: esta revisão expõe os dados existentes para a futura integração solicitada.

Snapshots e metadados de receitas/bancadas são calculados somente nas páginas relevantes, com cache de cinco segundos; não há varredura de receitas por peça a cada frame.

## Validação
Produção compilada sem avisos/erros; biblioteca de testes compilada (NU1900: consulta NuGet indisponível). Teste nativo PASS em `.tools/validation/hud-research-gear-20261011-181722-900/manifest.json`. Usou cópia pausada do cenário original dos seis colonos. Verificou fila de seis, seleção primeiro, unicidade, todos os slots por colono, alocação única do equipamento disponível, materiais não negativos e ausência de alteração da pesquisa, projetos ou objetos no mapa ao ler o painel.

As dez páginas renderizaram sem falha. Imagens de Pesquisa e Equipamentos inspecionadas: navegação, seis itens da fila, indicadores, recursos e início dos detalhes por colono legíveis. Não foram clicados controles de automação durante o teste. Imagens em `outputs/HUD/Pesquisas-Metas-2026-10-11.png` e `outputs/HUD/Equipamentos-Metas-2026-10-11.png`. A DLL foi instalada no jogo. Nenhum save do usuário foi sobrescrito; sessão isolada encerrada e add-on de testes removido.
