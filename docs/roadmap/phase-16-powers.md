# Fase 16 — Potência

**Objetivo**: mutante, mago, portador de artefato e implantado deixam de ser cinco
subsistemas e viram **um modificador declarado** sobre sistemas que já existem, com fonte,
efeito, custo, probabilidade, modo de falha e consequência social. O motor continua
conservando dinheiro e recursos com poderes ligados.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 8 fechar.

## Tasks
1. **`Extraordinary.Enabled` por mundo**: desligado, zero portadores, zero aquisição, zero
   manifestação, zero sistema de potência no caminho quente, resto do mundo intacto.
2. **Descritor de poder como dado de cenário**, com os eixos de `powers.md` (fonte, efeito,
   modo, custo opcional, confiabilidade, modo de falha condicional, vulnerabilidade opcional,
   assinatura). Nenhum poder em `enum` de código — mesma regra que a Fase 3 impôs a profissão
   e recurso. Alvo do efeito (mortalidade, produção, relação, aprendizado, deslocamento) e
   grandeza são consultáveis por reflexão — sustenta o primeiro critério.
3. **Modo do efeito** (`Passive`/`Active`/`Triggered`/`Conditional`) decide quando ele está
   disponível, independente de custo ou rolagem.
4. **Confiabilidade por poder**: `Guaranteed` executa determinístico sem RNG de resolução;
   `ResolutionCheck` usa o primitivo do ADR-0011 no stream do portador (ADR-0005):
   `chance = capacidade(portador, poder) − dificuldade(efeito, alvo, contexto)`, resultado em
   sucesso/parcial/falha. Um poder pode legitimamente não ter `ResolutionCheck`.
5. **Custo opcional, cobrado no uso quando existe**, nunca no sucesso. Moedas: fadiga, saúde,
   longevidade, sanidade, recurso raro consumido, dívida com uma entidade, atenção hostil.
   `Costs = []` é um poder válido. Modos de falha só quando há `ResolutionCheck` (efeito
   parcial, alvo errado, custo sem resultado, exposição, dano permanente ao portador) — falha
   nunca é no-op, e um poder `Guaranteed` sem modo de falha ainda pode ter consequência via
   assinatura observável.
6. **Aquisição de potência como `PowerAcquisitionRule` declarativa**: elegibilidade, gatilho
   (nascimento, quase-morte, trauma, item, ritual, exposição), progressão/concessão,
   permanência, tudo determinístico com cadeia causal no event log — nunca
   `RandomlyGivePower(npc)`. Desenvolvimento gradual opcional (`Dormant → Manifesting →
   Developing → Stable → Mastered`, estágios de cenário, não enum universal). Possuir não
   implica saber: registro do efeito no NPC é separado da crença dele e de terceiros (Fase 10).
7. **Manifestação em estado (transformação) opcional**: `ManifestationStateDescriptor` com
   condições de entrada/saída e modificadores. Super-humano permanente roda com
   `RequiredState = none`.
8. **Vulnerabilidade intrínseca opcional, distinta de contramedida**: quando existe, é dado
   do fenômeno desde a origem; contramedida é criada/descoberta depois e não altera o
   descritor original (aprofundado na Fase 24).
9. **Predisposição herdável como multiplicador de taxa**, reusando exatamente o mecanismo da
   Fase 6. Habilidade de poder **nunca** é copiada no nascimento.
10. **Consequência social vinda da cultura** (`society.md`): religiosidade, abertura,
    valorização da magia e autoritarismo decidem entre medo, culto, perseguição e
    recrutamento. O poder não carrega a reação, e "herói"/"vilão" não são campos do NPC —
    são interpretação, aprofundada na Fase 23.
11. **Escassez como parâmetro de cenário** (zero a cotidiano), sem gate — balanceamento, não
    arquitetura (ADR-0010). Portador entra na materialização do LOD (Fase 8) como papel: no
    agregado, a região reporta contagem de portadores conhecidos, sem nome.
12. **Cenário `test-powers` pareado**: o mesmo mundo com e sem potência, braço de controle
    dos critérios causais. Inclui `test-extraordinary-disabled` (mundo inteiro sem o
    fenômeno) e variante de poder sem custo/sem rolagem/sem fraqueza.

## Critérios de verificação
- **Nenhum efeito fora do declarado**: run instrumentado registra toda mutação de sistema
  atribuída a uma invocação; reprova se alguma mutação não estiver no descritor **ou** se
  algum efeito declarado ficar sem cobertura no cenário. Só a primeira metade passa com o
  poder desligado.
- **Falha cobra o mesmo custo do sucesso, quando há custo e `ResolutionCheck`**: par na mesma
  seed, rolagem forçada a falhar. Débito idêntico nos dois braços, 10/10 seeds. Cenário
  pareado prova o oposto: `Guaranteed` com `Costs = []` nunca debita e nunca rola, 10 anos.
- **`Extraordinary.Enabled = false` zera o sistema**: zero portadores, zero eventos de
  aquisição/manifestação, sistema ausente da lista executada por tick (inspeção, não
  cronômetro).
- **Conservação sobrevive a poderes**: com potência ligada, massa monetária e estoque só
  mudam por transação, cunhagem ou destruição registrada, assert a cada tick em 10 anos;
  100 em nightly. Nenhum poder cria valor fora dos campos monotônicos da Fase 3.
- **Habilidade não atravessa o nascimento, predisposição sim**: em 200 nascimentos de
  portadores, IC95 de `poder(pai) ↔ poder(filho)` **contém 0**; IC95 de
  `predisposição(pai) ↔ predisposição(filho)` **acima de 0**.
- **A cultura decide a reação, não o poder**: mesmo poder, mesma seed, duas culturas com
  religiosidade oposta → reações de sinal contrário em 10/10 seeds.
- **Potência entrou na conta**: desligar o sistema por flag muda o hash canônico em 10 anos.

## Questões em aberto
- O eixo **tempo** não tem sistema para modificar antes da Fase 18: bloqueado até lá, ou
  modificador de duração sobre o scheduler da Fase 1?
- Custo em **longevidade** colide com a morte agendada da Fase 3: reagenda o tick de óbito
  ou é evento compensatório novo (Fase 10)?
- A rolagem consome o stream do portador; portador **não materializado** não tem stream
  estável — materializa na invocação, ou a região ganha stream próprio?
- "Atenção de algo que era melhor não ter notado você" é custo ou modo de falha? Se a coisa
  é entidade, é Fase 17 e a dependência entre fases inverte.
- Prevalência não tem gate. Que sensor barato avisa "todo mundo voa" sem virar critério?

## Fora do escopo
Divindade e economia de crença: Fase 17. Poder sobre linha temporal/artefato ramificado:
Fase 18. Fontes alienígenas e tecnologia por contato: Fase 19. Prosa: Fase 12. Balanceamento
de escassez não tem gate (ADR-0010).

## Ver também
[powers.md](../domain/powers.md) · [npc.md](../domain/npc.md) ·
[society.md](../domain/society.md) · [genetics-and-family.md](../domain/genetics-and-family.md) ·
[simulation-lod.md](../domain/simulation-lod.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
