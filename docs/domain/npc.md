# NPC

O indivíduo simulado do Living World: o que compõe uma pessoa, quais números a descrevem e
como ela muda ao longo da vida.

## Um agente simulado, não um agente de LLM

Um NPC **não pensa via modelo de linguagem**. A imensa maioria da vida dele — acordar,
trabalhar, envelhecer, casar, brigar, morrer — roda em regra determinística e probabilidade
barata no motor. A LLM entra só na conversa, e ainda assim apenas interpreta o estado
pronto.

## Identidade

| Campo | Observação |
|---|---|
| Nome, sexo, data de nascimento | Data em `WorldDate`, não em tempo real |
| Espécie, cultura, idioma | Herdados do contexto de nascimento, não do sangue |
| Família, local de nascimento, residência | Residência muda com migração |
| Profissão, classe social | Mudam com a vida; ver behavior.md |
| Religião, facção | Podem ser abandonadas ou trocadas |

**Nome (AD-178):** "próprio família" (`Name`; `GivenName`/`FamilyName` = antes/depois do último espaço, sem campo novo no
snapshot). Cada cultura tem a sua fonologia, sorteada pelo RNG do mundo (`StreamFor("culture-naming", cultura)`): 9 das
mesmas sílabas dos nomes de cidade, terminações feminina, masculina e de família, e o costume — linhagem (70% patrilinear),
cônjuge que adota o nome da linhagem (50%), chance de homenagear um parente (15–50%). Regras: fundador nasce de família
própria, a casa e as crianças levam o nome da linhagem do casal; recém-nascido de casal casado leva o da linhagem, fora do
casamento o da mãe; o costume dá ao bebê o nome próprio de um avô/avó do mesmo sexo (de preferência morto) ou do pai morto
antes do parto; órfão acolhido mantém o nome; materializado ganha nome novo; família autorada (cenário, jogador) mantém o
escolhido; sem homônimo vivo na casa. Mundo salvo antes mantém o `npc-…` (filho dele ganha família estável pelo id do pai/mãe).
Ficha: "casa dos <família>". A cidade leva nome da língua da cultura que a funda (sílabas e sufixo de lugar dela; com o
costume, 40% das culturas, a filha herda a primeira sílaba da mãe).

**O nome pesa (`FamilyRules.Names`, `LW_NAMES` nos Workers):** xará = mesmo sexo e nome próprio de pai/mãe ou avô/avó.
Na ajuda mútua o xará socorre com +50% de chance e a linhagem (mesmo nome de família na cidade) é chamada com proximidade
0,3; o xará enluta o homenageado (luto de amigo ×0,6) e herda 10% da carteira dele antes da partilha; no namoro, o mesmo nome
de família sem ascendente comum conhecido corta a preferência pela metade; a má fama do pior da linhagem na cidade respinga
×0,25 na suspeita, na troca de culpado e no crédito. Quem troca de nome ao casar passa à linhagem do cônjuge.

## Atributos

**Físicos:** força, resistência, agilidade, saúde, fertilidade, longevidade, aparência,
percepção — governam trabalho braçal, combate, doença e reprodução.

**Corpo (AD-134):** nasce com tamanho de recém-nascido (~50 cm, ~3,3 kg; menor se prematuro), cresce pela curva de
idade e sexo com estirão na puberdade (antes nas meninas) até ~1,71 m (homem) e ~1,59 m (mulher), encolhe e perde
músculo na velhice. O músculo sobe com trabalho braçal, cai sem uso e na fome. A saúde é uma reserva: cai com fome, sede,
doença, parto difícil e velhice; volta com comida e sono, devagar no velho. Quem está fraco trabalha menos e anda mais
devagar; a desnutrição pesa no risco de morte, sobretudo da criança. O bebê mama na mãe (ou noutra lactante da casa) até
o desmame, entre 6 meses e 2 anos; sem leite, come papa com risco alto. Doenças de época (AD-137, [disease.md](disease.md)) pegam pela
convivência, deixam de cama, tiram saúde e explicam boa parte das mortes.

**Cognitivos:** inteligência, memória, criatividade, disciplina, curiosidade, capacidade
social, agressividade, empatia — governam aprendizado, decisão e vida social.

## Personalidade (0–100)

Extroversão, amabilidade, conscienciosidade, estabilidade emocional, abertura, ambição,
lealdade, altruísmo, impulsividade, aversão ao risco. Herdada no nascimento e, ao virar adulto,
acrescida do ambiente compartilhado de quem de fato criou a criança (AD-132, `RearingRules`; peso 0,1 desde o AD-162:
o ambiente compartilhado quase não pesa na personalidade, Plomin e Daniels 1987).

Não é enfeite de ficha: a personalidade **modula** profissão, relações, decisão momento a
momento, reprodução, conflito, ritmo de aprendizado, migração e tom de diálogo. Sempre como
peso, nunca como trava.

## Necessidades

Medidores que decaem com o tempo: fome, sede, sono, segurança, saúde, socialização,
pertencimento, prestígio, afeto, diversão, propósito.

Necessidade alta **gera objetivo automaticamente** — não existe lista de tarefas escrita à
mão. Fome 90 vira o objetivo "comer" sozinha; a utility AI decide como satisfazê-lo.

## Habilidades

Agricultura, caça, comércio, construção, medicina, combate, ensino, artesanato, política,
liderança, pesquisa, tecnologia, magia. Crescem por prática, treinamento, educação, observação, tutoria, predisposição genética,
cultura e acesso a recursos. A curva tem **retornos decrescentes**: de 10 para 20 é fácil;
de 80 para 90 exige anos, mestre e material — mestria rara faz linhagem e instituição
importarem. Desde AD-132 a idade pesa: bebê não aprende ofício; a criança ajuda em casa a partir dos ~5 anos e
aprende dos adultos com quem mora (pais, avós, tios, quem a acolheu) até metade do que eles sabem; dos 12 anos o
aprendiz ganha um mestre; vendo, só se aprende o que o outro sabe. A habilidade fora de uso cai ~4%/ano e a manual
cai depois dos 55. Na vila padrão: ~6 aos 10 anos, ~28 aos 15, ~48 aos 20, ~77 aos 30, ~93 aos 50.

## Ciclo de vida

| Estágio | O que muda |
|---|---|
| Criança | Não produz; dos ~5 anos aprende o ofício de quem a cria; valores de quem a cria entram na personalidade |
| Aprendiz | Dos 12 anos, com um mestre; habilidade sobe por anos, com retorno decrescente |
| Adulto | Produtivo, reprodutivo, politicamente ativo; auge físico e de renda |
| Idoso | Físico decai, prestígio e conhecimento sobem; ensina, lidera, transmite herança |

## Ver também
- [behavior.md](behavior.md) — como esses números viram decisão
- [genetics-and-family.md](genetics-and-family.md) — o que é herdado e o que é ambiental
- [memory.md](memory.md) — o que o NPC guarda do que viveu
- [simulation-lod.md](simulation-lod.md) — quando um NPC existe em detalhe
- [llm-contract.md](llm-contract.md) — o que da ficha vai para o diálogo
