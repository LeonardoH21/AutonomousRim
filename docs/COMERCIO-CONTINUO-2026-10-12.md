# Comércio contínuo e financiamento dos loadouts — 12/10/2026

Pedido de origem: anexo 8360518d-3917-4dff-bb6e-263d4aa32d21/Texto colado.txt. Implementação por etapas e checkpoints nativos; saves pessoais preservados.

## Implementação

- Venda sem compra e novas negociações com o mesmo visitante/nave. Revisão padrão de 5.000 ticks (duas horas), ajustável para 1/2/4 horas na HUD. Estoque/oferta alterados podem antecipar lotes maiores; oportunidades idênticas não repetem deslocamentos. Load invalida o cache.
- Exclusivamente SmokeleafJoint. Metas persistidas: reserva comercial de 60 baseados, objetivo de 1.000 prata e piso protegido de max(150, colonos × 50). Objetivo de acumulação é separado do piso protegido.
- Bills por financiamento, reserva e capacidade real de armazenamento, incluindo três pilhas por célula de prateleira. Primeiro lote até 100; até 500 com comprador recente. Plantação comercial limitada inicialmente a 16–36 células, depois até 144. Folhas, terreno e mão de obra são reais.
- Comida baixa, incêndio, ameaça e recuperação pausam atividades conforme sua urgência sem apagar metas. Políticas gerenciadas bloqueiam consumo recreativo/inventário de baseados; tratamento de dependência e políticas manuais permanecem respeitados.
- Déficits descontam estoque permitido no chão, inventários, itens carregados pelos colonos presentes, compras em trânsito e produtos vinculados a unfinished things reais. Fabricação pendente não é estoque concluído nem equipamento vestido.
- TradeDeal nativo com preços, dinheiro do comprador, filtros, acesso e reservas. Pilhas proibidas/reservadas são excluídas somente da sessão automática; não bloqueiam as demais. Cestas sem verba perdem compras menos prioritárias em vez de cancelar tudo.
- Comida/medicina urgente precedem infraestrutura e upgrades. Peça pronta desconta seus ingredientes. Componentes prontos precedem precursores, inclusive entre demandas industriais e militares, evitando financiamento duplicado. Materiais restantes seguem prioridade/gargalo, com plasteel, urânio e ouro à frente dos materiais comuns quando a prioridade é equivalente.
- Comparação de equipamento pronto considera receita, pesquisa, bancada utilizável, fabricante, qualidade/usabilidade e preço. Peças existentes e em fabricação reduzem compras redundantes.
- Entregas orbitais guardam IDs e são confirmadas na abertura real do pod e aumento de estoque acessível. Produção local e prazo vencido não confirmam pod identificado. Console, energia, cobertura, orçamento e partida são reavaliados.
- Caravanas preservam defensores, suprimentos, capacidade e rota segura. Compras só entram no estoque da colônia no retorno/desembarque real. Rotas e controles manuais continuam respeitados.
- Especialista militar protegido das Bills comerciais gerenciadas; negociação não interrompe jobs de equipamentos/baseados em curso. Reservas existentes protegem fabricação viável contra novas expansões opcionais.
- Progressão: inicial, flak/SMG, assault rifle/escudos, Marine/Charge Rifle, Cataphract. Dois melee/quatro ranged definidos pelos perfis; camisa/calça e proteção intermediária mantidas. Royalty permite meta de monoespada melee por comércio/loot; nenhuma receita inexistente foi criada.
- Política compartilhada com otimização nativa de roupas prioriza proteção faltante dos melee e impede trocar proteção militar por peça inferior. Hipotermia/insolação grave permitem prioridade térmica. Exclusões de colonos, políticas e peças manuais são respeitadas.
- HUD: reservas, financiamento estimado, estoque/meta, transações/baseados realmente vendidos, compradores/partida, próxima revisão, impedimentos, histórico, materiais reservados/em trânsito/em fabricação, peças por colono, pesquisas, bancadas e responsáveis/substitutos aptos.

Defs Royalty instaladas conferidas: armadura Cataphract = 150 plasteel + 50 urânio + 6 componentes avançados; capacete = 50 plasteel + 1 componente avançado. Seis conjuntos = 1.200 plasteel, 300 urânio e 42 componentes avançados, antes de armas, escudos, roupas e precursores. Neutroamina não integra esses equipamentos/baseados; integra demanda médica quando houver receita de medicina.

## Validação e proveniência

Cópias isoladas de TESTE V3 e seus seis colonos originais Alpha/Bravo/Charlie/Delta/Echo/Foxtrot. IDs são verificados após load e ao concluir. Skills, traits, genes e implantes preservados; sem substituição por colonos gerados. Enhanced World, Cassandra/Medium, fatores aprovados e velocidade nativa 3x. Necessidades preenchidas apenas na preparação inicial.

Fixtures fornecem bancadas, materiais iniciais, prata e oferta do comprador. O ciclo agrícola começa com plantas maduras para reduzir espera biológica. Colheita, replantio, fabricação, transação, entrega, viagem e equipamento são nativos; nenhum resultado é simulado como concluído. Peças inicialmente vestidas são aproveitadas: comprovamos fabricação/equipamento das peças faltantes e conclusão dos seis conjuntos, não fabricação de todas as peças desde zero.

| Etapa | Resultado |
|---|---|
| Visitante + colheita/replantio + gear | Ciclo retomado passou: 84 baseados fabricados, 45 vendidos novamente ao comprador inicial, seis conjuntos iniciais completos, 964 prata ao final. Venda adicional a segundo visitante natural. |
| Orbital | DLL final passou: 40 iniciais + 20 recém-fabricados vendidos à mesma nave; 379 prata acessível ao término, piso 300. Prateleira cheia ignorada em favor de outro destino coberto. Pods pendentes reduziram déficit; load em trânsito e após entrega passaram. Regressão final passou. |
| Guardas | Comprador sem dinheiro, recusa, sessão manual, alimentação baixa, incêndio, takeover manual, reserva, load/desligamento passaram. Regressão final passou. |
| Caravana na DLL final | Formação, transporte, negociação, retorno vivo, compras desembarcadas, load e desligamento passaram; dois viajantes/quatro residentes. |

O ciclo de gear foi retomado de checkpoints, corrigindo disputa de armadura e troca de capacete por chapéu. Incidentes aleatórios interromperam uma tentativa. A fixture econômica final usa autonomousrimcommercepeaceful: remove ameaças na preparação e bloqueia novos incidentes hostis somente no add-on de testes, mantendo storyteller/dificuldade/fatores. Essa opção não está no mod instalado de produção. Tentativas interrompidas/fracassadas foram preservadas e não contam como aprovações.

Resultados e hashes: outputs/Commerce/2026-10-12/results.json. Logs/saves completos: .tools/validation/commerce-*. Gear passou na revisão imediatamente anterior ao últimos ajustes da ordem de compra de componentes e seleção de destino orbital; regressões orbital/guardas/caravana validam a DLL posterior. Esses ajustes não alteram fabricação/auto equipamento.

Limites: não se esperou crescimento completo desde semeadura até primeira colheita; não foram fabricados seis conjuntos Cataphract. Progressão e custos derivam de defs/receitas reais. HUD compilada, sem inspeção visual nesta execução batch.

## Reprodução e retomada

```powershell
.\scripts\FunctionalStage.ps1 -Stage commerce-full-cycle -Flag autonomousrimcommercetest -SuccessMarker '[CommerceTests] DONE' -ExtraFlags autonomousrimfixturebaseline,autonomousrimelitesix,autonomousrimcontinuoustrade,autonomousrimcommercegear,autonomousrimcommercecrop,autonomousrimcommercepeaceful -ResumeSave 'C:\Users\Administrador\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\TESTE V3.rws' -TimeoutSeconds 1800
```

Para retomar, usar CommerceCycleCheckpoint.rws como ResumeSave e manter flags. Não recompilar/instalar durante execução. Bill/trader/ship/IDs/progresso persistem. O runner não altera o save fonte.

Saves pessoais preservados: TESTE V3 SHA256 01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD; TESTE V4 SHA256 A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE.

