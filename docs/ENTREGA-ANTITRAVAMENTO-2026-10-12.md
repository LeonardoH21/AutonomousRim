# Prioridade 1–10 — auditoria e reforço contra travamentos

A lista enviada pelo usuário substitui a sequência de novos recursos como prioridade atual. Revisão sobre os pacotes A/B1–B5 já existentes; não presumir que cada item começou do zero ou foi universalmente concluído.

## Correções desta entrega

- Obras: identificador usa LayoutSlot quando disponível, evitando colisão entre várias adições industriais na mesma sala. O alerta externo do watchdog só é exposto com construtor habilitado, apto e em turno; progresso/paralisação nativa continuam sendo medidos pelo executor existente.

- Pesquisa: elegibilidade exige pesquisador atribuído, aptidão/turno, bancada compatível energizada, célula utilizável, área/rota e reserva. Bancada proibida ou reservada não acumula falso tempo disponível. Medição continua pelos pontos reais do ResearchManager. Não chama a notificação ideológica nativa durante uma inspeção; restrições especiais de mods/ideologia continuam sujeitas ao job giver.
- Bills: separa ausência conhecida de estoque, filtro da Bill, raio, proibição/área e causas elétricas/combustível. Mostra alternativa de material com quantidade nativa necessária e pilhas candidatas, sem confundir candidato com combinação válida. A busca nativa mantém a autoridade sobre mistura, qualidade, reservas e ingredientes de peças inacabadas. As demais causas de ingrediente/acesso/reserva podem continuar agrupadas na mensagem de candidatos; não é um solver universal de mods.
- Conflitos: pesquisa/produção viáveis mostram job atual e até quatro profissões com prioridade concorrente. Informação explicativa, não diagnóstico falso de que toda profissão ativada realmente possui jobs pendentes.
- Estabilidade: bônus moderado de seis pontos mantém os especialistas anteriores quando continuam aptos. Não é um bloqueio de duração fixa; sobrevivência, saúde, recuperação e prioridades manuais continuam podendo mudar a escolha. Relatório de funções mostra suplente habilitado ou ausência dele; não promete execução imediata.
- Reserva: até duas próximas ordens próprias podem proteger estoque parcial disponível, mesmo antes de existir lote completo, sem declarar crafter pronto. Não bloqueia fisicamente pilhas nem interfere em blueprints manuais. Obras opcionais High agora respeitam o orçamento. Critical, cascas essenciais de abrigo/comida/hospital e infraestrutura elétrica básica têm exceções explícitas.
- Auditoria: cache de coordenação é invalidado quando muda pesquisa selecionada, quantidade de pesquisas concluídas ou quantidade de bancadas, além dos gatilhos existentes de emergência/toggles/manualmente forçado. Recarregar recria a coordenação e zera amostragem elegível; metas persistidas são reconciliadas contra objetos nativos.

## Cobertura da lista

| Ponto | Implementação/evidência e limite |
|---|---|
| 1. Progresso real | Pontos de pesquisa, trabalho restante/lotes nativos e watchdog de construção. Reforço novo da elegibilidade de pesquisa; testes reais de fabricação e trabalho simultâneo. |
| 2. Bills | Busca nativa e diagnóstico de pesquisa/profissão/habilidade/turno/energia/combustível/área/raio/filtro. Mensagens mais específicas nesta entrega; mistura e reservas complexas não recebem uma causa universal única. |
| 3. Coordenador | WorkCoordination/WorkPriorityManager existentes distribuem fabricação, construção, pesquisa e medicina; ConstructionWorkManager respeita especialistas. Novos conflitos explicados e estabilidade. Sem exclusividade absoluta: trabalhos de sobrevivência e decisões manuais podem competir legitimamente. |
| 4. Necessidades relacionadas | EquipmentDemandState liga destinatário, etapa, receita, pesquisa, bancada, recursos, fabricante e peça equipada; IndustrySupplyPlan/B5 abastecem dependências. Objetivos gerais de todos os mods ainda não compartilham um DAG universal; fluxo industrial nativo já integrado. |
| 5. Materiais | Orçamento consultivo limitado a próximas ordens próprias. Corrigidos High opcional e estoque parcial. Não reserva todo o loadout futuro. |
| 6. Especialistas/suplentes | Funções persistem pela rotina, bônus reduz oscilações, aptidão recompõe alocação; suplente apresentado. Escolha não garante mesmo desempenho do especialista nem bancada livre. |
| 7. Emergências locais | Comportamento existente: Recovery permite economia, paciente seguro não ativa perigo global, fogo remoto não é CriticalFire. Nenhum código de combate/emergência alterado nesta entrega. |
| 8. Menos mudanças | Rotinas base existentes, bônus novo e configurações manuais preservadas. Não introduz cooldown que impeça medicina/comida emergencial. |
| 9. Resultados | EquipmentDemandState diferencia ordem, pronto, crafting, delivery e equipped; contagem nativa de lotes e metas carregadas não presume entrega. |
| 10. Load/pesquisa | ResetSampling/referências nativas existentes; invalidação nova por pesquisa e bancadas. Receitas ficam sujeitas aos requisitos do jogo e revisões normais. |

## Referência

[AnalyzeCommands do AutoRim](https://github.com/Critical-Reynolds/autorim-mcp/blob/main/mod-src/AutoRim/Commands/AnalyzeCommands.cs) foi consultado diretamente: classificação de trabalhadores, explicação de ociosidade e bloqueios. Implementação própria sobre o código/nativas do AutonomousRim; nenhum código externo copiado.

## Validação

Produção compilada sem avisos/erros. Runtime tem somente NU1900 por feed de vulnerabilidades indisponível. Testes em cópia de V4, originais preservados, 3×; contratos usam setup descartável, sem alterar a campanha pessoal. PASS `antistall-diagnostics-complete-20261012-041250-029`: 34 verificações; estoque/filtro/raio, energia, habilidades, turno, área/posição/reserva de pesquisa, conflito, identidades de projetos, sono dos construtores, progresso antes da conclusão, blocos de mármore fabricados e save/load.

PASS `antistall-coordination-20261012-041332-551`: 15 verificações; orçamento parcial, expansão High opcional, exceção Critical, estabilidade de funções/suplentes, proteção do crafter, falta de ingredientes, suspensão e prioridades manuais.

DLL `5BC0F76B1229A323F16BF987C78AB720C112377088C5D254266F0C1E8438602D`; runtime `C61DB335A73C9897D169611AD4E124C3828FF08731A5255B96D735A2A21CFC90`.

PASS `antistall-native-parallel-20261012-041402-525`: três peças fabricadas, coletes ranged 0 → 3, pesquisa +160,62 e banquinho nativo concluído; três novos móveis, 39.447 ticks. Limite de 60.000 mantido, sem novos recursos, pesquisa, necessidades ou jobs concedidos. Não usar a diferença em tempo contra o teste anterior como benchmark: não houve comparação controlada de várias repetições.

PASS `antistall-industrial-infrastructure-20261012-041732-171`: 23 verificações de B5; forja/prateleiras construídas, tecido transportado e calças fabricadas por jobs nativos, controle manual e save/load preservados. Mesmas DLLs.

Total: 72 verificações explícitas e regressão simultânea nativa. DLL instalada conferida igual à compilada, addon temporário removido e nenhum processo de teste permaneceu aberto. V3/V4 pessoais mantêm hashes `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD` e `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`. Alterações locais sobre fb5d233; sem novo commit/push.

Nenhum teste visual da HUD, combate completo ou benchmark de campanha neste pacote. A auditoria distingue explicitamente componentes existentes, mudanças novas e limites de integração universal.

Falha preservada `antistall-diagnostics-20261012-040107-563`: a hipótese de nenhum estoque era incorreta porque V4 contém pedras fora do raio. Setup passou a remover apenas os ingredientes conhecidos da receita no cenário de ausência total; os casos de filtro e raio são construídos separadamente. Critérios nativos de prontidão/conclusão mantidos.


Outras preparações preservadas:
- `antistall-diagnostics-revised-20261012-040332-957`: automação desligada restaurou pesquisa anterior, e o fixture não tinha projeto selecionado. Passou a selecionar explicitamente uma pesquisa inacabada disponível compatível no setup.
- `antistall-diagnostics-explicit-research-20261012-040615-095`: execução copiou runtime anterior enquanto a compilação ainda terminava. Descartada; não é evidência da correção. Após isso, aguardou-se a conclusão e conferiu-se o manifesto.
- `antistall-diagnostics-final-20261012-040703-286`: isolamento de bancada de pesquisa era incompleto, com alternativas no mapa. Fixture final contém uma única bancada e verifica restrição nativa de área, obstrução e reserva separadamente, em vez de presumir que SetForbidden sempre limita esse prédio.
