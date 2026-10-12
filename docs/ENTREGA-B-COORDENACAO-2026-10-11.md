# Entrega B — coordenação de trabalho e materiais, 11/10/2026

Primeiro pacote de coordenação após a Entrega A. Escopo: construção, pesquisa e fabricação de equipamentos. Não é a implementação integral do arbitrador de combate/resgate/comércio ou de um grafo universal de dependências.

## Comportamento

- Equipamentos só reservam um fabricante dedicado quando uma receita pendente tem bancada utilizável, habilidade/restrições compatíveis, acesso e ingredientes encontrados pelo alocador nativo. Uma ordem impossível não sequestra o especialista da pesquisa e dos trabalhos alternativos.
- A escolha do fabricante considera se o colono consegue realmente executar as receitas daquela profissão, além de habilidades, continuidade e alterações manuais já existentes.
- Pesquisa é separada dos fabricantes focados. O executor de construção protege o pesquisador entre jobs quando sua pesquisa tem prioridade maior, bancada válida e acesso. A proteção do fabricante também depende de demanda viável.
- Alimentação/atendimento médico prioritários podem prevalecer sobre a dedicação de construção/fabricação/mineração. Isso corrige uma sobreposição na qual o ajuste final de especialista podia rebaixar novamente a Cooking urgente.
- Alterações manuais são detectadas **antes** da seleção de responsáveis. Desligar uma profissão não produz um plano que diga que aquele mesmo colono será seu responsável.
- Orçamento compartilhado protege os ingredientes do próximo lote de até duas ordens viáveis, apenas de Bills próprias ainda intactas. A busca nativa resolve materiais variáveis/fixos e misturas; não há estimativa fictícia de quantidade disponível.
- A proteção vale para novas obras opcionais da IA. Obras essenciais (Critical/High), fechamento de abrigo ainda incompleto e trabalhos já emitidos continuam seguindo os custos/reservas nativos. Não cancelar blueprints do jogador nem trancar pilhas.
- Comida e medicina não entram nessa reserva. Bills manuais podem orientar profissões, mas não geram uma reserva automática de materiais.
- Suspensão, alteração de filtro, falta de ingredientes, mudança de emergência ou desligamento fazem a demanda/reserva ser reconciliada. Cache normal de 600 ticks evita refazer as buscas em cada intervalo de execução; alterações manuais recém-detectadas forçam nova leitura.
- Aba **Diagnóstico** mostra ordens viáveis/pendentes, responsáveis propostos, materiais protegidos e principais bloqueios.

## Persistência e limites

O plano de coordenação é derivado por mapa e reconstruído após load. Bills, prioridades, propriedade e rotina continuam usando as estruturas persistentes anteriores; não gravar referências duplicadas ou um orçamento obsoleto no save. Os contadores da Entrega A continuam persistentes.

Uma reserva é um orçamento conservador por tipo de material; não é uma reserva nativa de pilha específica. Ela não substitui pathfinding, reservas reais do job ou a confirmação de equipamento vestido. Uma mudança de estoque pode levar até 600 ticks para atualizar o plano. Não existe garantia de ganho de TPS ou comparação de desempenho com concorrentes neste pacote.

Permanecem pendentes: grafo de demandas completo, arbitragem global de todos os executores, reservas para comércio/caravanas, pontuação de urgência unificada, previsão de prazos e coordenação por toda a cadeia de equipamento até os últimos sets. Parte dos itens 3–8/20/30 está ampliada; não marcar todos como concluídos.

## Testes

`coordination-contracts-20261011-222902-012`: PASS, 11 verificações com os seis originais em cópia isolada. Criou bancada/tecido para isolar mecanismos: ingrediente ausente/viável, reserva exata de uma receita, blueprint opcional bloqueado pelo orçamento, blueprint essencial permitido, pesquisador separado, proteção do fabricante, liberação quando bloqueado, suspensão e desativação manual de profissão. Fixture salva em `CoordinationEvidence`, apenas no perfil de teste.

DLL instalada/testada: `300F43F9B16E8664B1AFC3F9338A5C1707B6F2F6011168C11EBB8B08BB71F2AA`. Commit-base `fb5d233`, árvore com alterações locais; este pacote ainda não foi commitado/publicado.

- PASS `coordination-release-contracts-20261011-223133-384`: 11 verificações na revisão com proteção das funções de sobrevivência.
- PASS `coordination-release-research-20261011-223218-488`: pesquisa nativa do V3, 0 → 25,37364 pontos em 3.968 ticks; diagnóstico acompanhou progresso real.
- FAIL preservado `coordination-release-parallel-20261011-223321-209`: produção/equipamento avançaram, pesquisa ganhou 413,0275 pontos, mas nenhuma nova estrutura em 60.000 ticks. O V4 inicial contém zero blueprints/frames; esse cenário não comprovou construção simultânea. Não atribuir uma aprovação a esse ensaio.

PASS `coordination-parallel-order-20261011-223956-702`: adicionou **um blueprint de banquinho de madeira** em célula livre e acessível na cópia do V4; não concedeu materiais, prioridades, necessidades ou jobs. Em 44.935 ticks: esse objeto específico foi concluído nativamente, pesquisa ganhou 218,9745 pontos, cinco peças foram fabricadas e ranged com colete passaram de 0 → 3. Esse teste comprova coordenação com demanda concreta; não construção integral da base.

Runtime original dos contratos/pesquisa/ensaio negativo: `036A9CC50F3D374AB84F27DB8B3971AC2EEACF669A4741C35107A1D056AC9BDB`. Runtime com a ordem inicial explícita: `2D51F873DE0D1E1644458D7940BBC6B4A74FE7C08747EDAE6A195DE99083AD6C`. A DLL de produção não mudou entre esses ensaios.

PASS `coordination-release-diagnostics-20261011-224347-110`: 21 verificações de bloqueios, progresso nativo, suspensão/filtros/turnos, histórico limitado e persistência do contador/histórico após save/load. Mesmo hash de produção e runtime da repetição com ordem inicial.

Compilação da produção sem avisos/erros. Runtime sem erros; aviso NU1900 do feed de auditoria NuGet indisponível, sem dependências novas. Os testes são nativos em sessões ocultas; aparência da HUD não recebeu inspeção visual neste pacote.

Saves pessoais preservados: V3 `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD`, V4 `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`. Addon temporário removido ao encerrar os ensaios; DLL instalada igual à produção testada. Resultados/manifests em `.tools/validation`, preservando também o ensaio negativo.

## Continuação

Próximo passo da coordenação: transformar as dependências já verificadas em demandas persistentes com responsável e prazo; ligar falta de material à coleta/mineração/comércio e confirmar entrega/equipamento para fechar cada demanda. Manter alimentação/medicina, controles manuais, V3/V4 e o teste de orçamento como regressões.
