# Fase 7 — Relações e famílias

**Objetivo**: fecha o **objetivo técnico #1** — 100 NPCs rodam 100 anos sem LLM, formando
casais, tendo filhos e produzindo linhagens rastreáveis que não colapsam em clones.

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
- **Diversidade contra controle de deriva neutra**: **coeficiente de variação** por atributo
  genético (variância bruta não é comparável entre populações de tamanhos diferentes),
  comparado ao mesmo CV no cenário de deriva neutra com a mesma seed. Falha só se a
  diversidade do mundo real ficar **abaixo** do controle neutro.
  *Registro: 100 anos são ~4 gerações; com `Ne` entre 20 e 50, a perda esperada por deriva
  é de 3,9% a 9,6%. Um limiar de "≥ 50% da variância inicial" passaria com o modelo
  genético completamente quebrado. O teste real é o contraste com o controle, não o nível.*
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
histórica: Fase 9. Qualquer participação de LLM: Fase 10.

## Ver também
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[npc.md](../domain/npc.md) · [society.md](../domain/society.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md)
