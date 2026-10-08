# Resgate e tratamento durante combate

A emergência automática agora pode destacar um combatente controlado pela IA para atender um aliado incapacitado, sem recrutar novamente esse cuidador durante o atendimento. Usa os trabalhos nativos `Rescue` e `TendPatient`: transporte, reservas, tratamento e consumo de medicina continuam ocorrendo no jogo.

## Decisão

- Ordena os pacientes pelo prazo nativo até a morte por sangramento, depois pela intensidade do sangramento.
- Considera médicos capazes de andar/manipular, sem impedimento de trabalho médico, com saúde e descanso suficientes. Ordens e recrutamentos manuais são preservados.
- Avalia habilidade médica, contribuição ao combate, tempo de deslocamento e urgência. Favorece bons médicos cuja saída custe menos à defesa; proximidade pesa mais em casos urgentes.
- Com inimigos ativos próximos, exige que os defensores restantes sejam pelo menos tão numerosos quanto eles e tenham força estimada de pelo menos 110% da força inimiga. Combatentes já em retirada não contam. Estruturas hostis impedem esse destaque enquanto também há atacantes próximos.
- Verifica paciente, caminho até ele e caminho até uma cama válida. Fogo, contato inimigo e exposição perigosa impedem a ordem. Um inimigo já pressionado por um melee aliado pode ser considerado contido, mantendo distância mínima de quatro células dele.
- Se transporte mais tratamento consumir a margem de segurança até a morte, estanca o sangramento no local primeiro. Sem cama, também pode estabilizar no chão. Tratamento urgente dispensa buscar medicina para evitar atrasos; isso usa a qualidade nativa de tratamento sem remédio.
- O cuidador fica reservado até terminar o trabalho nativo. Ao chegar à cama, continua com tratamento quando necessário. Depois é liberado automaticamente, e a situação é reavaliada, inclusive a possibilidade de transportar alguém já estabilizado.

O scanner reavalia a emergência a cada 15 ticks. Não é uma promessa de resgate incondicional: se destacar alguém tornar a defesa insuficiente, o caminho estiver exposto ou não houver tempo para alcançar e tratar, o atendimento é adiado. O painel contabiliza esses casos.

## Recuperação da colônia

Vencer com feridos é compatível com esse comportamento. Depois de confirmar segurança, médicos priorizam atendimento e colonos exaustos/feridos recuperam suas necessidades. Colonos saudáveis recuperam suas prioridades anteriores, incluindo construção e pesquisa; um paciente não suspende toda a expansão da base. As automações secundárias seguem as regras existentes de emergência e recuperação.

## Validação em 08/10/2026

Comandos, após compilar produção e complemento de testes:

```powershell
.\scripts\CombatTest.ps1 -MedicalRescueChecks
.\scripts\CombatTest.ps1 -EmergencyChecks
.\scripts\CombatTest.ps1
```

O teste médico usa perfil privado, dificuldade Peaceful e velocidade 3x. Prepara cinco colonos com habilidades 20, reduz a medicina dos não médicos a 1 para conferir seleção e equipa participantes com armadura marine e capacete de aço. Prepara seis inimigos, quatro incapacitados desde o início, deixando dois atacantes ativos. Lesões e cenário são inseridos na preparação; depois disso não há cura artificial nem ajuste de saúde para concluir atendimento. É um teste controlado de mecânicas, não uma raid completa nem medição de taxa de vitória.

Execução final `medical-rescue-tests/20261008-155846/Player-20261008-155847.log`:

| Verificação | Resultado |
| --- | --- |
| Separar o médico de quatro combatentes móveis | Médico com medicina 20 destacado; três defensores permanecem contra dois inimigos |
| Hospital disponível | Resgate nativo até cama seguido de tratamento; paciente vivo e sangramento estancado |
| Sem cama, prazo inicial de 1.349 ticks até a morte | Tratamento nativo no chão estanca o sangramento antes do prazo |
| Fim do atendimento em ambos os casos | Cuidador liberado automaticamente; voltou à resposta de combate quando ainda havia ameaça |
| Segurança e controle manual | Célula em contato com inimigo rejeitada; recrutamento/ordem do jogador preservados |

Regressão `emergency-tests/Player-20261008-155422.log`: classificação de ameaças, resgate, rejeição de rota, recuperação individual, nova ameaça, manutenção da recuperação por lesão e restauração com edição manual passaram.

Regressão `combat-tests/Player-20261008-155456.log`: interceptação e cobertura neutralizaram as ameaças; retirada foi mantida pelo intervalo de 1.800 ticks, com inimigos ainda ativos. Nenhum participante morreu ou ficou incapacitado nesses três cenários. Há ferimentos, como esperado em combate. Os 57 checks de políticas também passaram; compilação de produção e complemento sem avisos/erros.

## Limites atuais

A força é uma estimativa, não uma previsão exata de vitória. Não escolhe uma cama alternativa dinamicamente se o hospital for invadido quando o paciente já está sendo carregado. Salvar/carregar um atendimento em andamento, resgates sob fogo intenso e interações com habilidades especiais/mods médicos ainda precisam de testes próprios. Esta revisão não comprova sobrevivência na maioria das raids.
