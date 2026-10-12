# Revisão da execução modular — 11/10/2026

Pedido: corrigir causas nas runs antes de acrescentar sistemas; manter a grade 13×13.

## Causas encontradas e alterações

| Problema | Causa encontrada | Correção |
|---|---|---|
| Refeitório ausente | Criação dependia de todos os serviços modulares concluídos | Refeitório/recreação 13×13 no plano inicial, mesa grande, cadeiras adjacentes e xadrez; TV/sinuca evoluem depois sem impedir a alimentação |
| Energia | Dimensionamento não expressava as duas subdivisões solicitadas | Novos projetos usam metade do módulo: interior 11×5, gerador e banco de baterias com passagem |
| Agricultura irregular/tardia | Campo inicial esperava quartos; planejador complementar recolhia células dispersas | Cinco campos iniciais completos de 169 células, alinhados à grade; expansões comerciais também usam módulos inteiros |
| Agricultor pouco produtivo | Demanda não contava vegetação espontânea; despacho de construção podia aproveitar os intervalos entre trabalhos agrícolas | Demanda inclui substituição da vegetação; despacho respeita agricultura de prioridade superior com trabalho acessível |
| Bancadas não aparecem | Atualização tecnológica só atendia ao layout antigo em anel; interiores eram montados uma única vez | Reconciliação modular recorrente, bancadas desbloqueadas, reserva de interação/passagem e oficina adicional quando não houver espaço |
| Pesquisa sem destino | Faltava integração modular com as instalações e explicação do uso posterior | Reavaliação de instalações e necessidades; rota inclui destino de mobiliário, produção, medicina e comércio, mantendo os checkpoints militares |
| Luz esquecida | Luz estava em projeto de acabamento posterior ao término do cômodo | Luminária faz parte do projeto essencial; projetos antigos transferem a tarefa existente sem substituir objetos |
| Corredores sem piso | Dependência global de todos os módulos, continuamente ampliada | Dependência somente das paredes do módulo correspondente; piso acompanha os cômodos, sem pavimentar automaticamente todo o contorno das plantações |
| Piso diferente do pedido | Concrete custa 1 aço e tem beleza negativa | PavedTile: lajota de concreto nativa, 2 aço/célula. Migração apenas das tarefas ainda não emitidas |
| Módulo aparentemente pronto | Estado de obra não explicava energia, instalações e plantio | Reconciliação reabre tarefas faltantes; HUD mostra estrutura, piso, cobertura, instalações, energia e células plantadas separadamente |
| Allow lento | Ciclo de 120 ticks | 60 ticks, até 16 pilhas por ciclo, mantendo necessidade, segurança e respeito a reproibições |
| Entulho | Layout modular não criava depósito inicial | Zona de despejo para pedaços de pedra e cadáveres de animais, próxima ao setor de abate sem ocupar módulos/corredores |
| Controle geral | HUD só possuía desligamento geral | Botão Ligar tudo/Desligar tudo para as 11 automações, com controles individuais preservados |

## Compatibilidade e limites

- Construções prontas, obras emitidas e cancelamentos manuais são preservados; não há demolição automática para redimensionar instalações antigas.
- No jogo vanilla, os campos têxtil e de smokeleaf usam `Plant_Cotton` e `Plant_Smokeleaf`. Não são apresentados como linho/hemp inexistentes na instalação.
- Cada campo aguarda uma área inteira fértil, descoberta e acessível; não cria um recorte irregular quando faltam células. Árvores são removidas pelo trabalho agrícola nativo. Pedra sem solo fértil não é tratada como área cultivável.
- Reservar bancada não termina pesquisa nem concede materiais. Bills e equipamento continuam nos gestores de produção, comida, defesa e comércio, sujeitos a recursos, habilidades e trabalho nativo.
- Energia e condições de trabalho são reportadas separadamente da conclusão física da obra.

## Verificação

- Compilação do mod e do complemento de testes.
- 57 verificações de políticas aprovadas antes da última correção agrícola; repetir ao encerrar.
- Testes isolados carregam a BASE EliteSix de hash `94B605291A81B328FC8C518B3962167222071638F5A9D090060B5473522062AF`, mantendo os seis perfis e fatores do cenário.
- O teste de transição tecnológica conclui pesquisas apenas na cópia descartável para verificar desbloqueios. Não é prova de progressão científica natural nem altera a campanha de 20 dias.
- `modular-execution-revision-20261011-042817-077`: detectou que Concrete não é a lajota de 2 aço; corrigido para PavedTile.
- `modular-execution-paved-20261011-043104-917`: passaram planejamento inicial, zonas nativas, luzes, piso, desbloqueio de bancadas e idempotência. A execução revelou apenas 2 células plantadas contra 158 paredes/portas; não aprovado como teste agrícola.
- `modular-farmer-priority-20261011-043617-772`: **PASS + DONE**. Cinco zonas nativas de 169 células, iluminação essencial planejada, corredor com dependência local, lajota de 2 aço, desbloqueios e idempotência aprovados. Trabalho nativo: 13 células plantadas e 75 paredes/portas concluídas; seis personagens originais preservados. Save: `Saves/ModularExecutionEvidence.rws` dentro do perfil. Os 57 testes de políticas passaram novamente.
- Próxima verificação preparada: `NativeWorkshopChecks`, observando pesquisa nativa → bancada nova construída → Bill concluída ligada a uma tecnologia recém-pesquisada. Usará uma cópia de `BeforeEliteMechanicStages.rws`, sem conceder pesquisas, recursos ou estruturas. **Não executada**: a revisão automática de aprovação falhou por limite de uso antes de iniciar o comando. O último ajuste de vínculo entre receita/bancada e pesquisa ainda precisa recompilar.
- A DLL de produção das correções modulares foi instalada e verificada no jogo antes do teste aprovado. As alterações desta revisão permanecem no diretório de trabalho; não foram commitadas nem enviadas ao Git nesta etapa.

## Nova orientação do usuário

O usuário criará manualmente um save de referência contendo todos os tipos de módulos 13×13 completos. Antes de definir novos layouts, analisar esse save quando ele for informado. Usar as estruturas como referência e derivar versões progressivas de cada módulo: mínimo funcional inicial, melhorias intermediárias e versão completa conforme as pesquisas. A presença de mobiliário avançado no save de referência não autoriza construí-lo sem pesquisa na gameplay. Preservar paredes compartilhadas, acessos, circulação e posições de expansão durante as atualizações.

A campanha natural e os demais testes de combate/resgate permanecem nos seus checkpoints anteriores; este relatório não os declara concluídos.
