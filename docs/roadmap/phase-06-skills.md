# Fase 6 — Habilidades e aprendizado

**Objetivo**: NPCs deixam de ser intercambiáveis — cada um acumula habilidades por
prática e ensino, e isso muda o que produz, quanto ganha e que profissão escolhe.

## Tasks
1. **Conjunto de habilidades**: agricultura, caça, comércio, construção, medicina,
   combate, ensino, artesanato, política, liderança, pesquisa, tecnologia, magia.
   Valor numérico com piso 0 e teto declarado no cenário. Sem habilidade oculta.
2. **Curva de retornos decrescentes**: função pura, sem estado de mundo, parametrizada no
   cenário — uma só curva, não uma por habilidade. Testável isolada, sem simulação.
3. **Fontes de ganho**: prática no trabalho (a principal), treinamento deliberado, escola,
   aprender com os pais, observação de quem trabalha perto, tutoria mestre→aprendiz.
   Cada fonte tem taxa própria e requisitos (tempo, dinheiro, presença de um mestre).
4. **Predisposição genética como multiplicador de TAXA**: o gene afeta a velocidade de
   ganho, nunca o valor inicial. Habilidade **não** é herdada — invariante de design,
   protegido pelo par de correlações nos critérios, não por convenção.
5. **Habilidade → produção e renda**: quantidade e qualidade do produto escalam com a
   habilidade do trabalhador; qualidade entra no preço via Fase 5. Ferreiro melhor produz
   mais e melhor, e a diferença é observável no estoque e no salário.
6. **Espiral de desenvolvimento**: trabalha → sobe metalurgia → produz melhor → ganha mais
   → melhora a oficina (bônus de taxa) → contrata aprendiz → transmite conhecimento.
   Cada elo é um efeito de sistema existente, não um script narrativo.
7. **Escolha e troca de profissão**: score por habilidade atual, personalidade e vagas
   abertas. Trocar tem custo — a habilidade da profissão antiga estagna, não zera.
8. **Tutoria**: um mestre com habilidade alta acelera o aprendiz; a taxa depende do
   `min(habilidade do mestre, teto)` e da habilidade de **ensino** dele.
9. **Cenários pareados de habilidade**: especialista vs trocador, mestre no topo vs mestre
   no piso da faixa do cenário. Idade e genes fixados por parâmetro, para que os testes
   causais rodem par a par na mesma seed.

## Critérios de verificação
- **Especialização compensa (direção, sem magnitude)**: NPC que trabalha 20 anos na mesma
  profissão termina com habilidade **maior** que a de um NPC de mesma idade e mesmos genes
  que trocou de profissão a cada 2 anos. Mesma seed nos dois braços, **20 seeds, 20/20**.
  A razão média entre eles vai para `tests/baselines/`; desvio acima de ±30% em relação ao
  baseline abre **alerta de revisão do modelo**, não falha o gate.
- **Habilidade não é herdada, taxa é** — os dois asserts juntos, um sem o outro não prova
  nada: em 200 nascimentos, o IC95 da correlação `habilidade(pai) ↔ habilidade(filho)`
  **contém 0**, enquanto o IC95 da correlação `geneDeTaxa(pai) ↔ geneDeTaxa(filho)` está
  **inteiramente acima de 0**. Se só o primeiro passar, a herança inteira pode estar morta.
- **Retornos decrescentes como propriedade matemática**: unit test da curva, sem simulação
  e sem seed — `ganho(n+1) <= ganho(n)` para todo `n` em `1..1000`.
- **Ganho no teto não move o mundo**: aplicar ganho a um NPC já no teto do cenário deixa
  `Hash(world)` inalterado. (Piso e teto são invariante do tipo — não viram critério.)
- **Mestre melhor forma aprendiz melhor**: `hab(aprendiz de mestre no topo da faixa) >
  hab(aprendiz de mestre no piso da faixa)`, faixa lida do cenário em runtime, idade e
  genes fixados, mesma seed nos dois braços. **20 seeds, 20/20**.
- **Gene muda o resultado, prática idêntica**: dois NPCs com genes diferentes e prática
  idêntica terminam com habilidades diferentes; dois com genes idênticos e prática idêntica
  terminam **byte-idênticos**. 20 seeds, 20/20 nos dois sentidos.
- **Oficina rende mais com dono melhor**: par base/tratamento na mesma seed, mesma entrada
  e mesmo número de trabalhadores, tratamento = dono com habilidade maior. Produção anual
  do tratamento maior em **10/10** seeds.
- **Habilidade entrou na conta**: desligar o sistema de habilidades por flag de teste muda
  `Hash(world)` após 10 anos de simulação.

## Fora do escopo
Herança genética propriamente dita e como os genes são gerados no nascimento: Fase 7.
Escolas como edifício da cidade e oferta de vagas por assentamento: Fase 8.

## Ver também
[npc.md](../domain/npc.md) ·
[economy.md](../domain/economy.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[rules/eval-criteria.md](../../rules/eval-criteria.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md)
