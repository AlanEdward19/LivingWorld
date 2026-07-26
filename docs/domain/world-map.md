# Mapa e Geografia

O substrato espacial do mundo: onde as coisas estão, o que o solo oferece e quanto custa ir
de um lugar a outro.

## Representação

Visualização **2D** na primeira versão (cliente React). Internamente o mundo é um conjunto
de regiões/células, cada uma com:

| Propriedade | Papel |
|---|---|
| Terreno | Planície, floresta, montanha, pântano, deserto |
| Bioma | Regime climático e vegetação dominante |
| Recursos | O que pode ser extraído ali |
| Altitude | Influencia clima, defesa e custo de travessia |
| Água | Rio, lago, costa — irrigação, pesca, transporte barato |

A visualização é 2D; o modelo interno não é — trocar o cliente por 3D não toca no domínio.

## Camadas

O mesmo mapa é lido por camadas sobrepostas: terreno, biomas, rios, montanhas, recursos,
estradas, fronteiras, reinos, cidades, aldeias, rotas comerciais, migrações, conflitos e
clima. Cada camada é uma leitura do mesmo estado, não um mapa separado.

## Navegação em drill-down

```
mapa mundial -> região/reino -> cidade -> distrito -> local -> NPCs -> interação
```

Concretamente:

> Mapa mundial → Reino de Eldor → Cidade de Valen → Distrito do Mercado → Taverna do Corvo
> → Mirena (proprietária), Hakon (mercador), Lira (viajante)

Cada degrau do drill-down também é um degrau de LOD: descer o mapa materializa NPCs.

## Recursos alimentam a economia

Os recursos por célula — trigo, madeira, ferro, água, pedra, caça, peixe — são a entrada da
produção. Uma cidade cercada de floresta e sem ferro não faz armas: ela compra, ou depende
de quem faz. A geografia é a primeira causa da especialização econômica.

## Distância, terreno e rotas

Custo de deslocamento sai de distância **mais** terreno: atravessar montanha custa mais que
seguir um rio. Esse custo decide se uma rota comercial é viável, para onde a migração flui
quando uma região empobrece e quanto tempo um exército ou caravana leva no caminho. É aqui
que geografia deixa de ser cenário e vira restrição econômica e demográfica.

## Particionamento espacial

A divisão em regiões serve à performance: o LOD usa **proximidade de jogador por região**
para decidir o que roda agregado e o que roda detalhado. Sem partição espacial não dá para
perguntar barato "quem está perto de quem".

## O mapa é dado, não código

O mapa pertence ao cenário — gerado proceduralmente ou desenhado à mão — e é carregado pelo
motor. Nada de nomes de reino, coordenadas ou recursos hardcoded no engine: trocar o
cenário troca o mundo inteiro sem recompilar.

## Ver também
- [economy.md](economy.md) — recursos, produção e rotas comerciais
- [cities.md](cities.md) — assentamentos, distritos e crescimento
- [simulation-lod.md](simulation-lod.md) — proximidade de jogador e resolução
- [society.md](society.md) — fronteiras, reinos e facções
- [history.md](history.md) — conflitos e migrações registrados no tempo
