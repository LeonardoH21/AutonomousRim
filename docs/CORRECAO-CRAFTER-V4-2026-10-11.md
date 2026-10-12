# Crafter e conjuntos ranged — TESTE V4, 11/10/2026

## Evidência do save

Arquivo `TESTE V4 gargalo sets rangeds.rws`, SHA256 `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`.

As bancadas de alfaiataria, forja e usinagem estavam energizadas. Havia ordens ativas de coletes flak e capacetes avançados. A colônia tinha cerca de 91 dias de alimento; não era uma suspensão por fome. O checkpoint 2 indicava quatro ranged e nove peças faltantes; Heavy SMG ainda exigia pesquisa, e os capacetes exigiam plasteel.

A distribuição de funções escolhia fabricação depois da pesquisa e podia atribuí-la a trabalhadores com prioridades manuais concorrentes, ou ao trabalhador de cultivo. A atribuição nominal não assegurava prioridade real de fabricação. O executor de construção também podia iniciar trabalho nos intervalos entre ordens nativas de fabricação. A demanda de ingredientes minerais ignorava ingredientes fixos quando não apareciam no filtro de materiais variáveis da bill — o jogo ignora esse filtro para ingredientes fixos, como plasteel e componentes.

## Alterações

- Reserva um fabricante de equipamentos antes da pesquisa e de funções secundárias, com pelo menos três trabalhadores aptos e condições seguras. Escolhe habilidades e continuidade da função; preserva prioridades manuais e procura alguém cujas tarefas concorrentes possam ser rebaixadas.
- O fabricante concentra as categorias com equipamentos pendentes em prioridade 1. Construção, transporte e produção secundária continuam como alternativas; sobrevivência, medicina, descanso e emergências permanecem protegidos. Quando as ordens ficam satisfeitas/suspensas, a reserva deixa de ser necessária.
- O executor de construção não toma o fabricante nos intervalos entre bills prioritárias. O escalonador nativo continua podendo executar tarefas alternativas quando não há ingredientes.
- O levantamento de recursos usa a mesma regra de ingredientes fixos do `WorkGiver_DoBill` nativo. Plasteel/componentes podem gerar designações normais de mineração mesmo que o filtro variável permita apenas aço.

## Teste no V4

`.tools/validation/gear-v4-fixed-20261011-204908-339`: PASS. Cópia do save, seis colonos originais, 3× e dificuldade pacífica somente no perfil descartável, para isolar produção de raids. Nenhum recurso, habilidade, necessidade, pesquisa concluída ou job foi concedido.

Em 41.262 ticks, foram concluídas cinco peças nativamente: uma camisa, dois dusters e dois coletes flak. Ranged com colete equipado passaram de 0 para 3: um colete já existia no chão, os outros dois foram fabricados. Bravo manteve fabricação principal; Foxtrot também fabricou coletes com suas prioridades existentes; Charlie continuou no job nativo de pesquisa. Isso comprova fabricação e equipamento, não a conclusão de todos os loadouts. SMGs continuam dependentes da árvore de pesquisa e os capacetes dos materiais necessários.

A primeira observação, com dificuldade original, foi interrompida por raid antes de completar a observação de produção; não é uma comparação de desempenho controlada.

DLL instalada: `33EF1698014337FC2899BA544B585FE00B4F99F90ACB07F0303604C993E31C4B`. Save original preservado.

Regressões finais com a mesma DLL: `.tools/validation/gear-work-verified-20261011-205745-976` PASS (17 verificações, incluindo foco do fabricante, proteção contra construção e designação de mineração de plasteel para ingrediente fixo); `.tools/validation/gear-research-verified-20261011-205816-450` PASS (Charlie pesquisou nativamente no V3, Metalurgia 0 → 25,01505 em 6.134 ticks).

O teste adicional inicialmente escolhia `Make_Patchleather` como primeira receita com Crafting, que não produz equipamento. A verificação foi corrigida para escolher uma receita que produz apparel, e então confirmar dedicação em prioridade 1. As falhas anteriores desse teste não representavam falha da fabricação no V4.
