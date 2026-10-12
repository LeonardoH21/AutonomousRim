# Construção administrativa dos módulos — 11/10/2026

## Escopo

Teste autorizado com pesquisas e construções instantâneas, em cópia isolada do cenário EliteSix. Os seis colonos originais são mantidos; o mundo de teste usa dificuldade Peaceful, solo firme uniforme, recursos fornecidos e velocidade 3×. A simulação constrói as tarefas calculadas pelos planejadores de produção; a estrutura não é copiada do save de referência nem montada com coordenadas alternativas para ocultar problemas.

O teste é de montagem e funcionamento dos módulos atuais. Não comprova construção por trabalho normal, progressão natural de pesquisa, sobrevivência por 20 dias, combate, comércio efetivo ou recrutamento. Os modelos completos do save `13X13 Todas as construções` continuam catalogados em `MODULOS-REFERENCIA-DO-USUARIO.md`; sua adaptação integral ainda está pendente, incluindo quartos com circuito térmico compartilhado, hospital avançado, dois depósitos/oficinas e o campo misto de medicina/morango.

## Verificações

- Colocação nativa de cada móvel/parede/cabo com `GenConstruct.CanPlaceBlueprintAt` antes de criar a construção instantânea.
- Paredes, portas, pisos e móveis existem efetivamente no mapa; telhados nas salas fechadas.
- Doze funções de sala: quartos, cozinha, abate, estoque, freezer, refeitório/recreação, energia, pesquisa, oficina, hospital, fabricação e laboratório.
- Oito bancadas da lista do planejador, incluindo usinagem, drogas, fabricação, fundição e refinaria; interação livre e caminho disponível.
- Cinco campos completos, com zonas nativas de cultivo descobertas.
- Estoques, filtros de comida, prateleiras de três pilhas/célula, espaço de medicina, camas médicas e camas de prisão válidas.
- Demanda real de estoque criada por pilhas físicas, para testar a expansão de prateleiras.
- O plano deve parar de acrescentar tarefas: três reavaliações consecutivas estáveis, depois da expansão.
- Consumidores elétricos ligados e freezer com termostato −2 °C e temperatura efetiva abaixo de zero.
- Salvar, recarregar e repetir as verificações no jogo. A exportação remove exclusivamente os componentes e metadados do complemento de testes.

## Defeitos e correções

**Energia insuficiente:** o planejador tinha um limite fixo de seis geradores. A montagem com todas as instalações pesquisadas ultrapassou 6 kW, e o sistema elétrico nativo desligou consumidores, apesar de os cabos estarem conectados e os geradores terem combustível. Removido o limite fixo; a quantidade segue o consumo máximo previsto, usando a potência nativa do gerador e uma reserva de 15% ou 200 W, o que for maior. A regra continua criando tarefas normais, sem gerar recursos/construções gratuitamente na gameplay.

**Aquecimento de geradores:** o filtro excluía a sala principal de energia, mas esquecia os módulos de geradores auxiliares. Eles receberam aquecedores desnecessários, ampliando o consumo. Novos planos não aquecem esses módulos. Construções já existentes do jogador não são demolidas automaticamente.

Também foram corrigidos problemas do próprio teste: conclusão prematura antes da configuração dos estoques, interferência de ameaças sinalizadas pela pesquisa e uso de `Danger.None` como prova de bloqueio físico. Um freezer frio pode ser alcançável e ainda ser rejeitado por essa política térmica; o teste registra a diferença e não altera as políticas dos colonos para fingir segurança.

## Evidências

As execuções intermediárias preservam os logs e saves de falha em `.tools/validation/all-modules-*`. A execução `all-modules-power-fixed-20261011-165719-557` passou antes e depois de salvar/recarregar: 63 consumidores ligados, 7.540 W de consumo nominal, nove geradores, 47 prateleiras, 28 luzes, seis quartos e freezer abaixo de zero. Ela ainda incluía aquecedores nos geradores auxiliares; a execução final verifica a correção adicional de eficiência.

O cenário original manteve SHA256 `94B605291A81B328FC8C518B3962167222071638F5A9D090060B5473522062AF`. O save de referência do usuário manteve SHA256 `D5B5BFEDE9E265261EDB8DB0DD6A53A15D6600EB72EE140C28ABB638CE46D81A`.

**Resultado final aprovado:** `.tools/validation/all-modules-efficient-final-20261011-170204-643`. O plano estabilizou na rodada 21 com 93 projetos e 4.149 tarefas (inclui preparações, acabamentos e tarefas compartilhadas). Antes e depois do save/load, as doze funções de sala, as oito bancadas, cultivos, medicina e prisão passaram. Consumo nominal: **6.125 W**, atendido por **oito geradores**; **54 consumidores ligados**. Freezer: **−1,6 °C** antes e **−2,0 °C** após recarregar, termostato −2 °C. Isso reduz o consumo nominal em **1.415 W** em relação à primeira correção, mantendo a reserva elétrica.

Save final de revisão no jogo: **AutonomousRim-Modulos-ADMIN-11-10-V2**. Cópia exportada: `outputs/ModuleAudit/AutonomousRim-Modulos-ADMIN-all-modules-efficient-final-20261011-170204-643.rws`. A versão anterior é preservada; V2 é a que inclui as duas correções. As automações ficam desligadas no save de revisão, mantendo a base pronta disponível para inspeção. Os módulos são os do planejador atual, não uma reprodução integral dos sete modelos de referência ainda não importados.

Reprodução: `scripts/AllModulesTest.ps1 -Visible`. Exportação somente após aprovação nativa: `scripts/ExportAdminModulesSave.ps1 -ValidationProfile <pasta-do-teste>`. A compilação do mod passou sem avisos; o complemento de testes apresentou somente o aviso NU1900 de consulta indisponível ao NuGet. Os 57 checks de políticas também passaram.
