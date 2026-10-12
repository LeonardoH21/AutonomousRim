# Correção dos módulos conforme o save do usuário

Referência: `13X13 Todas as construções.rws`, SHA256 `D5B5BFEDE9E265261EDB8DB0DD6A53A15D6600EB72EE140C28ABB638CE46D81A`. O original foi somente lido. As coordenadas compiladas podem ser regeneradas com `scripts/GenerateReferenceLayouts.py` a partir do catálogo extraído.

## Implementação

- **Energia:** metade de um módulo 13×13, com 11×5 células internas, seis posições de geradores, sete de baterias e a saída de ar externa original. A IA preenche a sala por demanda antes de reservar outra sala compacta. O aquecimento não entra na energia. Cabos aproveitam a transmissão de geradores/baterias e não sobrepõem esses objetos.
- **Pesquisa:** divisórias e móveis do conjunto pesquisa/dois quartos. Duas bancadas simples no começo, bancadas avançadas e livros quando pesquisados. O multianalisador ocupa a posição `(10,8)` da bancada simples direita; não é construído outro laboratório separado para essa evolução.
- **Comida:** freezer, fogão e mesa de abate no mesmo módulo, mantendo cozinha e abate em salas diferentes. A airlock conserva as portas nas posições `(1,8)` e `(1,12)`. Dois resfriadores usam alvo de −2°C e descargas externas sem teto. Prateleiras usam posições de referência, filtro alimentar e expansão conforme demanda.
- **Recreação avançada:** mesa 2×4, bancos, xadrez, hoopstone rings; depois sinuca, poker, TV grande e seis sofás nas posições do usuário. As instalações posteriores respeitam pesquisas.
- **Hospital:** camas normais médicas inicialmente, dois espaços exclusivos para remédios com prioridade Critical; depois seis camas hospitalares, seis monitores e a prateleira medicinal original. A prateleira substitui a zona de chão de acordo com o armazenamento nativo; referências a zonas vazias são descartadas antes de salvar.
- **Despejo:** a antiga “Stockpile zone 3” era o despejo automático. Zonas automáticas recebem nomes funcionais. O despejo aceita pedaços de pedra e corpos de animais; o estoque comum deixa de aceitar pedaços de pedra. Um trabalho nativo de HaulToCell leva pedras de paredes, pisos ou interações planejadas ao despejo, respeitando segurança, necessidades, prioridades e reservas do colono.
- **Terreno:** as entradas efetivas de cada modelo e a descarga/saída de ar recebem preparação adicional de corte e mineração. As áreas continuam exigindo apoio estrutural; não são colocadas paredes sobre terreno sem suporte.

Os quartos e a prisão aprovados foram preservados. Salas já construídas em saves anteriores não são demolidas automaticamente para impor a nova planta. O save de revisão é uma cópia nova para avaliar o resultado completo.

## Troca de móveis na gameplay

Somente móveis automáticos podem receber upgrades automáticos. A bancada antiga permanece disponível se faltarem recursos para sua substituição. Quando houver orçamento, o executor designa desmontagem nativa e depois emite o blueprint com custos e trabalho normais. Cancelar a desmontagem manualmente interrompe o upgrade; desligar construção remove somente designações de upgrade pertencentes à IA. Emergências suspendem novas trocas.

## Validação

As primeiras execuções detectaram e ajudaram a corrigir conflitos de cabos nas baterias e a persistência da antiga zona medicinal depois da instalação da prateleira. O teste também precisou limpar a fila anterior do trabalhador antes de isolar o transporte da pedra; a produção mantém as proteções contra interrupção de trabalhos prioritários.

O teste nativo usa os seis personagens originais do cenário Enhanced World, em uma cópia isolada, modo pacífico, velocidade 3×, pesquisas concluídas e construção administrativa. Verifica estrutura física, separação das salas, acessos às bancadas, equipamentos, iluminação/energia, filtros, freezer, vínculos do multianalisador e monitores médicos, além de salvar/recarregar. Uma pedra é levada por um colono usando o trabalho normal, sem teleporte. Há checagens de orçamento/cancelamento de upgrades e de obstáculos nas entradas reais.

**Limite:** esse teste prova a instalação administrativa e essas mecânicas; não comprova construção da base inteira por trabalho normal, progressão tecnológica desde o começo ou sobrevivência prolongada. O cenário original e o save desenhado pelo usuário foram preservados.

### Resultado final

**PASS** em `.tools/validation/reference-layout-v3-final-20261011-174449-243/manifest.json`, com `[AllModules] DONE` no log. As verificações físicas e operacionais passaram antes e depois de salvar/recarregar. Todos os 60 consumidores estavam energizados; consumo nominal de 6.775 W, oito geradores (seis no banco principal e dois no auxiliar), sete baterias no banco principal. Freezer: −1,8°C antes e −1,6°C depois da recarga, termostatos em −2°C; ambas as leituras abaixo de zero.

Verificados os vínculos nativos do multianalisador e dos seis monitores médicos, o transporte real da pedra por HaulToCell, a preparação de obstáculos nas entradas/ventilação, e os controles de orçamento/cancelamento de upgrades. A compilação de produção passou sem avisos/erros; a biblioteca de testes compilou com o aviso NU1900 sobre a consulta de vulnerabilidades do NuGet indisponível. Os 57 checks de políticas também passaram.

Save limpo de componentes do add-on de testes: `outputs/ModuleAudit/AutonomousRim-Modulos-ADMIN-reference-layout-v3-final-20261011-174449-243.rws`. Também copiado para a pasta de saves do jogo como **AutonomousRim-Modulos-Referencia-11-10-V3**. SHA256: `EFCD4DB57D8A558BF7B6D364515BFF79CABE7346686CB12FD3653B232CC72B43`. Os seis IDs dos colonos originais foram conferidos no export; nenhuma dependência RuntimeChecks permanece.

DLL instalada/testada SHA256: `5C0A1B547510B809806BD0B5A84C1A7FC044C148B0482FF8DDF7966EFC69C214`. Screenshot: `outputs/ModuleAudit/AutonomousRim-Referencia-V3.png`. A automação permanece desligada no save de revisão administrativa; o usuário pode habilitá-la pela HUD. O save original de referência e o cenário-base tiveram seus hashes reconferidos e permaneceram inalterados.
