# Dois estoques/oficinas — revisão V4 (11/10/2026)

Dois módulos completos de 13×13 (interior 11×11) combinam armazenamento e produção. O segundo é alocado junto ao primeiro, preferindo a mesma linha horizontal quando o terreno permite. Permanecem os corredores de três células entre módulos. Solo instável é recusado; obstáculos naturais seguem a preparação normal.

| Estoque | Bancadas junto às paredes |
|---|---|
| A | Ferraria, costura, laboratório de drogas, fabricação de componentes |
| B | Corte de pedra, usinagem, escultura, fundição, refinador de combustível, cervejaria |

Ferraria e costura começam com as variantes simples quando necessário e evoluem com pesquisa. As demais bancadas respeitam desbloqueios. Prateleiras grandes são instaladas após a pesquisa necessária; cada célula oferece três posições de pilhas. Ambos aceitam materiais e equipamentos, mantendo comida no freezer. O cálculo de capacidade soma os dois estoques e as células reais das prateleiras, sem contar novamente o piso sob elas.

São reservados os footprints nativos e pontos de interação das bancadas futuras, além de corredores centrais horizontal e vertical. As prateleiras não podem bloquear esses espaços. Não são criadas oficinas adicionais para os novos módulos combinados. Projetos de serviço de oficina/fabricação identificam os dois usos sem construir outra sala.

Compatibilidade: estoques antigos sem este layout são preservados; a alteração não demole uma base existente automaticamente. O save de revisão V4 será uma cópia administrativa nova, preservando V3 e o cenário original.

Validação concluída: PASS em `.tools/validation/storage-v4-release-20261011-180424-940/manifest.json`, com `[AllModules] DONE`. Dois módulos adjacentes na mesma linha; 12 prateleiras no A e 8 no B, total de 120 posições de pilhas nas prateleiras. Todas as bancadas acessíveis junto às paredes; 56 consumidores energizados. Filtros dos dois estoques, ausência de zona sobre prateleiras e geometria revalidados após save/load. Freezer −1,6°C antes e −2,0°C após recarga. Nenhum aviso de zona incompatível/sobreposição de slot group na rodada final.

As execuções anteriores encontraram/corrigiram profundidade da fabricação, interação da escultura bloqueada pelo refinador, sobreposição da zona do chão com prateleiras e orientação de portas laterais. As duas bibliotecas compilaram; produção sem avisos/erros, testes apenas NU1900 (consulta NuGet indisponível).

Save entregue na pasta do jogo: **AutonomousRim-Modulos-Estoques-11-10-V4**. SHA256 `603B226B51FBA5239E826C1FC719B0A7B1DF2E55F565AA9DA2DE8B8B9BFAC385`. IDs dos seis colonos originais preservados; componentes e dependência RuntimeChecks removidos. Automação desligada para revisão, pesquisas completas e construção administrativa. Este teste não comprova construção natural desde o começo. DLL instalada/testada: `236685FE2C126FD6006529C299FA289993E2725B16DA4D079D637320E9726566`.

V3 e original desenhado pelo usuário preservados. O add-on de testes foi removido e apenas a sessão isolada foi encerrada.
