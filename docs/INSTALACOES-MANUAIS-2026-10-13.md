# Reconhecimento de instalações manuais

A construção passa a usar evidência do mapa, além das coordenadas dos modelos. O objetivo é aproveitar instalações do jogador, sem reconstruir salas apenas porque estão em outra posição ou possuem outro formato.

## Serviços reconhecidos

| Serviço | Evidência mínima |
| --- | --- |
| Quartos/dormitórios | Camas humanas de colonos em recintos válidos; abrigo suficiente para a população atual |
| Cozinha | Bancada acessível em recinto fechado, com receita de refeição humana |
| Abate | Bancada acessível com receita que produz o resultado especial nativo de abate |
| Pesquisa | Bancada de pesquisa acessível em recinto fechado |
| Hospital | Cama humana médica para colonos, em recinto fechado |
| Energia | Geração conectada à rede; bancos de baterias internos conectados a essa geração também são identificados |
| Recreação/refeitório | Instalação de recreação e mesa com assento adjacente; a combinação satisfaz o serviço conjunto |
| Estoque | Ao menos quatro células internas de armazenamento que aceitam madeira e aço |
| Freezer | Ao menos quatro células que aceitam comida, no lado frio de um cooler ajustado para zero ou menos |
| Medicamentos | Ao menos duas células que aceitam medicina dentro do hospital |
| Oficina/fabricação | Bancadas e receitas reais de roupas, armas, blocos ou fabricação |

Camas de prisioneiros/escravos não satisfazem o abrigo ou hospital dos colonos. Área proibida, acesso bloqueado, fogo, exposição ao exterior e filtros incompatíveis impedem o reconhecimento correspondente. O catálogo usa classes e receitas nativas, incluindo bancadas de mods que as utilizam. Refrigeração e alguns equipamentos avançados ainda usam defs conhecidos; não há garantia para estruturas customizadas que implementem outra API.

Reconhecer instalação não significa garantir que ela esteja operando neste instante. Energia desligada, combustível, ingredientes, trabalhadores, capacidade e temperatura continuam sujeitos aos diagnósticos existentes. A HUD mostra a temperatura atual do freezer. Um terreno naturalmente frio sem cooler não satisfaz esse critério, evitando tratar um estoque que descongelará como freezer permanente. Uma sala vazia não revela sua finalidade sem móveis ou armazenamento.

## Aproveitamento e proteção do trabalho manual

Projetos substituídos são vinculados à instalação existente. Seus modelos ficam fora da fila de trabalho, orçamento e contagem física da HUD. Apenas blueprints não construídos, pertencentes à IA e não compartilhados com outro projeto ativo, são retirados. Frames com material investido, móveis prontos, ordens manuais, filtros, prioridades e termostatos são preservados. Frames preservados ainda podem ser concluídos pelo trabalho nativo.

As referências são revalidadas após load e periodicamente. Se a instalação desaparecer ou ficar inacessível, deixa de satisfazer a meta. Projetos anteriores mantêm seu modelo para recuperação; registros virtuais são removidos. Complementos são suspensos quando perdem a instalação de origem.

O planejamento acrescenta itens faltantes dentro das células reais do recinto, incluindo salas irregulares: capacidade médica mínima de uma cama por quatro colonos, medicina, prateleiras pesquisadas, baterias, bancadas industriais e evolução da pesquisa. Após desbloqueios, pode acrescentar cama hospitalar/monitor e pesquisa avançada/multianalisador, quando houver espaço. Móveis manuais não são demolidos para realizar essas melhorias. Portanto, uma instalação avançada pode precisar de espaço adicional. Este pacote não promete expansão universal de qualquer sala cheia.

Portas, footprints e células de trabalho existentes ou planejadas são protegidos. A colocação continua usando validação nativa; não constrói automaticamente através de paredes para caber no retângulo que engloba uma sala irregular.

## Validação e custo

Os testes usam perfis isolados. A fixture de instalações cria salas, móveis e materiais para verificar reconhecimento, propriedade, formas irregulares, colocação de blueprint e save/load; não é uma run de sobrevivência sem cheats. A regressão industrial mantém o planejamento geral congelado para testar fabricação/prateleiras separadamente, com construção, transporte e produção nativos. A expansão é verificada em outra cópia do L1 durante 12 horas, sem spawn de recursos ou conclusão instantânea.

O inventário é compartilhado por mapa e atualizado a cada 300 ticks. Consultas de acesso são feitas apenas para equipamentos relevantes, sem testar caminho até cada parede. O benchmark instrumenta `ExistingFacilities.Refresh`, `Reconcile` e `ExistingFacilityImprovements.Plan`, além dos módulos existentes. Tempos inclusivos não devem ser somados. Amostras de 2.000 ticks e TPS variável não constituem garantia de FPS ou ganho de desempenho.

Na DLL final, passaram 33 checks de instalações manuais, incluindo rotação diferente do modelo antigo e colocação industrial em sala irregular. O perfil final do L1 mediu 250,8 TPS. `Reconcile` custou 17,0 ms em 19 chamadas (média 0,90 ms, P95 7,08 ms), incluindo o inventário; a média inclui retornos pelo cache. Isso não demonstra aumento de TPS em relação ao pacote anterior, cuja amostra mediu 277,2 TPS.

As regressões de transporte/construção e indústria passaram. A cópia do L1 progrediu durante 12 horas: freezer 24 → 30 de 176 tarefas, energia 0 → 55 de 93, oito colonos vivos e camas originais preservadas. As duas salas ainda estavam ativas. Essas regressões usaram a revisão `03EC95...`; os dois ajustes finais de rotação/proteção de células irregulares foram verificados nos 33 checks e perfil da DLL final `C495642E704E98C6E2A45A379EF0F8BCD28B9E16AA0D0F172517DC02ABD9A438`. Os hashes completos de cada execução ficam nos manifestos.

Os manifestos, hashes e linhas de resultado estão em `outputs/ManualFacilities/results-2026-10-13.json`. Logs completos ficam em `.tools/validation/`. Os saves originais não são sobrescritos. As cinco referências de genes ausentes já existentes no L1 continuam sendo informadas pelo jogo; este pacote não migra esses genes.
