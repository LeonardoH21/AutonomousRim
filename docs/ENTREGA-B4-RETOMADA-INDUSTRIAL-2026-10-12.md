# Entrega B4 — retomada de produção e segurança industrial

Solicitação do próximo pacote recebida durante a validação B3. Escopo escolhido a partir dos próprios logs: continuidade de peças inacabadas e limpeza das demandas industriais durante bloqueios de sobrevivência.

## Mudanças

- A busca de viabilidade considera primeiro a peça inacabada vinculada e seu criador, sem exigir um novo conjunto de ingredientes. Mantém exclusividade do criador, proibição, presença no mapa, reserva e acesso. Não transfere ownership nem usa o getter `BoundWorker`, que pode desfazer o vínculo no código nativo.
- Peças não vinculadas são procuradas pelo método nativo `ClosestUnfinishedThingForBill`; nenhum job é emitido por esse diagnóstico. A fabricação e retomada continuam pelos jobs normais do jogo.
- Uma retomada reserva zero material adicional. Isso evita proteger um segundo lote e permite à coordenação continuar considerando o fabricante original quando ele estiver apto, preservando saúde, necessidades e profissões manuais.
- O diagnóstico de uma Bill vinculada analisa seu criador antes da amostragem limitada de três candidatos. Um criador no fim da lista não deve ser classificado como falta de ingredientes por ter ficado fora da amostra.
- Ameaças imediatas também bloqueiam o plano industrial quando o botão global de emergência estiver desligado. O botão não torna uma produção segura diante de ameaça real.
- O caminho antecipado de emergência agora limpa a previsão industrial e suspende Bills próprias intactas no ciclo industrial. Faltas antigas deixam de alimentar novas demandas comerciais desse plano. A atualização normal retoma as Bills intactas após segurança e reserva de comida; edições manuais permanecem fora do controle.

## Validação

Compilação de produção sem avisos/erros; runtime sem erros, com aviso NU1900 da auditoria NuGet indisponível. Contratos nativos de retomada/proibição/ownership, reserva baixa de comida, ameaça imediata e caminho global de emergência passaram, assim como os ensaios abaixo.

PASS `industry-resume-powered-contracts-20261012-025554-813`: 20 verificações, incluindo pausa/retomada por comida, ameaça com botão global desligado, caminho antecipado de emergência, viabilidade de peça inacabada somente para seu criador, proibição, vínculo preservado pela análise e reserva zero para retomada.

FAIL preservado `industry-resume-contracts-20261012-025258-655`: avaliação de coordenação antes da rede elétrica nativa inicializar. A fixture passou a aguardar até 600 ticks pela energia real; proibição/ownership e os bloqueios de sobrevivência já tinham passado na execução anterior. Nenhuma aprovação foi baseada em ignorar a condição de energia.

- PASS `industry-resume-native-20261012-025631-698`: pausa natural de SMG observada; retomada viável sem lote novo, produção concluída e arma equipada em Charlie. Dez componentes fabricados, base desligada, ownership após save/load e preservação de edição manual.
- PASS `industry-resume-fuel-20261012-030229-684`: madeira coletada, smithy abastecido e espada fabricada/equipada por jobs nativos; save/load preservado.
- PASS `industry-resume-parallel-20261012-030416-359`: três peças, coletes ranged 0 → 3, pesquisa +160,3164, banquinho concluído; 40.138 ticks.

Esses ensaios usam DLL `557F225F7C8CC81DB8A6B19F546F402BFFFA997967A231D728A4716CA9942870` e runtime `BA215024890AAD1DEBA874F59337DC67F84FF5B9E07E51942D5E6EA2D742B9F6`. O último ajuste altera o diagnóstico: seleção do criador e reutilização da mesma busca de viabilidade da coordenação. Não altera os jobs/executores nativos. Não atribuir os ensaios anteriores a outro hash.

PASS final `industry-creator-shared-diagnostic-20261012-031312-356`: 21 contratos, incluindo Charlie fora dos três primeiros trabalhadores recebendo diagnóstico `ready` de retomada, sem ingredientes novos. DLL instalada `AF6AFC7B6E2730C327E1F10EDEAF22288BC08932F369D1C0C907062CDB18A06E`, runtime `9495CAEF3FEC47D10F1ED70DA589FC6C6D4C469DDADD4EBFC8C4E38AB49A7DF7`.

Falhas preservadas `industry-final-creator-diagnostic-20261012-030901-942` e `industry-creator-state-diagnostic-20261012-031105-113`: restringir a amostra ao criador ainda não bastava, pois `Inspect` tinha uma chamada direta ao alocador de ingredientes crus. O caminho foi consolidado em `TryAllocate`, e o teste passou sem relaxar a prontidão ou a condição elétrica.

PASS `industry-progress-diagnostic-regression-20261012-031344-926`: 21 verificações da mesma DLL final; bloqueios de energia/profissão/ingredientes/filtro/habilidade/turno, progresso nativo antes da conclusão, fabricação real de blocos de mármore, relógio de gargalo e persistência após save/load. Total final: 42 verificações de contratos/diagnóstico, além dos ensaios de produção/abastecimento/coordenação registrados acima com seus respectivos hashes.

DLL instalada conferida igual à compilada. Addon temporário removido, nenhum processo de teste permaneceu aberto. V3 e V4 pessoais mantêm hashes `01B0BEC2823B5711CE0CC528CEFE61CF5DC3AD9F6E4A1AB62A598107F0E695FD` e `A1EE5524D1E65D0ABF6B3786823D8268837688FE2FA086149C8C4EF39B098DBE`. Alterações locais sobre `fb5d233`; sem novo commit/push neste pacote.

## Limites

Não redistribui uma peça inacabada para outro criador; não assume que o diagnóstico sozinho garante um job imediato. A retomada depende de reserva, acesso, habilitação, turno e recuperação normais. A limpeza industrial não apaga pedidos comerciais independentes que já existiam, nem cancela entregas contratadas. Não foi executado benchmark de campanha, teste visual da HUD ou fabricação avançada de todos os sets.

## Próximo checkpoint

Bancadas necessárias e armazenamento de insumos junto delas, cultivo de tecido conforme demanda e validação de equipamentos avançados e compra/entrega industrial completas. Manter as regressões nativas deste pacote.
