# Aprendizado pareado e desempenho por módulo

## Escopo implementado

A memória descritiva anterior permanece separada dos ensaios controlados. O primeiro ajuste comparável é `observation-v1` versus `food-buffer-v1`, que acrescenta um dia à reserva alimentar sazonal. Não existe geração de código, mudança arbitrária de estratégias ou treinamento de um modelo durante a partida.

Cada resultado identifica SHA-256 do save inicial, identidade persistente da colônia, contexto de conteúdo/dificuldade/bioma, configuração das automações/comércio/paralelismo, repetição e duração. A coleta nativa acompanha pelo menos 60.000 ticks consecutivos, menor reserva alimentar observada a cada 60 ticks, ticks com fome/caídos, mortes do elenco inicial, lotes de produção concluídos e incrementos observados de pesquisa. Incrementos de pesquisa são somados enquanto o projeto permanece igual entre amostras; não são uma reconstrução exata do último trabalho antes de trocar projeto.

Um par exige os dois braços do mesmo baseline/configuração/repetição/duração. São necessárias três identidades de colônia independentes, com pelo menos duas repetições pareadas por colônia. Copiar o mesmo save não cria outra identidade independente. Janelas antigas, ensaios incompletos, duplicados, contexto não verificável e valores não finitos não contam. Mortes e fome são resultados adversos válidos, não são descartados para melhorar o relatório.

A reserva ampliada só é recomendada quando cada colônia replicada apresenta ganho médio de pelo menos 0,25 dia na menor reserva ou redução de fome. Qualquer par elegível com mais mortes, fome, tempo caído, perda alimentar acima de 0,05 dia ou queda relevante de pesquisa/produção veta a promoção. A tolerância de pesquisa/produção é 10%, com uma unidade de tolerância absoluta. São critérios conservadores de validação, não um teste estatístico universal nem prova de vitória em campanha.

## Controles na aba Diagnóstico

Além do contexto/configuração, a recomendação exige população inicial e estação compatíveis (`Scenario`). Esse dado é congelado no começo do ensaio: uma morte continua sendo um resultado adverso, sem alterar retroativamente o grupo experimental. Arquivos intermediários anteriores a esse campo não são elegíveis para promoção.

- Criar um checkpoint A/B próprio, com nome único. Não sobrescreve saves pessoais.
- Escolher repetição 1–8 e recarregar o mesmo checkpoint para testar a política padrão ou a reserva ampliada, por um dia a 3×. Recarregar troca a partida ativa: salvar o progresso atual antes de iniciar outro braço.
- Ver andamento, interrupção e conclusão. Mudança de política/configuração, perda de continuidade temporal ou movimentação/adição de colonos interrompe o ensaio. Load não inventa tempo experimental anterior. Intervenções de outros mods e todas as alterações manuais do mundo não são universalmente detectáveis: executar os braços sem intervenções externas.
- Aplicar uma recomendação somente quando o contexto/configuração atual passa a comparação. Aplicação automática ao carregar é opcional e começa desligada. Restauração da política anterior continua disponível.
- Importar `AutonomousRimPolicyTrials.xml` da pasta de configurações: XML limitado a 2 MB/128 registros, DTD proibida, schema versionado e deduplicação. Dados de contextos diferentes permanecem no arquivo, mas não recomendam políticas para a partida atual.

O fluxo da HUD usa APIs nativas e funciona sem RuntimeChecks. Como o contexto inclui os mods/DLLs ativos, resultados com o complemento de testes não validam automaticamente uma partida que não o tem. Mudanças de versão/conteúdo exigem nova validação compatível.

## Medição de desempenho

Instrumentação opcional, inicialmente desligada, em ticks do mod, ameaça/combate, leitura do estado/salas, construção, estratégia, falhas, diagnóstico, prioridades, agenda, equipamento, comida, comércio/caravana e prisão. Registra chamadas, falhas, tempo total, média, p95 e máximo. P95 usa uma fila limitada às últimas 512 chamadas de cada método; média/máximo abrangem a janela inteira. Tempos são inclusivos: não somar pais e filhos.

TPS da HUD mede ticks do jogo por tempo real neste mapa, sem confundir velocidade selecionada com velocidade atingida. Pausas, carregamento e mudanças de velocidade reiniciam a amostragem; lacunas longas de frames não entram na janela. TPS inclui o restante do RimWorld. O cronômetro de um módulo mede tempo decorrido, incluindo possíveis pausas do sistema/GC; não é uma medição de ciclos exclusivos de CPU. A instrumentação tem custo próprio.

A thread auxiliar mantém fila limitada, contadores de tarefas concluídas/recusadas/falhas e tempo acumulado. Os cálculos continuam recebendo apenas cópias de valores. Não foi ampliado o paralelismo nem fixado um núcleo do processador. Um microbenchmark de pontuação de alvos verifica igualdade síncrona/assíncrona, separando custo síncrono e latência de submissão; não comprova ganho de TPS em combate.

A instrumentação revelou uma inicialização prematura de `ColonyStateScanner.TrackedResources` durante a preparação dos patches Harmony. A lista agora resolve DefOf durante a leitura do mapa, depois do carregamento das definições.

O botão de comparação síncrona/auxiliar usa somente o worker já existente, limpa os caches táticos dos mapas e reinicia a medição. Alterá-lo durante um ensaio muda a configuração e invalida sua continuidade. O modo é da sessão atual, não uma configuração permanente de afinidade da CPU.

O início do replay da HUD é ligado à finalização do novo mapa e ao seu primeiro tick. `LoadGame` apenas enfileira seu evento; `ExecuteWhenFinished` pode executar imediatamente antes dele, ainda no jogo antigo. Não usar esse callback para registrar um ensaio do novo mapa.

## Execução reproduzível

```powershell
.\scripts\ComparePolicies.ps1 -BaselineSaves 'C:\caminho\colonia-a.rws','C:\caminho\colonia-b.rws','C:\caminho\colonia-c.rws' -Replicas 2 -CompareParallel
```

Usar baselines das seis personagens originais Enhanced World no runner RuntimeChecks. Cada braço recebe uma cópia isolada do save, mesma versão de DLL e um dia de jogo. O runner alterna a ordem das políticas entre repetições, preserva hashes dos saves pessoais e exporta manifests, logs, resultados XML e CSV de desempenho. O braço síncrono usa a política padrão; não entra como par de aprendizado do braço com configuração paralela diferente. O runner é retomável pelos artefatos preservados; arquivos de comparação podem ser importados/deduplicados, mas uma etapa incompleta não conta como resultado concluído.

Testes sintéticos positivos/negativos exercitam a aprovação e os vetos em arquivos locais de teste; nunca entram no histórico global de estratégias da colônia.

## Resultados

Sete execuções nativas de um dia foram concluídas em cópias do V3, sempre com os seis originais, sem fome, mortes ou caídos. São execuções separadas a partir do mesmo ponto inicial, não uma campanha contínua de sete dias nem sete colônias independentes.

O par final instalado, em `outputs/Learning/20261013-040235`, usa a DLL SHA-256 `3268012528ED2D60944D1EB4CB352676228CDE5FAE1EBAB27DF1062DAB46286F` e runtime `3EC5897B8FBDDFBD48E8ED6781C4642E80A7CA6AD67FB6AB330CACB5EBD58F8F`. Ambos registram `colonos=6|estacao=PermanentSummer`, horizonte observado de 60.000 ticks, origem V3 idêntica e save/load/exportação XML aprovados.

| Braço final | Menor reserva | Lotes concluídos | TPS observado | Fome / mortes |
|---|---:|---:|---:|---|
| Padrão | 19,292 dias | 32 | 320,1 | 0 / 0 |
| Reserva +1 dia | 19,340 dias | 36 | 224,1 | 0 / 0 |

A diferença de reserva neste par é pequena e insuficiente para promover a política, além de faltar replicação entre três colônias independentes. As duas execuções não avançaram pesquisa: seu veto foi verificado pelos contratos sintéticos, não por pesquisa real nesta rodada. A diferença de TPS entre políticas não estabelece causalidade: jobs e eventos nativos podem divergir e não foi feito um estudo estatístico de desempenho.

A primeira matriz intermediária (`20261013-033002`) mediu 326,1 TPS com política padrão/paralelismo ligado e 329,8 TPS no modo síncrono. Apenas a tarefa do microbenchmark foi executada pelo worker; não houve benchmark de combate ativo. Isso não justifica ampliar o paralelismo. No controle dessa matriz, a leitura de ameaças consumiu 37,662 s inclusivos no dia (4.000 chamadas, média 9,415 ms); comida teve média de 69,897 ms por chamada, e prioridades de trabalho 20,875 ms. Essas medições indicam onde investigar custo, sem prometer aceleração já implementada.

As matrizes intermediárias são preservadas para auditoria, mas seus registros sem `Scenario` não promovem políticas. O índice consolidado está em `outputs/Learning/results-2026-10-13.json`, com hashes, resultados e limitações. Os saves V3/V4 permanecem com os hashes originais registrados nesse arquivo.

Falhas de preparação foram preservadas: inicialização antecipada de DefOf nas três primeiras tentativas; primeiro fluxo HUD com callback aplicado ao jogo antigo; tentativa com requisição de tick que ainda usava esse callback. O controlador passou após vincular a requisição à finalização do mapa novo. Nenhuma dessas tentativas é contabilizada como validação concluída.

PASS final `learning-hud-installed-final-20261013-041338-097`: oito verificações nativas do controlador da HUD na mesma DLL instalada. Inclui checkpoint separado, replay no mapa novo, configuração/contexto, bloqueio de aplicação sem evidência, interrupção manual, save/load e rejeição de arquivo alterado. Manifest/log copiados para `outputs/Learning/workflow-installed-final`. Add-on temporário removido, sem processo de jogo restante e DLL instalada igual à compilada. Produção compilou sem avisos/erros; runtime compilou sem erros, com NU1900 da consulta de vulnerabilidades indisponível.

Resultados e limitações das rodadas nativas são registrados junto dos artefatos em `outputs/Learning`. A aprovação funcional do mecanismo não implica aprovação da reserva ampliada em outras colônias. A HUD compila e seus controladores recebem testes nativos; aparência visual não é comprovada por batchmode.
