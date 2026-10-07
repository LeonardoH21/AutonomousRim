# Responsividade e módulos 13×13

Revisão de 7 de outubro de 2026. Substitui o desenho de pátios para **planos novos**. Saves com estruturas e projetos antigos conservam seu planejamento; a atualização não destrói bases existentes.

## Execução

- Ameaças e decisões de combate: 15 ticks.
- Distribuição de trabalhos de construção aprovados: 30 ticks.
- Revisão de etapas, materiais e novas ordens: 120 ticks.
- Prioridades de trabalho: 180 ticks; agenda: 300; comida: 600; equipamento: 900.
- Percepção geral compartilhada: 360 ticks; ameaças mantêm sua atualização rápida independente.
- Planejamento da expansão: ciclo independente de 1.200 ticks.
- Estratégia e análise de falhas: 1.800 ticks, em momentos diferentes.
- Até nove projetos, dimensionados pela disponibilidade de construtores. Limites de novas ordens não retiram investimentos existentes da fila nativa.
- Um construtor principal é reservado quando há pelo menos três trabalhadores aptos e nenhuma emergência de alimentação, incêndio ou atendimento médico. Prioridades manuais continuam protegidas.

Esses intervalos usam ticks normais do jogo. Não há alteração de velocidade de trabalho, custo, pesquisa, habilidade exigida ou duração das ações.

## Geometria e ordem

Um módulo tem 13×13 células externas: `parede + 5 + divisória + 5 + parede`. Quatro quartos compartilham divisórias; duas partes unidas têm interior de 11×5 sem parede no meio. O estoque inteiro tem 11×11 internos. Cozinha e abate ocupam quartos separados.

A grade tem passo de 16 células, deixando corredores de três células entre módulos. A busca visita centro, eixos cardeais e depois cantos de cada anel. Módulos novos precisam estar ligados a um módulo existente. Terreno ocupado pelo jogador é preservado; rochas e ruínas neutras removíveis recebem ordens nativas de preparação.

Quartos necessários à população recebem paredes, pisos de madeira e camas antes dos serviços seguintes. Cozinha e abate têm preferência na fila sobre estoque, freezer e energia, mas uma sala bloqueada por escavação não impede outra obra financiada de avançar. Os pisos fazem parte da própria etapa do cômodo: não esperam o acabamento da base inteira. O concreto dos corredores espera o núcleo; sua limpeza precisa anteceder o uso dos acessos. Uma plantação de arroz de 13×13 é procurada em solo fértil descoberto após os quartos, sem reservar dezenas de módulos vazios.

O fechamento térmico opcional de conjuntos de nove módulos e a demanda por salas secundárias ainda não são critérios de aprovação desta revisão inicial.

## Protocolo nativo

`scripts/ModularConstructionTest.ps1` usa um perfil separado por `RunName`, preserva os saves e registra hashes das DLLs. A preparação permite cinco colonos com habilidades 20. Recursos iniciais, necessidades, traços, saúde, pesquisa, transporte e construção são os do jogo. A velocidade é 3×.

O observador registra primeira ordem, primeira entrega, primeira parede, sete paredes, quartos, cozinha, abate, estoque e freezer. A versão mais recente também verifica a ordem paredes/pisos/móveis, sobrevivência dos colonos iniciais e refrigeração real abaixo de 0 °C com cooler ligado. O teste falha se sete paredes ainda não estiverem concluídas após oito horas ou se o núcleo inicial ultrapassar três dias.

## Evidência dos testes

| Execução | Resultado |
|---|---|
| Primeira geometria | Rejeitada: árvore no ponto de uso do fogão. Preparação ampliada. |
| Segunda geometria | Rejeitada: ruína na saída de calor. Validação e limpeza dos dois lados do cooler corrigidas. |
| `run-3` | Núcleo inicial construído em 50,21 horas. Sete paredes em 0,64 h; quartos em 25,54 h; cozinha em 27,55 h; abate em 29,28 h; estoque em 50,16 h. Esta versão ainda não exigia a medição de temperatura para PASS. |
| `run-5` | Quartos em 25,30 h; cozinha em 48,43 h; abate em 50,16 h; freezer construído em 60,77 h. Reprovada no limite de três dias: estoque/energia ainda incompletos. A escavação do abate estava serializando os outros serviços; essa dependência global foi removida. |
| `run-4` | Primeiro quarto em 6,38 h, mas outra entrada ficou bloqueada por ruínas externas. Interrompida para diagnóstico, **não aprovada**. Corrigida a limpeza antecipada dos acessos/corredores. |

Não comparar mapas diferentes como um benchmark controlado de velocidade: distribuição de recursos, obstáculos, traços e necessidades variam. Os resultados acima mostram execução nativa observada, não garantem um prazo fixo em qualquer bioma.

A execução **`run-6` passou** usando os ciclos independentes de trabalho, agenda, comida e equipamento:

| Marco | Horas de jogo desde o início |
|---|---:|
| Primeira ordem | 0,00 |
| Primeira entrega | 0,34 |
| Primeira parede | 0,36 |
| Sete paredes | 0,71 |
| Primeiro quarto habitável | 8,11 |
| Cinco quartos, pisos, tetos e camas | 10,51 |
| Cozinha | 28,42 |
| Estoque | 40,94 |
| Abate | 48,48 |
| Freezer construído | 51,50 |
| Refrigeração ligada abaixo de 0 °C; PASS | **61,37** |

O verificador independente `scripts/CheckModularSave.py` confirmou cinco módulos conectados, quartos de 5×5, paredes compartilhadas, pisos internos previstos e uma plantação inteira de 13×13 no save real. O concreto de todos os corredores não é requisito do núcleo inicial e ainda estava em execução ao salvar; não se apresenta este save como uma base com todo o acabamento concluído.

Evidência local: `.tools/modular-construction-run-6/Player-20261007-204234.log`, `manifest.json` e `Saves/ModularInitialComplete.rws`. SHA-256 da DLL instalada/testada: `381A1D4A2F2A0DA22534A77D51BC9049017C170D3D0D224C9A2437A6B3D9DF33`.

Compilação da produção e do observador sem avisos/erros; 35 verificações de políticas passaram. Os testes de construção usam trabalho real. Os dois testes de geometria rejeitados, a execução interrompida e o teste que ultrapassou três dias foram mantidos como evidência de diagnóstico.

Para exportar o save sem o observador de testes, use `scripts/ExportModularSave.ps1 -RunName run-6 -CopyToGameSaves`. O exportador remove somente componentes e metadados do mod temporário de teste; preserva a colônia, suas construções, recursos e trabalhos.
