# Entrega A — progresso e diagnóstico, 11/10/2026

Primeiro pacote da pesquisa de concorrentes. Implementação original, sem incorporar código dos repositórios externos. Não representa conclusão das 55 melhorias nem comprovação de vitória em campanha.

## O que mudou

- Monitor por mapa observa pontos reais de pesquisa, obras efetivamente concluídas e lotes de receitas concluídos pelo motor nativo.
- O trabalho restante de uma receita também é observado: uma fabricação longa que continua avançando não vira um falso gargalo por ainda não ter produzido o item.
- Pesquisa/Bills elegíveis sem avanço recebem alerta depois de aproximadamente quatro horas de oportunidades de trabalho observadas. Emergência, sono, recuperação, colono drafted, crise mental e necessidades críticas excluem oportunidades. O monitor existente de construção permanece responsável pelas obras.
- Bills mostram: suspensão manual, meta satisfeita, receita indisponível, bancada sem energia/combustível/avariada, acesso obstruído, profissão sem responsável, restrição de habilidade, espera pelo turno, rota indisponível, ingredientes não encontrados, execução ou espera pela seleção/reserva do trabalho.
- Ingredientes são consultados pelo alocador nativo `WorkGiver_DoBill.TryFindBestBillIngredients`, na thread principal. Isso preserva ingredientes fixos, mistura, filtros, raio, proibições e acessibilidade. O diagnóstico não cria jobs nem muda filtros/prioridades.
- `ShouldDoNow` não é chamado pela nova leitura: esse método altera flags de pausa. As condições de quantidade/retomada são lidas sem modificar a Bill.
- Histórico limitado a 64 eventos, com mudanças de estado, primeiro lote/dezenas de lotes e objetivos removidos/substituídos. Contadores e histórico são serializados no save; o carregamento reinicia apenas a amostragem temporal para evitar contagem de tempo fora do mapa.
- HUD: aba **Diagnóstico**, seção **Progresso real e bloqueios**, seguida do histórico recente. Exibe até 30 objetivos e dez eventos mais recentes; o histórico salvo mantém 64.

## Limites deliberados

São diagnosticadas até seis Bills por leitura de 600 ticks, com até três candidatos por busca de ingredientes, em rodízio. Grandes oficinas levam várias leituras para serem cobertas; intervalos longos não são inteiramente somados ao relógio de gargalo. O alerta é conservador, não um cronômetro contínuo de ociosidade.

“Busca sem ingredientes” significa que os candidatos amostrados não encontraram um conjunto utilizável; não afirma que o recurso inexiste no mapa. Filtro, raio, proibição e rota podem causar o mesmo resultado nativo. Receitas/bancadas de mods sem WorkGiver reconhecido são explicitamente classificadas como diagnóstico limitado. Uma exceção de receita é isolada e registrada; não interrompe a leitura das outras Bills.

O monitor não corrige filtros manuais, não ativa funções desligadas e não força trabalhadores. O arbitrador de ordens, grafo compartilhado de demanda e reservas globais de materiais da Entrega B ainda precisam ser implementados. Resgates, comércio e equipamento efetivamente vestido ainda não usam este novo rastreador de resultados.

## Correspondência com o roteiro

| Item | Estado neste pacote |
|---|---|
| 1 — progresso por objetivo | Pesquisa e produção implementadas; construção reaproveita monitor existente. Outras frentes pendentes. |
| 2 — bloqueios de Bills | Diagnóstico implementado para bancadas reconhecidas; candidatos/receitas nativos. |
| 9 — resultado real | Pontos, obras, trabalho restante e lotes nativos. Entrega/comércio/resgate/equipar pendentes. |
| 10 — auditoria ao carregar/pesquisar | Reamostragem e reconciliação de objetivos implementadas; recuperação automática de toda a cadeia ainda pendente. |
| 11 — eventos | Histórico por mapa limitado e salvo. |
| 19 — HUD | Diagnósticos e eventos integrados à aba existente. |
| 54 — validação em etapas | Fixture dedicada, save/load e regressões de pesquisa/produção; não benchmark entre concorrentes. |

## Validação

Preparação exclusivamente em cópias isoladas, usando os seis colonos do cenário aprovado. A fixture pode criar bancada/ingrediente e ajustar turnos/prioridades para isolar situações; fabricação é feita por job nativo, sem concluir a receita por cheat. Save do usuário permanece intacto.

A fixture `progress-diagnostics-d-20261011-214243-862` passou 18 verificações, incluindo produção real de blocos e persistência do contador/histórico após save/load. As primeiras tentativas falharam na preparação: o raio não estava inteiramente limpo e um chunk sem CompForbiddable foi usado para um teste de proibição. A área foi ampliada e o caso passou a testar exclusão pelo filtro da Bill. Essas tentativas não são contabilizadas como sucesso.

Revisão final instalada: SHA256 `B0B0825A07D5B3AEB6F172F3303854D83D84F01C37DC9AB4F18106947F2653CE`; runtime de testes `3B2B9EFF5174118878803F8C58361B9E712890E060D34D46B0D0CF764CBC5E24`.

`progress-release-diagnostics-20261011-215220-934`: **PASS, 21 verificações**. Além dos casos anteriores, confirma avanço do trabalho restante antes de completar o lote e ausência de eventos repetidos para a mesma obra bloqueada. Os seis IDs originais permanecem presentes após recarga. O save de evidência está apenas no perfil isolado.

`progress-release-research-20261011-215313-573`: **PASS**. Charlie executou pesquisa nativa; Metalurgia avançou de 0 para 25,03981 pontos em 6.104 ticks. O diagnóstico acompanhou os pontos reais sem falso gargalo. Nenhuma pesquisa/recurso/job foi concedido nessa regressão.

`progress-release-gear-20261011-215424-104`: **PASS**. Três peças foram concluídas nativamente; ranged com colete equipado passaram de 0 para 3 em 40.144 ticks (cerca de 16 horas do jogo). O cenário foi pacificado apenas na cópia isolada. Não foram concedidos materiais, pesquisas, necessidades ou jobs. Este critério verifica produção e equipamento de coletes; não significa que os conjuntos completos/armas de todos os colonos estejam concluídos.

Os três manifests finais têm os mesmos hashes de DLL e runtime. V3 original preservado: `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD`. V4 original preservado: `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`. Perfis ficam em `.tools/validation/`; seus saves de fixture não foram copiados para os saves pessoais. O addon de testes é temporário e removido ao terminar.

Compilação do mod: zero erros/avisos. Compilação do runtime: zero erros, aviso NU1900 por indisponibilidade da consulta online de vulnerabilidades do NuGet; usa referências/pacotes locais já restaurados. Não houve incorporação de dependência nova.

## Próxima etapa

Completar as lacunas da Entrega A e iniciar a Entrega B: demanda de equipamento com responsável, bancada, pesquisa, material e confirmação de equipamento vestido; depois arbitrar construção/fabricação/pesquisa e reservar recursos sem bloquear comida, medicina ou alterações manuais. Preservar V3/V4 como regressões.
