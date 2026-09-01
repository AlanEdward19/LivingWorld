# Fase 28 — Cognição e LOD observacional

**Status**: fechada (2026-09-01). Gate `verify.sh` verde; critérios cobertos por teste.

**Objetivo**: (a) o utility AI da Fase 4 vira **inspecionável** — cada decisão relevante
grava estímulo, ponderação e escolha como dado navegável, com painel web "ver o cérebro" e
sandbox isolado para injetar traço/estímulo sintético; (b) o custo disso e da população em
geral cai via **compressão de estado frio** e **LOD por três escopos de observação real**
(mundo/cidade/interior), não mais um NPC sempre no mesmo nível dentro da cidade observada.
Critérios finais: `rules/eval-criteria.md`.

## Tasks — Compressão
1. Log de eventos frios (Fase 9 task 7) ganha codificação delta/dicionário em vez de
   texto/JSON puro; medir bytes/NPC/ano pós-compactação contra o teto da Fase 9.
2. Snapshot fora da janela recente vira referência + diff, não cópia integral (mesmo padrão
   delta da Fase 9 task 6, aplicado ao histórico, não só ao tick atual).
3. Interning de string/traço compartilhado (profissão, tag de evento, traço de
   personalidade) — deduplicado globalmente, não por NPC.

## Tasks — LOD observacional
4. **Pesquisa bloqueante, primeira do cluster**: para cada par (escopo de observação,
   sistema de simulação) decidir SEMPRE-TICK / LAZY-RECOMPUTE / SÓ-AGREGADO; publicar tabela
   em `docs/domain/observational-lod.md` antes de qualquer código desta fase.
5. Três escopos aninhados, não dois: **mundo** (só cidades/regiões visíveis saem do
   agregado), **cidade** (NPCs e prédios visíveis em detalhe pleno — o resto da cidade, não),
   **interior** (um prédio específico em detalhe máximo). Lugar em detalhe é a **união dos
   escopos de toda fonte de observação ativa** — hoje só a câmera do cliente (Fase 15); a
   Fase 25 acrescenta cada jogador incarnado como fonte adicional, sem mudar o mecanismo.
   Ortogonal à resolução agregado/detalhado/máximo de `simulation-lod.md`.
6. LOD nunca pausa vida: envelhecimento, mortalidade, nascimento, casamento, separação e
   emprego/demissão continuam via evento agendado (Fase 9 task 4) **igual, observado ou
   não** — LOD desta fase só liga/desliga posição exata, micro-ação e rastro, nunca a vida
   em si. Ver tabela em `observational-lod.md`.
7. Recalcular ao entrar: só a camada cosmética (posição exata) é recomputada por fórmula
   fechada ao ganhar observação; ao perder, desliga de novo — sem manter as duas camadas
   ativas ao mesmo tempo e sem tocar nenhum evento de vida.
8. Sensor de custo por escopo: gate mede µs/NPC-tick da camada extra (posição/rastro) para
   observado vs não-observado; teto por cenário (formato da Fase 9 task 10) — o custo da
   vida em si (task 6) já está fora dessa conta desde a Fase 9.

## Tasks — Motor de cognição
9. Pipeline estímulo → avaliação (necessidade/traço/memória/contexto social) → decisão vira
   dado estruturado por decisão, não só o resultado — estende o utility AI de `behavior.md`,
   não o substitui.
10. Rastro grava necessidade dominante, traço aplicado, memória consultada e opção
    descartada — log estruturado, sob o mesmo teto de bytes/NPC da Fase 9.
11. Painel web "ver o cérebro": NPC detalhado expõe o rastro em dados (tabela/timeline) e
    visual (fluxo estímulo→decisão); consome o rastro da task 10, não recalcula.
12. Sandbox de decisão isolado: injeta estímulo/traço sintético no mesmo motor da task 9 sem
    tocar o mundo principal — ferramenta de autoria/pesquisa, fora do relógio do mundo.
13. Rastro auditável só grava em NPC no escopo "cidade"/"interior" observado — agregado e
    aproximado não gravam (reaproveita LOD das tasks 5–7; o motor não roda além do que já
    rodaria sem esta fase).

## Critérios de verificação
- Nenhum evento de vida (morte, nascimento, casamento, separação, emprego/demissão) atrasa
  ou deixa de ocorrer por falta de observação — mesma taxa/tempo com e sem fonte olhando,
  10/10 seeds.
- Sensor de escala (Fase 9) dentro do teto com rastro ligado, cenário com N% observado; o
  custo medido é só da camada extra (posição/micro-ação/rastro), nunca da vida em si.
- Custo da camada extra em NPC não-observado ≤ fração declarada do custo do NPC observado
  (número definido pela pesquisa da task 4).
- Recalcular ao entrar produz posição/estado byte-idêntico ao de simulação contínua — sem
  tolerância, é recompute exato por fórmula fechada + RNG sob demanda (Fase 9 tasks 5/8).
- Painel reflete exatamente o rastro gravado, sem inferência no cliente.
- Compressão mantém round-trip (descomprimir → mundo idêntico) e reduz bytes/NPC/ano contra
  a linha de base da Fase 9.
- Sandbox isolado: escrita não vaza para o estado do mundo principal (teste de isolamento).

## Fora do escopo
Traço/emoção novo (Fase 21+). Fatores biológicos indiretos (metabolismo, fadiga, nutrição)
— extensão futura desta fase ou fase própria, registrar em `STATE.md` se ativado. Qualquer
efeito extraordinário (16–20), viagem temporal (18). Incarnação de jogador em si (Fase 25) —
esta fase só garante que o mecanismo de fontes já suporte mais de uma; implementar o jogador
como fonte concreta é da 25.

## Ver também
[behavior.md](../domain/behavior.md) · [simulation-lod.md](../domain/simulation-lod.md) ·
[observational-lod.md](../domain/observational-lod.md) · [cognition.md](../domain/cognition.md) ·
[phase-04-needs.md](phase-04-needs.md) · [phase-09-scale.md](phase-09-scale.md) ·
[phase-15-map-visual.md](phase-15-map-visual.md) · [phase-16-powers.md](phase-16-powers.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
