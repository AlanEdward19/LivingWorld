# Nomes, linhagem e honra

Como uma pessoa se chama, de onde vem o nome e o que ele faz na vida dela (AD-178, AD-183). Saiu de [npc.md](npc.md).

## O nome

"próprio família" (`Name`; `GivenName`/`FamilyName` = antes/depois do último espaço, sem campo novo no snapshot). Cada
cultura tem a sua fonologia, sorteada pelo RNG do mundo (`StreamFor("culture-naming", cultura)`): 9 das mesmas sílabas dos
nomes de cidade, terminações feminina, masculina e de família, e o costume — linhagem (70% patrilinear), cônjuge que adota o
nome da linhagem (50%), chance de homenagear um parente (15–50%). Fundador nasce de família própria; a casa e as crianças levam
o nome da linhagem do casal; fora do casamento, o da mãe; o costume dá ao bebê o nome próprio de um avô/avó do mesmo sexo (de
preferência morto) ou do pai morto antes do parto; órfão acolhido mantém o nome; família autorada mantém o escolhido; sem
homônimo vivo na casa. Mundo salvo antes da AD-178 mantém o `npc-…`. Ficha: "casa dos <família>".

**Costume declarado (AD-183):** o cenário ou período pode fixar o costume por cultura no bloco opcional
`{"Naming": {"Cultures": [{"Culture": 1, "Lineage": "Matrilineal", "SpouseTakesName": false, "AncestorNameChance": 0.3,
"DaughterCity": "Founder"}]}}` (`NamingCustom`, validado na carga, guardado em `FamilyRules.Names.Customs`, no snapshot).
Campo ausente = o sorteado; bloco ausente = o sorteio por cultura.

## A cidade

A cidade leva nome da língua da cultura que a funda (sílabas e sufixo de lugar dela). **Cidade-filha (AD-183):** quem lidera a
saída do aglomerado funda (`Founders.Pick`, sem sorteio): o adulto que leva mais gente consigo (a própria casa e as de pais,
filhos e irmãos que saem junto), depois meios (carteira, ×0,5), idade de 25 a 55 anos (×0,5) e boa fama vista (×0,5); empate
pelo menor id. Fica em `City.FounderId` (ficha da cidade: `FounderId`, `FounderName`, link `founder`), a fundação vai ao log
(`SettlementFounded`: cidade, mãe, fundador, costume do nome) e o feito dá 30 de boa fama ao fundador. O costume da cultura
decide o nome: 40% das culturas dão à filha a 1ª sílaba da mãe; das outras, 35% dão o nome próprio do fundador com um sufixo
de lugar; o resto, nome novo. A fundação pelo pool (sem gente materializada) não tem fundador.

## O que o nome faz (`FamilyRules.Names`, `LW_NAMES` nos Workers)

| Efeito | Onde entra | Motivo no evento |
|---|---|---|
| Xará (nome próprio de pai/mãe ou avô/avó, mesmo sexo) socorre com +50% de chance | ajuda mútua | `AidGiven` motivo `xara` |
| Linhagem (mesmo nome de família na cidade) chamada à ajuda, proximidade 0,3 | ajuda mútua | `AidGiven` motivo `linhagem` |
| Xará enluta o homenageado (×0,6 do luto de amigo) | luto | `/npcs/{id}/brain` `Motives`: `Grief`/`xara` |
| Xará herda 10% da carteira antes da partilha | espólio | `EstateSettled` xará e valor |
| Mesmo nome sem ascendente comum: preferência ×0,5, também em quem é pedido | namoro | `CourtshipRejected` `MesmoNome` |
| Má fama do pior da linhagem na cidade respinga ×0,25 | suspeita, troca de culpado, crédito | `CrimeReported` `fama-da-linhagem` |
| Boa fama do mais honrado da linhagem respinga ×0,25 (AD-183) | ver abaixo | — |

## Honra (AD-183)

Boa fama (0–100, meia-vida de 3 anos como a má fama) guardada em `WorldState.Honor` (`HonorLedger`, só quem fez algo; nulo
fora do snapshot e do hash). Feitos, lidos do log de eventos (`Honor.OnEvent`): socorro dado a outra casa +2, coisa nova que
funcionou +15, negócio aberto +5, cidade fundada +30. O registro guarda nome de família e cidade: a linhagem lembra o feito do
morto até ele se apagar. **Boa fama vista** = a própria + 0,25 × a do mais honrado da linhagem na cidade (o que a cidade sabe
das linhagens é montado uma vez por tick: o feito do dia corre a cidade no dia seguinte). Consumidores: a suspeita, a troca de
culpado e o crédito leem a má fama vista menos 0,5 × a boa fama vista; o credor que não é parente ganha até 20 de confiança; no
namoro a preferência é ×(1 + 0,2 × nome) e, com vaga escassa, quem tem nome melhor é contratado antes (nome = boa fama vista
menos má fama vista, −1..1). Chaves: `-honor`, `-founder`, `-matchrep`, `-hirerep`, `-refuse`.

## Luto mostrado

O `Mourning` da projeção usa o critério do motor (`PsycheMechanic.Mourns`): luto ainda sentido (≥ 2%, o limiar abaixo do
qual o motor dá a emoção por passada); antes era ≥ 10%, só de tela.

## Confiança por número (pesquisa da AD-183; precisa de conferência)

- Homenagem a avós 15–50%: **média**. Registros paroquiais variam muito: Ljustorp (Suécia, séc. XVI–XVIII) ~90% dos 1ºs filhos
  com o nome do avô previsto; Inglaterra séc. XVI 80–90% com o nome do padrinho (muitas vezes avô), Smith-Bannister 1997;
  Estônia quase nunca. A faixa é o meio; um período fixa o seu no bloco `Naming`.
- Mesmo nome no namoro ×0,5: **baixa**. Há casos extremos (Coreia proibiu casar no mesmo sobrenome e clã até 1997/2005; a lei
  chinesa antiga vetava o mesmo sobrenome) e lugares sem tabu nenhum (Europa); 0,5 é meio-termo de calibração.
- Xará +50% na ajuda, ×0,6 no luto, 10% de legado: **baixa**. Lembrança de compadrio e padrinho protetor (Mintz e Wolf 1950),
  sem medida.
- Linhagem 0,3 na ajuda, respingo 0,25 da fama (boa e má), honra que desconta 0,5: **baixa**. A ideia de honra e vergonha de
  família vem de Peristiany (org.) 1965 e Pitt-Rivers 1965; os números são calibração.
- Pesos dos feitos (2/15/5/30), confiança 20, namoro 0,2, pesos do fundador: **calibração sem fonte**.
