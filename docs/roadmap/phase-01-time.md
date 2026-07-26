# Fase 1 — Motor de tempo

**Objetivo**: o mundo avança sozinho, de forma determinística e reprodutível **entre
processos**, com sistemas registrados por frequência, eventos agendados no futuro e um
hash de mundo que serve de eval gate para todas as fases seguintes. Ainda sem NPC.

## Tasks
1. **`WorldDate` e calendário**: dia/mês/ano configuráveis por cenário; conversão
   tick ↔ data; aritmética de data (somar meses, comparar). Value object, imutável.
2. **`WorldClock` e loop de tick**: avança N ticks; ordem de execução dos sistemas dentro
   de um tick é **fixa e declarada**, não ordem de registro acidental.
3. **`ISimulationSystem` + `TickFrequency`** (`Hourly`, `Daily`, `Monthly`, `Yearly`):
   registro por frequência; um sistema só é chamado no tick da sua frequência.
4. **RNG semeado por stream**: `ctx.Rng(streamKey)` derivado da seed do mundo. Streams
   independentes — adicionar um sistema não desloca a sequência dos outros. Ver ADR-0005.
5. **Scheduler de eventos futuros**: fila de prioridade por tick alvo; agendar, cancelar,
   processar. Empate no mesmo tick desempata por ID, nunca por ordem de inserção.
6. **Controles**: pausa, retomar, velocidade (ticks por segundo real), avanço rápido de N
   ticks sem render. Nada disso é estado do mundo — é estado do hospedeiro.
7. **Snapshot + hash canônico vs volátil** (ADR-0006): serializar, reidratar, e **duas**
   funções de hash. O critério da classificação é o do ADR-0014: **canônico se alimenta uma
   decisão**; **volátil se é recomputável do canônico ou é cosmético sem efeito causal**; na
   dúvida, canônico. Todo campo é classificado numa das duas listas, e a classificação é
   rastreável até o caminho de decisão que lê o campo — ou até a ausência dele.
8. **Determinismo entre processos**: runner de teste que executa o mesmo cenário em dois
   processos separados. .NET randomiza hash de string por processo, então a ordem de
   iteração de `Dictionary<string,_>` difere — duas execuções no mesmo processo não pegam
   bug de ordenação.
9. **Golden hashes versionados**: `tests/golden/world-hashes.json` com
   `{cenário, seed, ticks, hash}`. Mudança legítima de regra quebra o arquivo e atualizar
   o baseline vira linha visível no diff, não silêncio.
10. **Teto de iterações internas do tick**: contador de passos por tick com limite
    declarado no cenário; estourar aborta com erro nomeando o sistema culpado.
11. **Sistema de exemplo** trivial (um contador por frequência) só para provar o
    agendamento e o determinismo. É descartável — some quando a Fase 3 chegar.

## Critérios de verificação
- `Sim(seed: 42).Run(3650)` em **dois processos separados** → hashes canônicos idênticos.
  Seeds diferentes → hashes diferentes (senão o hash não mede nada). Vale para todos os
  cenários do `world-hashes.json`, que é comparado no gate e só muda por commit explícito.
- **Reidratação sobrevive ao futuro**: snapshot no tick T, reidratar e rodar mais 500 ticks
  produz o mesmo hash canônico da run contínua até T+500. Round-trip de hash sozinho é
  tautológico quando o hash sai da própria serialização.
- **Cobertura do snapshot por reflexão**: todo campo público do estado do mundo aparece no
  snapshot serializado; campo novo não incluído reprova o teste.
- **Classificação de campos por reflexão**: o teste é gerado sobre a lista de campos, não
  escrito à mão. Mutar por reflexão **qualquer** campo canônico muda o hash canônico;
  mutar campo volátil não muda; campo não classificado em nenhuma das listas reprova.
- **A classificação obedece ao critério (ADR-0014)**: segunda asserção sobre a mesma lista —
  todo campo canônico é lido por **ao menos um** caminho de decisão, e **nenhum** campo
  volátil é lido por caminho de decisão. Campo que não satisfaz a própria classe reprova,
  mesmo que a asserção de mutação acima passe.
- **Streams independentes**: registrar um sistema que consome ativamente `ctx.Rng("streamB")`
  mantém o hash por-stream de `streamA` e `streamC` idêntico ao da run sem ele. O hash
  total muda — é isso que prova que o sistema novo entrou na conta.
- **Pausa não é estado do mundo**: rodar 37 ticks, pausar, mudar a velocidade, retomar e
  rodar mais 63 → mesmo hash canônico de 100 ticks diretos; e a reflexão sobre os campos
  serializados exige que velocidade e flag de pausa **não** estejam no snapshot.
- Um sistema `Yearly` roda exatamente **10 vezes** em 3650 ticks diários; um `Daily`, 3650.
- Evento agendado para o tick T é processado **no** tick T; dois eventos no mesmo tick
  processam em ordem de ID, estável entre os dois processos; cancelar impede a ocorrência.
- **Terminação do tick**: cenário adversarial com um sistema que se re-agenda para o mesmo
  tick é **abortado** com erro nomeando o sistema; nos cenários normais, nenhum tick passa
  de 50% do teto de iterações declarado (a folga é medida e vai para o baseline).

## Fora do escopo
NPC, cidade, economia, mapa. Persistência em banco — o snapshot desta fase pode ser em
memória/arquivo; EF Core entra na Fase 3.

## Ver também
[time-and-ticks.md](../domain/time-and-ticks.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[ADR-0006](../adr/ADR-0006-snapshot-mais-event-log.md) ·
[ADR-0014](../adr/ADR-0014-canonico-vs-volatil.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md)
