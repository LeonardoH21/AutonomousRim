# Base compacta, conforto e clima

Implementação de 5 de outubro de 2026. Os novos planos usam um bloco com corredor central coberto de duas células, portas dos quartos voltadas para ele e duas saídas externas. Quartos têm 5×5 células internas; cozinha 4×4; estoque e sala social 6×6. O planejador aceita o bloco inteiro antes de adicionar módulos, preservando zonas, estruturas e itens proibidos. Trabalhadores podem transportar itens permitidos para fora do caminho pelas tarefas normais do jogo, sem destruí-los.

Exemplo para três colonos (esquema de setores, não escala):

![Esquema orientativo da base compacta](base-compacta.svg)

```text
           saída norte / calor para o exterior
       ┌─────────────┬──┬─────────────┐
       │ quarto 1    │  │ quarto 2    │
       ├─────────────┤  ├─────────────┤
       │ quarto 3    │  │ estoque     │
       ├─────────────┤  ├─────────────┤
       │ cozinha     │  │ sala social │
       └─────────────┴──┴─────────────┘
                     saída sul
         corredor: 2 células de largura
```

As salas são adicionadas em pares; tamanhos diferentes deixam recuos nas fachadas. Um novo bloco é reservado para expansão quando necessário. Projetos antigos permanecem no local original. O mod não demole a base para impor o novo estilo.

## Ordem construída nesta versão

1. Quartos, camas e cobertura.
2. Estoque seco e cozinha com fogão a lenha e bancada de açougue, conforme a preferência solicitada.
3. Sala social com mesa, assentos e xadrez.
4. Corredor fechado, cobertura, saídas e, quando disponíveis, ventiladores e resfriadores.
5. Geração, cabos, aquecedores e luzes dos cômodos.
6. Mesa de cabeceira e cômoda por quarto.
7. Pisos uniformes nos ambientes e no corredor.

Cada etapa exige os materiais reservados antes de abrir novas obras. A fila continua limitada a seis projetos por ciclo e doze pendentes. Trabalhadores reais executam as tarefas; o mod não cria prédios instantaneamente. Hostis interrompem novas ordens. Cancelamentos ou mudanças manuais pausam o módulo afetado; desligar a base remove os projetos pendentes da IA e mantém estruturas e obras que já receberam materiais.

## Estilo e felicidade

- Um conjunto uniforme de materiais, corredores livres e móveis alinhados dá uma aparência organizada sem aumentar excessivamente a área.
- Paredes de pedra são preferidas quando há blocos suficientes para o conjunto. Portas e móveis começam em madeira. Pisos de pedra, preferencialmente mármore, são escolhidos quando há pesquisa e estoque suficiente; o plano reserva os blocos das paredes antes dos pisos. O fallback é madeira, que é inflamável.
- Mesa de cabeceira e cômoda são colocadas para se conectarem às camas; não adianta acumular várias do mesmo tipo junto à mesma cama. O teste consulta as conexões reais das instalações de conforto.
- A sala social reúne refeição e recreação, economizando deslocamentos e permitindo desenvolver um ambiente mais impressionante. A qualidade dos móveis depende de quem os constrói.
- Pisos eliminam o solo exposto e facilitam manter o ambiente limpo. Não tornam a cozinha estéril e não substituem o trabalho de limpeza. Arte, pintura e plantio em vasos ainda não são executados automaticamente.
- Não há bônus de humor garantido por um tamanho fixo: impressividade, beleza, limpeza, espaço, qualidade, necessidades e preferências dos colonos continuam sendo calculados pelo jogo.

**Evitar:** entulho e cadáveres nos ambientes, animais sujando a cozinha, móveis bloqueando portas/interações, pisos caros antes de comida e abrigo, madeira como material definitivo em locais de alto risco de incêndio e gastar toda a reserva em decoração.

A cozinha continua com açougue junto, como solicitado. Isso concentra trabalho sujo no ambiente de preparo. Uma melhoria futura recomendada é um anexo separado para açougue, com acesso curto e sem passagem pela bancada de cozinha.

## Temperatura e energia

A HUD mostra a estimativa mínima/máxima sazonal do próprio jogo e a temperatura externa atual. Um bioma aparentemente confortável ainda pode enfrentar ondas de calor ou frio.

Quando **Eletricidade, Ar-condicionado e Móveis complexos já estão pesquisados ao gerar o bloco**, o plano inclui:

- Uma ventilação direta entre cada sala e o corredor, em parede simples. Cada lado dá para células internas livres; não existe uma segunda parede bloqueando o ar.
- Um resfriador em cada extremidade do corredor. O lado frio aponta para dentro e o ar quente para o exterior livre e sem teto. As saídas para pedestres continuam ao lado deles.
- Aquecedores distribuídos pelo corredor, com alvo de **20 °C**, e resfriadores com alvo de **24 °C**. Essa faixa evita que aquecimento e resfriamento disputem a mesma temperatura.
- Luz elétrica em cada sala e cabos conectando todos os consumidores.
- Geradores a lenha em área externa. A quantidade é calculada usando os consumos máximos reais dos equipamentos e uma margem elétrica de 20%; cabos não são sobrepostos aos geradores.

Os termostatos são configurados uma vez; alterações posteriores do jogador são preservadas. O jogo controla o funcionamento automaticamente. Reabastecimento, manutenção, destruição, falta de madeira, portas abertas e extremos climáticos podem impedir atingir a faixa. Potência elétrica suficiente não significa capacidade térmica ilimitada. O mod ainda não dimensiona resfriadores pela perda térmica, não aumenta a climatização após eventos extremos e não troca a fonte por energia solar/geotérmica.

Se essas pesquisas faltarem, o bloco básico continua disponível sem climatização elétrica. O mod não inicia pesquisas nem reforma automaticamente um bloco antigo depois da pesquisa. Em biomas sem madeira, combustível e materiais são uma dependência explícita; ainda falta uma política para fontes alternativas e proteção térmica emergencial.

**Evitar:** mandar calor para dentro da base, cobrir o escape do resfriador, usar ventiladores em paredes duplas, conectar o freezer ao corredor aquecido, depender de gerador sem abastecimento e manter portas externas abertas. Ventiladores equilibram temperatura; não são um sistema de oxigênio.

Freezer deve ser isolado do circuito de conforto, com termostato abaixo de zero e, quando necessário, isolamento e antecâmara próprios. Freezer, hospital, agricultura, produção de roupas e defesas continuam nas etapas futuras descritas no plano geral.

## Verificação

O teste isolado distingue construção real de preparação acelerada: um colono constrói um quarto inteiro com paredes, porta/ventilação, cama e teto. Os demais módulos são montados usando transições nativas de projeto → estrutura → construção para verificar posições, pisos, interações, compartilhamento de limites e instalações. O jogo executa a rede elétrica e os equipamentos; o teste força condições de frio/calor e verifica a variação real de temperatura, alimentação das luzes e termostatos, conexões dos móveis, saídas e escape externo. Isso não representa um teste prolongado de sobrevivência em todos os biomas.

Quando o mapa sorteado não tem um retângulo livre, o teste verifica a recusa do planejador e prepara terreno apenas na colônia descartável. A montagem acelerada também retira itens permitidos do local, preservando-os. Essas preparações não fazem parte da automação normal.

Resultado: compilação sem erros/avisos, 35 verificações de cálculo aprovadas e teste visível no RimWorld 1.6.4633 aprovado com 25 ciclos de leitura, construção, climatização, instalações, pisos, iluminação e captura da HUD sem exceções. DLL instalada conferida por SHA-256. Testes prolongados de sobrevivência e salvamento/carregamento continuam necessários.

## Referências consultadas

As definições XML e classes da instalação RimWorld 1.6.4633 são a fonte de custos, consumo, instalações e posicionamento. Referências de mecânicas:

- [Salas e impressividade](https://rimworldwiki.com/wiki/Rooms)
- [Temperatura e isolamento](https://rimworldwiki.com/wiki/Temperature)
- [Ventilação e paredes adjacentes](https://rimworldwiki.com/wiki/Vent)
- [Mesa de cabeceira](https://rimworldwiki.com/wiki/End_table)
- [Cômoda](https://rimworldwiki.com/wiki/Dresser)
