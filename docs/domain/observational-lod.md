# LOD observacional (rascunho de pesquisa — Fase 28 task 4)

O que muda quando o jogador olha para o **mundo**, para uma **cidade**, ou para dentro de um
**prédio**. Ortogonal à resolução agregado/detalhado/máximo de [simulation-lod.md](simulation-lod.md):
essa resolução decide **quanto** um NPC existe; esta decide **com que frequência recalcular**
um NPC que já é detalhado, dado o que a câmera do jogador (Fase 15) enquadra agora.

## Três escopos aninhados

```
mundo    -> jogador no mapa múndi
cidade   -> jogador dentro de uma cidade, olhando as ruas
interior -> jogador dentro de um prédio específico
```

Cada escopo enquadra uma região; tudo fora dela cai um nível.

## Princípio: LOD nunca pausa vida

Um NPC **detalhado** (já materializado, Fase 8) continua envelhecendo, correndo risco de
morte, arranjando e terminando relacionamento, tendo filho, sendo contratado ou demitido —
**igual, observado ou não**. Esses são eventos agendados pelo motor de decisão por evento da
Fase 9 (task 4): já custam O(decisões), não O(NPCs × tick), e esse custo já independe de
quem olha desde a Fase 9. LOD observacional **não é uma quarta resolução abaixo de
"detalhado"** — é só a decisão de que camada de **detalhe cosmético/inspecionável** por cima
dessa vida (que já está acontecendo) vale a pena pagar agora.

O que sempre roda, para todo NPC detalhado, veja alguém ou não:
- Envelhecimento, mortalidade, nascimento, casamento, separação, emprego/demissão — eventos
  agendados (Fase 9 task 4), mesmo custo de sempre.
- Necessidade numérica (fome/sono/energia) — decaimento fechado (Fase 9 task 5), lido sob
  demanda.
- A decisão macro em si (o utility AI escolhe a próxima ação) — é o mesmo motor da Fase 4,
  já barato; não existe versão "menos decidida" de um NPC.

O que só roda com custo extra quando alguma fonte observa o lugar:

| Camada extra | Sem nenhuma fonte observando | Com fonte observando |
|---|---|---|
| Posição exata / pathing passo a passo | Aproximada ("está em casa", "está no trabalho") | Recomputada com precisão de tile ao entrar em escopo |
| Reavaliação de micro-ação (qual cadeira, qual prateleira) | Não roda — irrelevante sem ninguém olhando | Roda |
| Interação social entre dois NPCs | Resolvida como evento único por mudança de macro-estado (ex.: "jantaram juntos") — **acontece igual**, só não em turno a turno | Turno a turno, com detalhe de diálogo se houver |
| Rastro de decisão (Fase 28 task 10) | Não grava | Grava |

**Fora de qualquer escopo** (cidade/região sem nenhuma fonte por perto) segue a resolução
**agregada** de `simulation-lod.md` — mas isso é sobre NPC **não-materializado**
(estatística), não sobre um detalhado que ninguém está olhando agora. Um detalhado nunca
volta a ser estatística só por falta de observação (isso já é regra de desmaterialização da
Fase 8/9, que tem critério próprio de relevância histórica, não de câmera).

## Recalcular ao entrar, não regredir ao sair

"Recalcular ao entrar" nunca significa "a vida do NPC começa agora" — a vida já rodou
inteira (seção acima). Significa só materializar a camada cosmética que estava aproximada:
posição exata (fórmula fechada sobre a última posição conhecida + rota), e a partir dali
ligar reavaliação de micro-ação e rastro. Sair desliga essas duas camadas de novo, sem parar
nenhum evento de vida — só o custo extra de "estar no holofote" some, o resto continua.

## Fonte de observação é plural desde o início

`simulation-lod.md` e a Fase 9 original atribuíam "quem observa" a incarnação de jogador
(Fase 25), como se fosse a única fonte possível. Não é: **lugar em detalhe é a união dos
lugares que qualquer fonte ativa está olhando agora**, e fonte é plural por desenho, mesmo
que hoje só exista uma.

```
lugares_em_detalhe(tick) = ⋃ escopo(fonte) para toda fonte ativa em tick
```

Hoje a única fonte é a câmera do cliente web (Fase 15) — um operador, um lugar. Depois da
Fase 25, jogadores incarnados são fontes adicionais: N jogadores, cada um com seu próprio
escopo mundo/cidade/interior, mais quem estiver de espectador na web. O motor não muda —
só cresce o conjunto de fontes que alimenta a união acima. Fase 25 **não redesenha** este
mecanismo, só registra mais um tipo de fonte nele.

**Em aberto pra Design**: com N fontes simultâneas, quantos lugares em detalhe o cenário
sustenta ao mesmo tempo antes de o custo total (Fase 9) estourar o teto? Hoje, com 1 fonte,
essa pergunta não aparece — vai aparecer assim que a Fase 25 adicionar a segunda.

## Resolvido no spec (`.specs/features/phase-28-cognition/spec.md`)
- **Tolerância de equivalência**: nenhuma — recompute é exato. Decaimento usa a fórmula fechada
  já determinística da Fase 9 task 5; decisão dependente de RNG usa stream sob demanda da Fase 9
  task 8, reproduzível byte-a-byte pela mesma seed. Não é aproximação, é a mesma garantia de
  determinismo que a Fase 9 já dá para o agregado, generalizada ao indivíduo fora de escopo.
- **Interação social LAZY**: um evento por mudança de macro-estado (ex.: "família jantou junta"),
  não por hora — mesmo princípio de custo O(decisões) da Fase 9 task 4.
- **Evento parcial** (incêndio/guerra numa parte da cidade, jogador dentro de um prédio):
  promove só os NPCs diretamente envolvidos, nunca a cidade inteira (registrado como assumption
  no spec — granularidade fina de "o que conta como envolvido" fica pra Design).

## Perguntas em aberto (a fechar na task 4 antes do código)
Nenhuma bloqueante restante — a task 4 aprofunda a tabela acima com números reais de custo, não
decisões de desenho novas.

## Ver também
[simulation-lod.md](simulation-lod.md) · [phase-09-scale.md](../roadmap/phase-09-scale.md) ·
[phase-28-cognition.md](../roadmap/phase-28-cognition.md) · [world-map.md](world-map.md) ·
[time-and-ticks.md](time-and-ticks.md)
