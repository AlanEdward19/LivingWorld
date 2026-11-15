# Poderes por genética (AD-156)

No mundo tudo emerge da simulação; a exceção são os poderes, e mesmo eles vêm **por genética**, como seria na vida real se
existissem. Índice: [powers.md](powers.md) (o modelo do poder) → este arquivo (como ele passa de pais a filhos).

## Genótipo

- **Um locus por poder, dois alelos por pessoa** (materno e paterno), guardados em 2 bits por locus (`Npc.PowerGenome`, até
  32 loci; o poligênico ocupa vários). Descritor que não cabe nos 32 loci não é herdável (só evento ou autoria).
- **Fundador** (população inicial, materializado do pool, instanciado: quem não nasceu de concepção simulada): alelos sorteados da semente
  pela frequência do cenário, cada um independente, lidos na hora (equilíbrio de **Hardy-Weinberg**: p², 2pq, q²). Nada vai ao
  snapshot; `PowerGenome = 0` some do JSON.
- **Nascido no mundo**: na concepção, cada pai passa um dos seus dois alelos com 50% (1ª lei de Mendel), loci independentes
  (2ª lei), stream `power-genome` por bebê. O pai passa o seu único X à filha e o Y ao filho. Pai desconhecido dá um alelo da
  população. **Mutação nova**: o alelo sem o poder vira alelo do poder com a taxa do poder, por locus e por gameta.
- **Poder adquirido por evento ou autoria não passa ao filho**: só alelos passam. Por isso não há acúmulo: o filho só tem o
  que os alelos herdados dão (os três caminhos antigos "ambos / um só / mistura", que somavam listas, saíram).

## Modo de herança (dado do poder, `PowerGenetics.Mode`)

| Modo | Manifesta com | Observação |
|---|---|---|
| `Dominant` | uma cópia | heterozigoto × não portador: metade dos filhos |
| `Recessive` | duas cópias | o heterozigoto transmite sem manifestar; o traço pula geração |
| `XLinkedRecessive` | homem: uma; mulher: duas | pai afetado não passa a filho homem; passa a todas as filhas |
| `XLinkedDominant` | uma cópia | idem quanto ao pai |
| `Polygenic` | soma das cópias de N loci + desvio do ambiente ≥ limiar | modelo de liability |

## Do genótipo ao poder

- **Penetrância**: quem tem o genótipo manifesta com a chance do poder (sorteio fixo da pessoa, stream `power-phenotype`).
- **Expressividade**: intensidade contínua, 1 + dose a mais (homozigoto dominante 1,25; poligênico 0,25 por ponto acima do
  limiar) + desvio N(0; `ExpressivityStdDev`), entre 0,25 e 2. Consumida na rolagem do poder (`ResolutionCheck`): multiplica a
  capacidade do portador. Poder sem genótipo fica em 1.
- **Idade de início**: curva da puberdade, N(13; 1,5) anos por padrão, sorteio fixo. O nascimento agenda o evento `onset`
  (`ExtraordinaryStateSystem`); nada é varrido por tick. O fundador que já passou da idade manifesta na carga.
- **Gatilho do ambiente**: as regras de aquisição por evento continuam; com `TriggerRequiresGenotype` o gatilho só desperta
  quem tem o genótipo (inclusive quem a penetrância deixou dormente).

## Custo e seleção

Cada poder que a pessoa tem multiplica, mesmo sem uso: o gasto de fome (`HungerMultiplier`, padrão 1,1; passa pela lactação)
e de sede (`ThirstMultiplier`, 1,1); a fertilidade na concepção (`FertilityMultiplier`, 0,9); o risco de morte
(`HazardMultiplier`, 1,0). É por aí que a seleção natural opera sobre a frequência do alelo.

## Cenário

- `Extraordinary.Prevalence` vira a fração de manifestos no começo, repartida entre os poderes herdáveis; a frequência do
  alelo sai dela por Hardy-Weinberg, já descontada a penetrância (poligênico sem frequência declarada: q = 0,25).
- `Extraordinary.Genetics` (padrão do cenário) e `Descriptors[].Genetics` (do poder), opcionais; campo omitido herda o padrão.
  Sem nenhum dos dois, a regra fica nula no mundo (`InheritanceRules`), como antes.
- Com `Extraordinary.Enabled = false` nada disso roda: nenhum stream novo, nenhum campo novo no snapshot.

## Medida

`LW_POWERS=1 bash scripts/demography-report.sh` (cenário de referência: dominante, recessivo, ligado ao X e poligênico, 8%
de prevalência) imprime manifestos por mil, poderes por portador e frequência do alelo a cada 25 anos;
`LW_PROFILE_POWERS=1 bash scripts/profile.sh` mede o custo. Números do AD-156 em [decisions-log](../decisions-log.md).

## Fora (backlog 06, sobras P1–P6)

Ligação entre loci, mesmo motor das doenças genéticas, membros do pool agregado (só autossômicos, sem custo de fome),
poder novo por mutação de efeito (`MixDescriptorBuilder` sem consumidor), cultura e casamento por poder.
