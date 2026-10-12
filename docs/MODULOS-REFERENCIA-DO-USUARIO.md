# Referência do usuário: 13X13 Todas as construções

Save lido em 11/10/2026, sem alteração do original.

- Arquivo: `C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\13X13 Todas as construções.rws`
- SHA256: `D5B5BFEDE9E265261EDB8DB0DD6A53A15D6600EB72EE140C28ABB638CE46D81A`
- Catálogo extraído: `outputs/ReferenceModules/reference-modules.json`.
- Planta de consulta: `outputs/ReferenceModules/reference-plan.html`.
- Extração reproduzível: `scripts/ExtractReferenceModules.py`.

O catálogo contém posições relativas, rotações, material, dimensões, pesquisas necessárias, filtros de estoque, termostatos e as estruturas efetivamente presentes. A planta marca a posição de origem de cada objeto; o catálogo conserva as dimensões de móveis grandes.

## Sete módulos fechados 13×13 encontrados

| Módulo | Conteúdo confirmado | Evolução na gameplay |
|---|---|---|
| Refeitório/recreação | Mesa 2×4, 26 bancos, três mesas de xadrez, três hoopstone rings, sinuca, poker, TV grande, seis sofás, aquecedor e cinco luzes | Mesa e assentos primeiro; lazer inicial que o jogo permitir; depois mobiliário complexo, sinuca/poker e TV conforme pesquisa e recursos |
| Cozinha/abate/freezer | Setores separados, fogão elétrico, mesa de abate, 27 prateleiras, duas portas automáticas, duas portas normais, dois resfriadores e quatro luzes | Fogão a lenha e portas normais inicialmente; energia/refrigeração; prateleiras; portas automáticas quando justificadas |
| Estoque/oficina A | 24 prateleiras, costura elétrica, forja elétrica, laboratório de drogas, fabricação, crematório e armário de ferramentas | Costura/forja básicas nas posições correspondentes; laboratório, fabricação e versões elétricas entram quando utilizáveis. Crematório é posterior e depende de demanda |
| Estoque/oficina B | 27 prateleiras, corte de pedras, usinagem, esculturas, cervejaria, fundição e armário de ferramentas | Corte de pedra e produção útil primeiro; usinagem, cervejaria/fundição conforme pesquisa, matérias-primas e demanda |
| Quatro quartos | Quatro camas de casal, quatro cabeceiras, quatro cômodas, quatro vasos de bonsai e quatro luzes; um aquecedor de 21°C e quatro saídas de ar | Camas básicas sem alterar divisórias; cama de casal e conforto depois; preservar o circuito térmico, pesquisar as saídas de ar e acompanhar temperatura real |
| Energia/hospital | Energia na metade inferior: seis geradores, sete baterias e uma saída de ar externa. Hospital na outra metade: seis camas hospitalares, seis monitores, um aquecedor, três luzes e uma prateleira medicinal | Não construir seis geradores/sete baterias de imediato: preencher os espaços conforme consumo. Hospital começa com camas normais médicas e dois espaços de medicina; depois camas hospitalares e monitores |
| Pesquisa/dois quartos | Dois bancos simples e dois avançados, quatro cadeiras, cinco estantes de livros, quatro luzes; dois quartos mobiliados, um aquecedor e três saídas de ar | Dois bancos simples inicialmente; bancos avançados e apoio depois. Os dois quartos e a pesquisa compartilham o circuito de calor |

As posições de referência formam módulos vizinhos, com três células entre eles. Isso é uma preferência de organização, não obrigação de preencher um quadrado ou cubo inteiro.

## Agricultura real

Quatro campos completos de 13×13: batata, arroz, smokeleaf e algodão.

O quinto campo 13×13 é dividido em **4×13 de healroot (52 células, 30,77%)** e **9×13 de morango (117 células, 69,23%)**. Preservar essa divisão quando for implementado o novo modelo agrícola; são duas zonas dentro de um único módulo. Não contar esse exemplo como dois campos de 169 células.

## Hospital e armazenamento

A prateleira do hospital está em `(149,180)` no mapa, `(8,11)` dentro do módulo, e aceita somente `MedicineHerbal`, `MedicineIndustrial` e `MedicineUltratech`. A prioridade salva é **Preferred**. Na automação, manter o filtro medicinal e elevar a prioridade para **Critical**, para que os depósitos gerais não atraiam os medicamentos necessários ao atendimento. Antes da pesquisa de prateleiras, usar dois espaços de estoque no mesmo setor.

Organização proposta para os dois depósitos, mantendo as bancadas em seus lugares:

- **A: indústria e equipamento** — aço, plasteel, componentes, tecidos/couros, armas e roupas, com setores próximos à forja, costura e fabricação. Preparar matérias-primas e excedentes do laboratório em setor próprio.
- **B: matéria-prima e produção auxiliar** — blocos de pedra, madeira, materiais de escultura, cervejaria e fundição. Pedaços de pedra ficam no despejo externo próximo ao corte de pedras, sem ocupar prateleiras internas.
- **Freezer:** ingredientes e refeições, com prioridade superior ao depósito geral; separar alimentos dos filtros gerais.
- **Hospital:** apenas medicina no espaço dedicado, com prioridade máxima.

Os filtros gerais do save são amplos; o modelo automático deverá especializá-los. Filtros não precisam ser copiados indiscriminadamente só porque constam do exemplo.

## Aquecimento e refrigeração

Preservar o desenho das quatro saídas de ar do conjunto de quartos e as três ligações do conjunto de pesquisa/dois quartos. Um aquecedor por conjunto é o ponto de partida aprovado. A IA deverá acompanhar a temperatura real, clima, portas abertas e capacidade energética antes de acrescentar potência; não instalar automaticamente um aquecedor por quarto.

A saída de ar da energia está na parede externa, em `(153,172)`. Preservar essa comunicação com o exterior e não descarregar calor no hospital. Geradores e baterias entram conforme a demanda, mantendo os espaços e acessos do modelo.

Os dois resfriadores do exemplo estão ajustados em **−3°C**. Na gameplay automática permanece o padrão anterior solicitado de **−2°C**, com ajuste por desempenho térmico. O catálogo registra −3°C como observado, sem alterar o save.

## Adaptação ao terreno

1. Preferir módulos próximos, caminhos curtos e expansão compacta.
2. Não exigir um retângulo final preenchido. Pular locais inadequados e expandir pelos lados utilizáveis.
3. Validar apoio nas células das paredes, bancadas e pisos usando as exigências nativas de cada construção; não presumir que todos os terrenos suportam tudo.
4. Cortar árvores e remover obstáculos por trabalhos normais. Escavar montanha quando a área resultante permitir construção, com acesso de mineração e saída preservados.
5. Não atravessar água/solo sem apoio com paredes nem reservar uma sala impossível apenas para conservar simetria.
6. Proteger as posições de interação das bancadas, o espaço de passagem e as células externas de resfriamento/ventilação.

## Estado da implementação

**Integração parcial na gameplay em 11/10/2026.** O bloco cozinha/abate/freezer, a sala compacta de energia, o conjunto pesquisa/dois quartos e as versões avançadas de recreação/hospital agora usam as posições extraídas do save. `ReferenceModulePlanner` e `ReferenceLayoutData` substituem os modelos genéricos para essas novas instalações. O multianalisador substitui a bancada simples da direita. Upgrades de móveis automáticos usam desmontagem, recursos e blueprints normais; cancelamentos são respeitados.

Os quartos e a prisão já aprovados foram preservados. Os dois estoques/oficinas foram integrados e adaptados na V4 conforme a revisão: bancadas nas paredes, módulos adjacentes e prateleiras no interior; veja ESTOQUES-OFICINAS-V4-2026-10-11.md. A configuração de quatro quartos com aquecimento compartilhado e o campo misto continuam catalogados para integração posterior. Salas já construídas em saves anteriores não são demolidas automaticamente para impor a nova planta. O teste administrativo revisado é uma nova cópia de revisão, não uma alteração do save feito à mão.

As correções modulares e o teste agrícola aprovados anteriormente continuam registrados em `docs/REVISAO-MODULAR-2026-10-11.md`. Não usar o save de referência construído à mão como prova de que a IA já executou a construção completa.
