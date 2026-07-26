# Fase 15 — Potência

**Objetivo**: mutante, mago, portador de artefato e implantado deixam de ser cinco
subsistemas e viram **um modificador declarado** sobre sistemas que já existem, com fonte,
efeito, custo, probabilidade, modo de falha e consequência social. O motor continua
conservando dinheiro e recursos com poderes ligados.

> **Spec, não gate.** Os critérios abaixo são a intenção; os critérios finais são escritos
> sob `rules/eval-criteria.md` quando a fase for ativada. Não comece esta fase antes da
> Fase 8 fechar.

## Tasks
1. **Descritor de poder como dado de cenário**, com os seis eixos de `powers.md`. Nenhum
   poder em `enum` de código — mesma regra que a Fase 3 impôs a profissão e recurso.
2. **Declaração explícita de alvo**: todo efeito nomeia o sistema que modifica (mortalidade,
   produção, relação, aprendizado, deslocamento) e a grandeza. O registro é consultável por
   reflexão — é ele que sustenta o primeiro critério.
3. **Invocação como rolagem no RNG semeado**, stream do portador (ADR-0005):
   `chance = capacidade(portador, poder) − dificuldade(efeito, alvo, contexto)`, resultado
   em sucesso / parcial / falha.
4. **Custo cobrado no uso, não no sucesso**. Moedas: fadiga, saúde, longevidade, sanidade,
   recurso raro consumido, dívida com uma entidade, atenção hostil.
5. **Modos de falha com consequência**: efeito parcial, alvo errado, custo sem resultado,
   exposição pública, dano permanente ao portador. Falha nunca é no-op.
6. **Predisposição herdável como multiplicador de taxa**, reusando exatamente o mecanismo da
   Fase 6. Habilidade de poder **nunca** é copiada no nascimento.
7. **Consequência social vinda da cultura** (`society.md`): religiosidade, abertura,
   valorização da magia e autoritarismo decidem entre medo, culto, perseguição e
   recrutamento. O poder não carrega a reação.
8. **Escassez como parâmetro de cenário** (prevalência de portadores por população). Sem
   gate: é balanceamento, não arquitetura (ADR-0010).
9. **Portador na política de materialização do LOD** (Fase 8) como papel. No agregado, a
   região reporta contagem de portadores conhecidos, sem nome.
10. **Cenário `test-powers` pareado**: o mesmo mundo com e sem potência, para servir de braço
    de controle a todos os critérios causais.

## Critérios de verificação
- **Nenhum efeito fora do declarado**: run instrumentado registra toda mutação de sistema
  atribuída a uma invocação; o teste enumera por reflexão os sistemas alcançáveis e reprova
  se alguma mutação não estiver no descritor do poder **ou** se algum efeito declarado ficar
  sem cobertura no cenário. Só a primeira metade passa com o poder desligado.
- **Falha cobra o mesmo custo que o sucesso**: par na mesma seed, tratamento = rolagem
  forçada a falhar por flag de teste. O débito é idêntico nos dois braços e o braço de falha
  não recebe o efeito. 10/10 seeds.
- **Conservação sobrevive a poderes** — o critério que protege o motor: com potência ligada,
  massa monetária e estoque só mudam por transação, cunhagem ou destruição registrada,
  assert **a cada tick** em 10 anos no gate; 100 anos em nightly. Nenhum poder cria valor
  fora da lista de campos monotônicos da Fase 3.
- **Habilidade não atravessa o nascimento, predisposição sim** — o par de correlações da
  Fase 6, e um sem o outro não prova nada: em 200 nascimentos de portadores, o IC95 de
  `poder(pai) ↔ poder(filho ao nascer)` **contém 0**, e o IC95 de
  `predisposição(pai) ↔ predisposição(filho)` está **inteiramente acima de 0**.
- **A cultura decide a reação, não o poder**: mesmo poder, mesmo uso, mesma seed, duas
  culturas do cenário com religiosidade oposta → reações de sinal contrário em 10/10 seeds.
- **Potência entrou na conta**: desligar o sistema de potência por flag muda o hash canônico
  em 10 anos.

## Questões em aberto
- O eixo **tempo** de `powers.md` não tem sistema para modificar antes da Fase 17. Fica
  bloqueado até lá, ou entra como modificador de duração sobre o scheduler da Fase 1?
- Custo em **longevidade** colide com a morte agendada da Fase 3: reagendar o tick de óbito
  é mutação do scheduler ou um evento compensatório novo, à moda da Fase 9?
- A rolagem consome o stream do portador; portador **não materializado** não tem stream
  estável. Materializa na invocação, ou a região ganha stream próprio?
- "Atenção de algo que era melhor não ter notado você" é custo ou modo de falha? Se a coisa
  é uma entidade, ela é da Fase 16 e a dependência entre as duas fases inverte.
- Prevalência é parâmetro sem gate. Que sensor barato avisa que o cenário virou "todo mundo
  voa", já que o próprio ADR-0010 recusa transformar escassez em critério?

## Fora do escopo
Divindade e economia de crença: Fase 16. Efeito de poder sobre linha temporal e artefato que
ramifica: Fase 17. Fontes alienígenas e tecnologia que chega por contato: Fase 18. Prosa
sobre o extraordinário: Fase 11. Balanceamento de escassez não tem gate (ADR-0010).

## Ver também
[powers.md](../domain/powers.md) · [npc.md](../domain/npc.md) ·
[society.md](../domain/society.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[simulation-lod.md](../domain/simulation-lod.md) ·
[ADR-0010](../adr/ADR-0010-potencia-como-modificador-unificado.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md)
