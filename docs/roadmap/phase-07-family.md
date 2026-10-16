# Fase 7 — Relações e famílias

**Objetivo**: fecha o **objetivo técnico #1** — 100 NPCs rodam 100 anos sem LLM, formando
casais, tendo filhos e produzindo linhagens rastreáveis que não colapsam em clones.

**FAM-32 Verified (2026-10-15)**: prova independente em `family-diversity-calibration/verification.md`; FAM-06/20/22 permanecem pendentes na spec.

## Tasks
1. **Relação com eixos numéricos**: confiança, afeto, respeito, dívida. Par ordenado
   (A→B) — relação não é simétrica. Evolui por evento (ajuda, traição, convívio,
   comércio), decai sem contato. Nada de "amizade" como flag booleana.
2. **Formação de relação**: proximidade física, convivência repetida, compatibilidade de
   personalidade, diferença de status e cultura. Quem nunca se encontra nunca se conhece.
3. **Atração e cortejo**: score de atração a partir de idade, saúde, status, habilidade,
   afinidade cultural e a relação já existente. Cortejo dura tempo e **pode ser rejeitado
   com motivo nomeado** (`Incesto`, `ForaDaFaixaEtária`, `SemAfinidade`) — motivo é dado.
4. **Casamento e household**: casar cria um household novo com moradia e estoque próprios;
   filhos entram nele; morte de ambos os pais dissolve e redistribui.
5. **Reprodução**: janela de fertilidade por idade e sexo, saúde, qualidade da relação e
   recursos do household. Concepção **agenda** o nascimento no scheduler (Fase 1) — não é
   varredura por tick. Gravidez pode falhar; parto tem risco para mãe e criança.
6. **Hereditariedade**: `atributoFilho = pesoPai*atributoPai + pesoMãe*atributoMãe +
   mutação + influênciaAmbiental`, com mutação do RNG semeado por `NpcId` do filho.
7. **Separar genético de ambiental**: GENÉTICO = físico, saúde, fertilidade, aparência,
   parte do potencial cognitivo, temperamento. AMBIENTAL = educação, crenças, cultura,
   idioma, valores, habilidades, traumas. Origens distintas no snapshot, nunca no mesmo
   campo — o teste de diversidade depende dessa separação.
8. **Seleção emergente**: quem sobrevive e se reproduz define o pool. **Sem** função de
   fitness artificial, sem "melhor NPC". Se aparecer um score global de aptidão, é bug.
9. **Controle de deriva neutra como cenário**: mesma demografia, acasalamento aleatório,
   seleção desligada, mesma seed. É o comparador do critério de diversidade — sem ele o
   número de diversidade não significa nada.
10. **Cenário contrafactual de household**: o mesmo genoma semeado em household rico e em
    household pobre, com as demais condições fixadas. É o experimento que falseia
    "gene é destino"; sem ele o aviso de design é só um comentário.

## Critérios de verificação
- **Linhagens derivadas do cenário, não chutadas**: `esperado = anosDeHorizonte /
  idadeMédiaPrimeiroParto`, lido em runtime. Assert `≥ floor(esperado / 2)` linhagens
  completas rastreáveis do último ano até um fundador do ano 0.
- **População final contra baseline**: distribuição de 20 seeds versionada em
  `tests/baselines/`, mesmo tratamento da Fase 3. Sem faixa literal — `10 ≤ N ≤ 1000`
  aceitava quase qualquer modelo quebrado.
- **Toda** criança tem `PaiId` e `MãeId` apontando para NPCs que existem e estavam vivos na
  concepção. Zero órfãos de referência, assert a cada tick em 10 anos no gate; 100 anos em
  nightly (`Category=Scenario`).
- Nenhum nascimento tem mãe fora da janela de fertilidade declarada no cenário.
- **Incesto, os dois lados**: zero casamentos entre parentes de primeiro grau em 10 anos
  (negativo) **e** cenário com dois irmãos adultos coabitando e compatíveis em tudo o mais,
  no qual o cortejo é rejeitado com motivo `Incesto` (positivo). Só o negativo passa também
  se irmãos nunca se encontrarem.
- **Diversidade calibrada contra deriva neutra (FAM-32, ADR-0036)**: em 20 seeds pareadas e
  10 anos, a média de `CV(Vitality real) − CV(Vitality neutro)` deve ficar na faixa
  `média baseline ± ConfidenceZ × EP baseline`, calculada de `tests/baselines/family-diversity.json`.
  `Openness`, sem seleção direta de mortalidade, é controle negativo: sua diferença média
  deve ficar na faixa medida e seu IC95 pareado deve conter zero.
  Em checkpoints anuais até 100 anos do cenário completo, o CV de Vitality e a população viva
  não podem ficar abaixo dos mínimos empíricos das 20 seeds versionadas; o baseline exige
  CV positivo e mais de um vivo. Seleção de mortalidade amplificada deve reprovar a mesma faixa.
  *A seleção direcional pode reduzir variância; o critério antigo contra zero foi substituído
  por decisão explícita do usuário (#65). Preservar a cidade ao nascer e casar corrige o
  bloqueio de emprego da geração seguinte; contato no turno e parceiro compatível são causais.*
- **Correlação genética × sucesso, teto derivado**: bootstrap de `|r|` sobre 20 seeds. Exige
  (a) `r` significativo, `p < 0.05`, e (b) o IC95 de `|r|` **inteiramente abaixo** do `|r|`
  medido no mesmo mundo com o canal ambiental **desligado**. Sem `0 < |r|` (tautológico) e
  sem teto inventado.
- **Ambiente é causal, medido contra o canal genético**: 20 pares mesma-genética /
  seeds-ambientais-diferentes. A distância entre as distribuições de riqueza precisa ser
  **≥** a distância observada em 20 pares mesma-ambiental / genéticas-diferentes.
- **Contrafactual de household**: mesmo genoma em household rico vs pobre, 40 anos, 20
  seeds. (a) as medianas de resultado **diferem** — se fossem iguais, o berço não faria
  nada; (b) as distribuições **se sobrepõem**, com sobreposição `≥` a medida entre dois
  genomas extremos no mesmo household — nem gene nem berço decidem sozinhos. Ambas as
  estatísticas vão para baseline.
- **A Fase 7 entrou na conta**: desligar hereditariedade e formação de casais por flag de
  teste muda `Hash(world)` após 10 anos.

## Fora do escopo
Migração entre assentamentos, LOD e crescimento urbano: Fase 8. Dinastias e memória
histórica: Fase 10. Qualquer participação de LLM: Fase 11.

## Ver também
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[npc.md](../domain/npc.md) · [society.md](../domain/society.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md)
