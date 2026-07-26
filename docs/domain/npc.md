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

## Atributos

**Físicos:** força, resistência, agilidade, saúde, fertilidade, longevidade, aparência,
percepção — governam trabalho braçal, combate, doença e reprodução.

**Cognitivos:** inteligência, memória, criatividade, disciplina, curiosidade, capacidade
social, agressividade, empatia — governam aprendizado, decisão e vida social.

## Personalidade (0–100)

Extroversão, amabilidade, conscienciosidade, estabilidade emocional, abertura, ambição,
lealdade, altruísmo, impulsividade, aversão ao risco.

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
importarem.

## Ciclo de vida

| Estágio | O que muda |
|---|---|
| Criança | Não produz; aprende dos pais e do ambiente; atributos ainda se formando |
| Aprendiz | Entra em ofício ou escola; habilidades sobem rápido; começa a formar relações |
| Adulto | Produtivo, reprodutivo, politicamente ativo; auge físico e de renda |
| Idoso | Físico decai, prestígio e conhecimento sobem; ensina, lidera, transmite herança |

## Ver também
- [behavior.md](behavior.md) — como esses números viram decisão
- [genetics-and-family.md](genetics-and-family.md) — o que é herdado e o que é ambiental
- [memory.md](memory.md) — o que o NPC guarda do que viveu
- [simulation-lod.md](simulation-lod.md) — quando um NPC existe em detalhe
- [llm-contract.md](llm-contract.md) — o que da ficha vai para o diálogo
