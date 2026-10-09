# Prisão automática

Implementada em 08/10/2026. Compilação contra as DLLs locais do RimWorld 1.6; testes no jogo continuam adiados por solicitação do usuário. Este documento descreve o código, não resultados observados de partidas.

## Ativação

Na aba AutonomousRim, dentro do painel rolável, ligar **Prisão automática**. O módulo começa desligado, inclusive em saves antigos. Base automática permite construir; Comida e Prioridades automáticas ajudam a manter produção e distribuição de trabalho. A meta inicial é oito colonos, ajustável de três a vinte na HUD.

## Construção e expansão

- Depois do estoque funcional, procura espaço firme e acessível a até 45 células do hospital ou estoque, preservando plantas, zonas e projetos existentes.
- Reserva bloco 11×11. Duas celas individuais com interior 4×4, uma enfermaria de prisioneiros 4×4 e espaço para a quarta cela. Paredes compartilhadas, portas próprias e piso de madeira. Acesso externo em concreto.
- Camas são marcadas para prisioneiros somente após fechamento do cômodo e verificação de que não há camas de colonos nele. A enfermaria tem cama comum marcada como médica; não converte camas do hospital dos colonos.
- Mesa, cadeira e luminária de parede vêm no acabamento, sem atrasar a disponibilidade da cama. Aquecedor a 21 °C quando o planejamento encontra frio; resfriador passivo no calor. Células acima de 35 °C ou abaixo de 0 °C não são utilizadas para novas capturas.
- Cabos normais dentro das paredes e ocultos no restante do percurso até rede existente ou planejada. Energia e pesquisa precisam estar disponíveis; o módulo não cria energia artificialmente. Arvores e rochas no local recebem trabalhos normais de limpeza/mineração.
- No layout modular, prepara um quarto extra quando há reserva de comida suficiente e a população está abaixo da meta. Novos módulos e plantações preservam a reserva da prisão.

## Captura e cuidados

Reavaliação a cada 60 ticks, uma nova tarefa a cada 120. Captura limitada a humanoides hostis, vivos, caídos e elegíveis pelo jogo, com cama válida, rota segura, médico apto e pelo menos dois dias de comida. Limite de até quatro prisioneiros, incluindo capturas pendentes e conforme camas disponíveis.

Capturas aguardam o fim das ameaças, incêndios e recuperação global. Os resgates e o sangramento não tratado de colonos próprios têm precedência. O módulo não destaca combatentes para capturar durante a batalha. Se a estimativa de transporte e atendimento excede o tempo de vida por sangramento, tenta tratamento nativo no chão antes de capturar. Não garante sobrevivência quando tempo, habilidade ou acesso são insuficientes.

Depois da captura: atendimento médico e alimentação por trabalhos nativos. Respeita proibição manual de tratamento. Prisioneiros entram no consumo diário de comida, produção, plantações, reserva de medicina e demanda de Doctor/Warden. Caravanas não saem deixando pacientes presos sem atendimento ou a colônia sem carcereiro apto. Outros gerenciadores não usam os auxiliares reservados pela prisão.

## Destino e checkpoints

1. **Tratar:** manter até atendimento e recuperação; necessidades urgentes precedem interações sociais.
2. **Avaliar:** pontuação de skills, paixões, profissões fracas na colônia, alguns traços, idade e perdas permanentes de membros. Para recrutar exige cama de colono livre, três dias de comida, elegibilidade nativa e vaga na meta populacional.
3. **Converter:** com Ideology e ideologia alvo, usar conversão nativa por carcereiro da ideologia principal. Sem alvo aplicável ou já convertido, pular a etapa.
4. **Recrutar:** somente depois da conversão real; interações nativas reduzem resistência e tentam recrutamento. Cinco dias sem progresso geram aviso na HUD. Não altera resistência, ideologia ou facção diretamente.
5. **Liberar:** candidatos não escolhidos ou com lealdade inabalável recebem tratamento antes da liberação nativa, com rota segura. Facções permanentemente hostis não recebem promessa de ganho diplomático. Registra saída e variação observada da relação, sem atribuir automaticamente outros eventos ao módulo.
6. **Integrar:** ao ingresso real, o scanner geral atualiza quartos, alimentação, trabalho, agenda e equipamento. Metas militares continuam no sistema de progressão existente.

Fugas nativas de prisão entram explicitamente no detector de ameaças. O módulo suspende seus trabalhos em perigo; defesa usa o combate existente, sem garantia de recaptura ou combate não letal.

## Controle manual e persistência

Prisioneiros que já estavam no save permanecem manuais. Alterações do jogador em interação ou ideologia de conversão cedem o controle. Draft e novas ordens forçadas preservam o auxiliar manual. Ao desligar, encerra somente ordens próprias, restaura interações que ainda lhe pertencem e interrompe obras pendentes da prisão; mantém estruturas concluídas e capturas já realizadas. Estado, destinos, ordens e histórico são salvos.

## Validação por etapas

O ensaio nativo `prison-20261008-210807-417` passou em captura de dois inimigos, tratamento, conversão com Ideology, recrutamento real, liberação, persistência e desligamento. A fixture prepara camas e candidatos e reduz resistência/certainty inicialmente para acelerar as interações; isso não descreve uma economia natural nem altera o módulo de produção. Corrigido também o cache nativo da célula de prisão ao configurar camas depois da construção.

O ensaio integrado atingiu três vagas construídas naturalmente e registradas no checkpoint do 13º dia. Ainda faltam cenários específicos de segurança, fuga, intervenção manual, tratamento sem medicina e salvar/carregar durante transporte; liberação não comprova ganho diplomático contra facção permanentemente hostil. O primeiro planejamento térmico é conservador; adaptação sazonal prolongada permanece pendente. Layouts antigos que não sejam modulares precisam já ter cama de colono sobrando para o recrutamento automático. Evidências e próximos passos em `VALIDACAO-ETAPAS.md`.
