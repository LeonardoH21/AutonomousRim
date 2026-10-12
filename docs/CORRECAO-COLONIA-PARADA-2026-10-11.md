# Correção de colônia parada — 11/10/2026

## Causa observada
No save real `Teste V2`, todos os seis colonos estavam em recuperação pessoal e tinham zero horários Work. Os trabalhos produtivos estavam majoritariamente zerados pela própria IA (não eram alterações manuais). Recreação estava entre 89% e 98%, descanso entre 56% e 74%; alimento zero, desnutrição leve e humor entre 24% e 39%. Nenhum colono usava roupas. A recuperação exigia humor >=40% para terminar e convertia todos os blocos Work em Anything. Fome/humor também excluíam colonos das funções e zeravam prioridades, agravando a falta de comida.

## Mudança
- Fome, humor baixo e falta de recreação não desativam todos os trabalhos. Incapacidade, ferimento sério, recrutamento, estado mental e exaustão continuam protegidos.
- A agenda mantém blocos de sono (22–06), trabalho (08–17), lazer (17–19) e Anything nos intervalos. Corujas noturnas invertem a rotina; meditação continua quando aplicável. Necessidades alteram a hora atual e a seguinte, preservando o restante do dia. O flag antigo de recuperação pessoal é recalculado, liberando saves travados.
- Cada prioridade gerenciada guarda uma rotina persistida, inicializada por habilidade/paixão e distribuição de carga: construção, cozinha/warden, cultivo/corte, fabricação, pesquisa, mineração/transporte/limpeza e medicina. As atividades de apoio ficam habilitadas. Caça requer arma apropriada.
- Demanda e urgências ainda permitem ajustes temporários. Um bônus pequeno preserva especialistas habituais sem superar grandes diferenças de habilidade. Crafting com ordem pendente recebe prioridade de produção. Alterações manuais, desativação e restauração continuam respeitadas.

## Evidência
A primeira execução sobre cópia isolada do save travado passou: todos os seis retomaram trabalho nativo (FinishFrame e CutPlantDesignated). Não foram adicionados recursos/equipamentos, alteradas necessidades nem substituídos colonos. Observação de 12.000 ticks, velocidade 3×; comprova retomada de trabalho, não recuperação completa da fome nem sobrevivência prolongada.

Teste WorkSchedule ampliado PASS em `.tools/validation/work-schedule-stable-verified-20261011-184415-993`: paixão/skills, comida, mineração e corte com demanda real, construção, incêndio, costura, médico, ferimentos leves, descanso, humor/recreação baixos sem deadlock, rotina noturna, meditação, pós-emergência, save/load e alterações manuais. Um colono exausto foi dormir por decisão nativa e recuperou descanso.

Produção compila sem avisos/erros. Testes compilam com NU1900 (consulta de vulnerabilidades NuGet indisponível). DLL instalada: D998E671AEAA71572666FC33CD57D10CB346C7EDE026823178A59012A56154A2.

Verificação final no save real: PASS (os seis colonos retomaram trabalho nativo por 12.000 ticks) em `.tools/validation/idle-colony-final-20261011-184506-784`. Original preservado, SHA256 BB150A30C059E58A21CD121100BAB93066EFAFA2E7C32AD8A64DC1AA37301008. A cópia do teste usa perfil separado e não sobrescreve a partida.
