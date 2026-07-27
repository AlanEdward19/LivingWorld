# Fase 17 — Divindade e economia de crença

**Objetivo**: um deus não é entidade nova — é potência da Fase 16 acoplada a um recurso,
fiéis, e à camada de crença da Fase 10. O ciclo **fiéis → poder → manifestação → fiéis** roda
nos dois sentidos, o esquecimento drena, a distorção muda a natureza do deus, e de dentro do
mundo ninguém distingue deus real de deus falso.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 8 fechar.

## Tasks
1. **Deus como portador de potência** com pool de crença = nº de fiéis × intensidade da
   devoção × frequência de retransmissão. Nenhum eixo de `powers.md` é reescrito.
2. **Realimentação**: manifestação vira relato na Fase 10; relato retransmitido alimenta o
   pool; o cânone limitado empurra o culto para fora quando ninguém o retransmite.
3. **Decaimento por esquecimento** até um **piso declarado no cenário**, monotônico enquanto
   não houver retransmissão.
4. **Natureza derivada da doutrina corrente**, nunca campo fixo: os operadores de distorção
   do ADR-0007 (moralização, perda de causa, troca de atribuição, fusão) agem sobre a
   doutrina como agem sobre batalhas. O deus da colheita vira deus da guerra sem decidir.
5. **Culto como instituição**, reusando Fases 5 e 8: templo é edifício com renda e
   empregados, sacerdote é profissão, dízimo é transação, doutrina é conhecimento
   transmitido, cisma é divergência cultural.
6. **Panteão com pool disputado**: vários deuses partilham a mesma população; crescer é
   tirar do vizinho. Perseguição, sincretismo e guerra santa saem daí, sem roteiro.
7. **Intervenção divina = invocação de potência**: custo em pool, rolagem, modo de falha com
   consequência (presságio ambíguo, milagre no fiel errado, sinal caro e inútil).
8. **Realidade do deus só na consulta de Verdade** da Fase 10. Nenhum handler de jogo a
   resolve; nenhum caminho responde "esse deus é real?".
9. **Cenários pareados**: culto perseguido vs. culto em paz; deus real vs. mito com a mesma
   trajetória de fiéis; doutrina com e sem operadores de distorção ligados.

## Critérios de verificação
- **Deus sem fiéis decai, e só até o piso**: cortada a retransmissão, `poder(t+1) ≤ poder(t)`
  a cada tick em 10 anos, convergindo ao piso declarado no cenário e nunca abaixo dele.
  Uma única subida sem manifestação nem fiel novo reprova.
- **Perseguição bate o esquecimento natural**: par base/tratamento na mesma seed, tratamento
  = perseguição ao culto. `poder(trat) < poder(base)`, com a diferença **maior que o spread
  entre duas seeds do baseline** — senão é só decaimento normal. 10/10 seeds.
- **A distorção muda a natureza do deus, e o controle prova isso**: par na mesma seed com o
  braço de controle rodando **sem** os operadores do ADR-0007. No braço tratado, a natureza
  corrente diverge da fundadora em pelo menos o número de cultos declarado no cenário; no
  braço de controle, **zero** divergências. Sem o controle, deriva de doutrina explicaria.
- **Crença nunca revela realidade**: enumerar por reflexão **todos** os handlers de consulta
  de crença e exigir que nenhum resolva para o campo de realidade do deus — falha se algum
  handler ficar sem cobertura. Par de mutação, igual ao da Fase 10: desligar a checagem por
  flag de teste tem de **fazer este critério falhar**.
- **Deus esvaziado e mito em ascensão são indistinguíveis**: no tick em que os dois cenários
  têm o mesmo pool e nenhuma manifestação na janela declarada, toda a superfície de consulta
  de crença retorna respostas byte-idênticas. Qualquer divergência é vazamento.
- **Crença entrou na conta**: desligar a economia de crença muda o hash canônico em 10 anos.

## Questões em aberto
- O pool de fiéis é **conservado** (crescer é tirar do vizinho) ou cresce com a população?
  Se é conservado, quem detém a parcela de quem não crê em nada?
- Decaimento monotônico **até um piso** torna todo deus esquecido imortal em potência
  mínima. O piso é zero com coleta da entidade, ou o deus fica para sempre e barato?
- Natureza derivada da doutrina: é enum de cenário ou vetor contínuo sobre os sistemas que a
  potência modifica? A resposta decide se "deus da guerra" é rótulo ou efeito.
- Cisma cria uma **entidade-deus nova** ou duas doutrinas apontando para a mesma? Muda o
  esquema, a contagem do panteão e o teste de divergência de natureza.
- Milagre precisa de testemunha para virar relato. Milagre sem ninguém por perto aconteceu —
  e custou? Ou a rolagem sequer roda, e o motor economiza?

## Fora do escopo
Potência genérica: Fase 16. Operadores de distorção e cânone: Fase 10 (aqui só são
consumidos). Sermão, mito narrado e hagiografia em prosa: Fase 12. Culto que venera uma
linha temporal perdida: Fase 18. Culto de carga por contato: Fase 19.

## Ver também
[divinity-and-belief.md](../domain/divinity-and-belief.md) ·
[powers.md](../domain/powers.md) ·
[historical-memory.md](../domain/historical-memory.md) ·
[society.md](../domain/society.md) · [economy.md](../domain/economy.md) ·
[ADR-0007](../adr/ADR-0007-memoria-historica-degradavel.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
