# Sandbox: casal no subúrbio (rumo da fatia a partir de 2026-11-27)

**Pedido do dono:** "focarmos em um sandbox menor: um casal de 2 pessoas em uma casa no subúrbio,
podermos controlar qualquer um deles; os móveis cadastrados no motor, interagir com eles, possuir
um deles, validar toda a gameplay e daí ir evoluindo." O mundo grande de hoje parece morto e pouco
realista (dezenas de NPCs grudados trabalhando no mesmo bloco) — vira issue à parte.

## O mundo do sandbox
- Um cenário em dados (sem nada escrito no código): período moderno, `InitialPopulation` 0 e uma
  família autorada (`AuthoredHouseholds`, já existe): casal adulto casado.
- Uma casa de subúrbio com lote, quintal e calçada; tamanho e cômodos (quarto, cozinha, sala,
  banheiro) vêm do cenário; a rua e um ou dois lotes vizinhos vazios para crescer depois.
- Móveis do catálogo do cenário (cama de casal, fogão, geladeira, mesa e cadeiras, sofá, chuveiro
  ou banheira, vaso, pia, TV ou rádio), cada um com usos e efeitos genéricos declarados em dados.
- Comida inicial na geladeira e uma forma de repor (decidir no S1: mercado como prédio com estoque,
  ou entrega), sem moeda criada do nada.

## Etapas
**S1 — o mundo e o controle** [implementado em #252, AD-218; ver docs/domain/sandbox.md]. Cenário do sandbox no menu ("Sandbox: casal"); entro em qualquer um
dos dois e troco entre eles a qualquer momento (o outro segue a própria vontade pelo motor); casa
com cômodos de tamanho jogável e câmera de interior (#250). Aceite: criar o mundo, entrar num, trocar
para o outro, andar pela casa toda, usar cama, mesa e fogão.

**S2 — necessidades e objetos (antigo M4)** [implementado em #255, AD-220; ver docs/domain/needs-and-objects.md]. Higiene, diversão e bexiga como necessidades do motor
(regras em dados); catálogo de objetos todo em dados (`DefaultObjects.cs` sai do código); toda ação
com duração, animação e cancelamento; fila de ações. Os dois usam os mesmos objetos.
Aceite: um dia inteiro cuidando das barras só com a casa; o parceiro não controlado se cuida sozinho.

**S3 — o casal (antigo M6).** Menu social entre os dois e relação visível; o motor decide a reação.

**Depois:** vizinhos, emprego, mercado, compra e arrumação de móveis (M5), conversa por IA (M7).
Cada etapa mede FPS e custo e roda só os testes rápidos (longrunning ao fim da fatia).
