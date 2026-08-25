# Fase 16.1 Context

**Gathered:** 2026-08-24
**Spec:** `.specs/features/phase-16-1-power-engine/spec.md`
**Status:** Ready for design (sem decisões em aberto — ver spec, Assumptions)

---

## Feature Boundary

Motor de poder genérico e determinístico: **qualquer poder é descritível como dado**
(`PowerDescriptor`), sempre ancorado numa mecânica real da simulação (existente ou construída
nesta fase), nunca como ação roteirizada solta — sem exceção, exceto poderes de tempo/viagem
no tempo (Fase 18). P1 = registro de mecânicas + primitiva de transferência. P2 = TODAS as
mecânicas/conceitos de base que faltam: senescência/imortalidade, sorte determinística,
leitura/alteração de mente, transferência de anos de vida, transmutação de matéria, Força/
Percepção/Combate completos (limite de carga, velocidade de coleta/construção, alcance de
percepção, reação/decisão mais rápida, combate NPC-vs-NPC), **gravidade pessoal (recria voo/
velocidade existentes a partir de um conceito real, não chave especial)**, **temperatura/
clima local**, **fauna (entidade animal mínima)** e **memória/cognição privada (consulta
filtrada ao log de fatos)**. P3 = Power Evolution (documentado, não implementado).

---

## Implementation Decisions

### Escopo do MVP (P1)

- Motor genérico + amostra representativa (~15-20 poderes cobrindo as categorias da lista),
  não os ~300 poderes individualmente rastreados como requisito — usuário confirmou
  explicitamente que rastrear cada um infla a spec sem valor proporcional.

### Arquitetura: registro C# vs. DSL/script

- Registro C# tipado (`IExtraordinaryMechanic` por namespace de efeito/custo). Usuário
  recomendou e confirmou — preserva o determinismo/tipagem que `ADR-0005` já exige em todo o
  motor; um interpretador de script pra poder é projeto à parte, com risco de determinismo
  maior (haveria que provar que o interpretador em si nunca introduz não-determinismo).
- Adicionar mecânica nova = registrar 1 classe nova. Adicionar poder novo (dentro de
  mecânicas já registradas) = só dado de cenário, zero C#.

### Power Evolution (árvore de evolução + mistura genética)

- Entra nesta spec como P3, documentado com ACs descritivas (não testadas/implementadas
  nesta fase) — usuário confirmou explicitamente. Design deve deixar o modelo de dados
  (`PowerDescriptor`/`ExtraordinaryCarrierState`) aberto o bastante pra não precisar quebrar
  contrato quando isso for retomado, mas não construir a feature agora.

### Gaps de mecânica ausente — tratamento

- Usuário foi explícito: "quero que todos os gaps sejam anotados nesta spec, de forma que é
  necessário implementar cada uma delas antes de finalizar essa fase." Isso mudou o escopo de
  "documentar gap" pra "vira requisito P2 com AC real" pras mecânicas que são de fato
  fecháveis dentro do motor de poder (senescência, sorte, mente, transferência de vida).
- **Três pontos precisaram de decisão adicional do usuário** (resolvidos na mesma sessão, ver
  spec Assumptions):
  1. **Tempo forte** (parar o tempo, rebobinar, loop temporal) — confirmado que fica FORA da
     16.1, endereçado na Fase 18 (Timelines). **Única exclusão real da fase.** O que é
     fechável agora (senescência/envelhecimento, cadência de decisão) já é P2 (`PWR-20..23`,
     `PWR-59..61`).
  2. **Transmutação/duplicação de matéria** — confirmado: entra como P2 (`PWR-35..38`), só
     através do canal já auditado de cunhagem (`WorldEventKind.Minted`/`Destroyed`), nunca
     criando valor invisível — preserva a garantia de conservação da Fase 16 original.
  3. **Força/Percepção/Combate** — o agente inicialmente tratou isso como backlog separado
     (sequência já combinada numa conversa anterior). **Usuário corrigiu explicitamente**:
     "TUDO que é necessário para poderes entra nesta fase sem exceção" — essas 5 mecânicas
     (carga, coleta/construção, percepção, reação, combate) viraram P2 desta spec
     (`PWR-50..65`), na mesma ordem de construção já combinada antes (carga → coleta →
     percepção → reação → combate por último, por ser o mais sensível).
  4. **Recriação de voo/velocidade + conceitos fundamentais ausentes** — o agente generalizou
     o motor de efeito/custo mas deixou `movement.flight`/`movement.speed-multiplier` como
     duas últimas chaves especiais, e não listou os conceitos de DOMÍNIO (distintos de
     mecânica de poder) que a lista exige e a simulação não tem. **Usuário apontou os dois
     buracos explicitamente.** Corrigido: voo/velocidade agora são casos de uma mecânica de
     `gravity.self` real (`PWR-70..73`, com compatibilidade retroativa pros dados já salvos);
     e uma seção própria "Conceitos Fundamentais Necessários" lista gravidade, temperatura/
     clima local (`PWR-74..76`), fauna (`PWR-77..79`) e memória/cognição privada
     (`PWR-80..83`) como pré-requisitos de domínio, cada um escopado no menor tamanho que já
     desbloqueia os poderes dependentes (nunca reescrita de um sistema gigante do zero).
  5. **Auditoria própria do agente contra a lista inteira de ~300 poderes** — usuário pediu
     "falta algo que ajudaria a recriar aqueles 300 poderes?"; o agente varreu a lista
     inteira categoria por categoria e achou 12 lacunas reais não cobertas ainda: seletor de
     alvo por área/região (`PWR-06..09`, virou primitiva P1 — várias histórias P2 já
     assumiam "raio" informalmente sem essa primitiva existir), ciclo de poder passivo/
     contínuo (`PWR-90..92`), vulnerabilidade/resistência mecânica (`PWR-93..95`), skill
     como efeito de poder (`PWR-96..98`), fertilidade modificável (`PWR-99..100`), Flora
     (`PWR-101..103`, par de Fauna), instanciação de NPC — clone/divisão/reencarnação
     (`PWR-104..107`), identidade/controle prolongado — possessão/troca de corpo/metamorfismo
     (`PWR-108..111`), vínculo/pacto duradouro (`PWR-112..114`), alma/fantasma pós-morte
     (`PWR-115..116`), espaço dimensional/portal (`PWR-117..119`), precognição probabilística
     (`PWR-120..122`). **Usuário respondeu "adicione tudo"** — todas as 12 entraram como P2
     com AC testável, não só documentadas como gap.

### Agent's Discretion

- Nomenclatura exata dos tokens de mecânica nova (`transfer.<atributo>`, `luck.<chave>`,
  `mind.<chave>`) — segue o padrão já em uso (`npc.<stat>`, `carrier.<stat>`,
  `movement.<eixo>`), livre pra ajustar durante Design se um nome já registrado colidir.
- Onde exatamente o estado de "traço de personalidade pré-alteração" (mente, PWR-30) vive —
  Design decide entre um campo novo em `ExtraordinaryCarrierState` (preferencial, menor
  blast radius) vs. em `Npc` — spec já registra a preferência mas não fecha o tipo exato.

### Declined / Undiscussed Gray Areas → Assumptions

Uma: se "precognição sem viagem no tempo" conta como parte da exclusão de tempo forte (Fase
18) ou é mecânica própria desta fase. O agente perguntou isso especificamente antes; quando
o usuário respondeu "adicione tudo" ao lote de 12 gaps, essa ambiguidade pontual não foi
resolvida explicitamente — o agente assumiu que NÃO é tempo (é leitura probabilística, nunca
muta/rebobina o mundo) e registrou isso como suposição não confirmada na spec (ver
Assumptions & Open Questions) em vez de decidir silenciosamente. Todo o resto (4 perguntas
iniciais + 2 de acompanhamento + a correção de escopo de Força/Percepção/Combate + a
auditoria de 12 gaps) foi respondido diretamente pelo usuário nesta mesma sessão.

---

## Specific References

- `ADR-0010-potencia-como-modificador-unificado.md` é citado como fundação filosófica direta
  — o usuário está pedindo, na prática, que a implementação finalmente cumpra o que esse ADR
  já prometia ("poder é modificador sobre um sistema que já existe").
- `ExtraordinaryLocomotion.cs` (padrão "resolve do zero a cada tick, sem estado gravado no
  Npc") é citado como o modelo de referência pra generalizar — já é exatamente o padrão que o
  usuário descreveu quando explicou "voar = manipular gravidade ao redor".
- Lista de ~300 poderes fornecida pelo usuário nesta conversa (categorias: imortalidade,
  precognição, psíquico/mental, social/carisma, genético/hereditário, teletransporte/
  velocidade/força, clima/fertilidade, sorte/probabilidade, clonagem/metamorfose, verdade/
  mentira, animais/plantas, matéria/transmutação, energia, gravidade, magnetismo,
  tecnológico, morte/necromancia, conceitos abstratos, "quebra de regra da realidade",
  fraquezas específicas, Power Evolution) — vive só na conversa, não copiada pro repo (spec
  referencia categorias e exemplos, não a lista bruta).

---

## Deferred Ideas

- **Power Evolution completo** (implementação, não só documentação) — vira spec própria
  (16.2 ou posterior) quando o usuário priorizar.
- **Poderes de tempo/viagem no tempo** — única categoria realmente fora desta fase, fica pra
  Fase 18 (Timelines).
