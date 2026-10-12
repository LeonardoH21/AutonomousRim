# Pesquisa parada no TESTE V3 — 11/10/2026

## Diagnóstico no save real

- Metalurgia (`Smithing`) selecionada, progresso zero, duas bancadas simples construídas. Plano estratégico ativo, estabilidade 94/100 e aproximadamente 21 dias de alimentos; não era falta de bancada ou de comida.
- Save em `Securing` após incêndio externo. A regra antiga promovia qualquer fogo a menos de 12 células de uma construção ou 15 de um colono a emergência global. No teste, voltou a `Danger` e zerou Pesquisa de todos, deixando diversos colonos vagando.
- Mesmo após retornar à rotina, o candidato Delta tinha Construção 1 e Pesquisa 2 protegidos como alterações manuais. Além disso, a proteção do pesquisador contra trabalhos concorrentes só existia para pesquisas necessárias a construções. Selecionar uma tecnologia não garantia alguém trabalhando nela.

## Correção

- Fogo próximo mantém Firefight 1. Emergência global por fogo passa a exigir chamas a até 3 células do footprint de uma construção ou até 2 de um colono que não esteja apagando fogo. Hostis continuam acionando emergência normalmente. Isso permite trabalhar durante incêndios externos, sem ignorar fogo alcançando a base.
- Na recuperação, com Work Automation ativo, o controlador de emergência restaura e libera suas prioridades. O gerenciador normal volta a avaliar medicina e necessidades; os dois controladores não disputam a mesma prioridade nem confundem mudanças próprias com alterações manuais.
- Pesquisa ativa reserva especialista em condições seguras, mesmo quando não desbloqueia uma construção. Tarefas concorrentes gerenciadas recebem prioridade inferior. Colonos com prioridades manuais incompatíveis são preteridos quando há alternativa; desativações manuais continuam preservadas.
- Uma pesquisa selecionada manualmente também pode receber trabalhador pelo Work Automation, sem exigir que a seleção automática de pesquisas esteja ligada. Alimentação crítica, cuidados urgentes, incapacidade e ameaças continuam tendo precedência.

## Evidência nativa

Save original SHA256: `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD`.

Antes: `.tools/validation/research-v3-before-20261011-192438-022`: 30.000 ticks, Metalurgia 0 → 0; nenhum job nativo de Research observado.

Depois: `.tools/validation/research-v3-fixed-20261011-192840-508`: PASS. Bravo assumiu o job nativo `Research` com prioridade 1, e Metalurgia avançou 0 → 25,06141 em 4.517 ticks. Charlie fazia `FinishFrame`, Echo `DoBill`, Alpha e Delta transportavam materiais. Teste a 3× em cópia isolada, com os seis colonos originais, sem spawn de recursos, alteração de necessidades, pesquisa instantânea ou job de pesquisa forçado.

DLL de produção instalada e verificada: `76355F4B46C18D3245537D59B8416FB2ED13663C2498D358214A0C64389171DF`.

Regressões nativas também passaram com a mesma DLL: `.tools/validation/research-work-regression-20261011-193042-490` (16 verificações de prioridades, incêndio distante versus crítico, sono nativo, horários e alterações manuais); `.tools/validation/research-emergency-regression-20261011-193115-081` (11 verificações de ameaças, resgate, rotas seguras, recuperação e restauração de prioridades).

Este teste prova a retomada nativa; não representa a conclusão da árvore tecnológica. O save original é preservado, e pode ser continuado com a DLL corrigida.
