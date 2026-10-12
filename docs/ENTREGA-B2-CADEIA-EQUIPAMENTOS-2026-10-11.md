# Entrega B2 — demandas persistentes e coleta independente

Continuação da coordenação de construção/pesquisa/fabricação. Este pacote liga a indústria ao executor de coleta sem depender da construção estar ligada e registra demandas individuais até o equipamento efetivamente vestido.

## Alterações

- Uma meta identifica colono, checkpoint e peça. Mantém criação, última mudança, responsável atual/previsto, motivo de espera e confirmação de conclusão no save.
- Estados separam pesquisa, bancada, emissão de Bill, bloqueio de produção, trabalho nativo, peça disponível e peça equipada. Material no chão ou receita terminada não conclui a entrega.
- Excluir um colono, desligar autoequipamento, emergência, política/peça manual ou editar/suspender uma Bill preservam o controle do jogador. Demandas são reconciliadas com o inventário e as Bills atuais.
- Perder/desgastar uma peça reabre a demanda quando ela deixa de satisfazer o checkpoint; metas de checkpoints anteriores permanecem histórico, sem fingir que foram entregues.
- Janela de revisão de dois dias por meta, exibida quando vencida; é um aviso para revisão, não ETA nem autorização para forçar trabalho/ignorar sobrevivência.
- Coleta roda no ciclo de gerenciamento quando construção **ou** equipamentos estão ligados. Corrige a dependência anterior de `ExecuteConstruction` e da existência de projetos de base.
- O executor existente calcula ingredientes minerais das Bills próprias intactas, considerando filtros/restrições fixas. Marca fontes locais por corte/mineração nativos, com limites de quatro novas marcações por ciclo e 16 pendentes, respeitando trabalho, proibição, distância e acesso seguro.
- Desligar os dois módulos remove somente as marcações próprias; ordens manuais permanecem.
- A aba Diagnóstico apresenta metas pendentes por colono, contagem de peças equipadas, etapas bloqueadas e responsáveis.

## Limites

Não é um grafo universal nem uma reserva exclusiva por destinatário. O módulo anterior continua coordenando fabricantes e protegendo o próximo lote contra novas obras opcionais. Responsável previsto pode mudar; só job nativo indica fabricação em andamento. A reconciliação normal é a cada 600 ticks.

O comércio já existente calcula os materiais do checkpoint. Este pacote não emite compra/viagem nova nem promete aquisição de material ausente do mapa. Cultivo de tecido e precursores industriais ainda usam os planejadores existentes; não há resolução recursiva universal de receitas alternativas ou alocação exclusiva de materiais por todas as metas.

Metas históricas são limitadas a 64, além das metas do checkpoint atual. Conclusão individual persistida não implica que todos os conjuntos ou a campanha estejam aprovados. Nenhum código externo foi copiado neste pacote.

## Validação

Produção compilada sem avisos/erros; runtime sem erros, com aviso NU1900 do serviço de auditoria NuGet indisponível. Testes em cópias isoladas dos saves pessoais, seis IDs originais, sem alterar skills/traits/implantes. A fixture da cadeia concede bancada abastecida, minério e estado inicial de necessidades; após início, aço, fabricação e equipamento devem resultar de jobs nativos. Aparência da HUD não recebeu inspeção visual neste pacote.

Ensaios preservados sem aprovação:

- `equipment-chain-20261011-225926-767`: falha da fixture, Bill genérica criada para receita que exige a versão nativa com item inacabado. Corrigido para `recipe.MakeNewBill()`. Executor agora reprova também exceções de JobDriver, evitando aprovar uma sessão com erros de trabalho.
- `equipment-chain-native-bill-20261011-230253-728`: mineração e fabricação nativas observadas, mas raid interferiu antes da entrega. Sessão própria encerrada após conferir PID/CommandLine; sem aprovar entrega ou sobrevivência. Nova fixture pacífica preparada desde o início. O painel foi ajustado para apontar ameaça imediata mesmo com a automação de emergência desligada.

PASS `equipment-chain-peaceful-20261011-230640-770`: 13 verificações. Mineração solicitada com base desligada; mining nativo observado; espada fabricada a partir do aço minerado e equipada em Bravo por trabalho nativo. Identidade, criação e conclusão da meta sobreviveram a save/load em `EquipmentChainEvidence`. Desligamento removeu as marcações próprias e preservou uma ordem manual de mineração. Setup pacífico com bancada abastecida e seis veios conhecidos; não prova campanha econômica sem fixture.

DLL final instalada/testada `48289195498BA9E38F002D7643E7C46A6E35F592F40D9CEE2B2ED4ACA743DFE9`; runtime `E98904657D70A69CC88C048F2CE5269140FDD2108AE0AE1670084593B6372767`. Commit-base `fb5d233`, alterações locais ainda não commitadas/publicadas.

- PASS `chain-coordination-contracts-20261011-230846-247`: 11 contratos do pacote anterior (viabilidade, orçamento, obra essencial/opcional, responsáveis e controle manual).
- PASS `chain-research-regression-20261011-230922-578`: pesquisa do V3 0 → 25,63774 em 2.952 ticks; diagnóstico acompanhou progresso real.
- PASS `chain-parallel-regression-20261011-231012-924`: em 41.987 ticks, cinco peças fabricadas, ranged com colete 0 → 3, pesquisa +157,4076 pontos e banquinho inicial concluído nativamente. Um blueprint foi concedido no setup; materiais/jobs/necessidades/research não foram concedidos nesta regressão. Confirma coordenação com demanda concreta, não construção integral de base.

Hashes pessoais conferidos sem alteração: V3 `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD`; V4 `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`. DLL no jogo igual à produção testada. Addon temporário removido após encerrar os ensaios. Manifests/logs ficam em `.tools/validation`, incluindo falhas de preparação e a interferência por raid.

## Próximo checkpoint

Expandir dependências para bancada/receitas de componentes e cultivo de tecido, tornar urgência/deadline acionáveis com segurança e ligar faltas sem fonte local ao planejamento comercial existente. Preservar cadeia minério → fabricação → equipamento e regressões V3/V4.
