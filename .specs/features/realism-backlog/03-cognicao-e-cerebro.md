# Cognição, pensamento e cérebro do NPC (não começado)

> Achados 9 e 11 da auditoria (`relatorios/relatorio-realismo.md`). Issues relacionadas: #143 (índice de vivacidade),
> #145 (memória da própria vida), #146 (humor e luto), #147 (necessidades além da sobrevivência), #148 (aspirações),
> #149 (rotina individual), #150 (personalidade), #151 (corpo muda o dia), #144 (socializar com alguém, em parte feito).

## O que o dono pediu (palavras dele)

- "Toda a existência de um npc e tudo que ele faz deve mirar na realidade da vida real, em todos os aspectos, até na tomada de decisão." "Até os pensamentos."
- "Os npcs eu queria tipo os neurônios e rede neural (ao ler o cérebro)."
- "Levar em consideração a educação e criação dada em casa pelos pais (biológicos, adotivos, tios, avós ou quem criou eles)." (a parte de habilidade e temperamento entrou na AD-132)

## Como é hoje (auditoria)

- O NPC não guarda memória do que vive: só jogador, conversa com LLM e assimilação de crença criam lembrança. Não há emoção nem luto.
- O "pensamento" registrado não é o motivo real da ação: pressões de renda e de proteger a casa aparecem no rastro, mas Work vale uma constante 50.
- A decisão é uma utilidade sobre quatro necessidades (fome, sede, sono, social), rotina por profissão e histerese. `Socialize` enche a necessidade social mesmo sem ninguém por perto.
- 41 a 45% dos despertares não mudam nada.

## A fazer, parte 1: cognição realista sobre o motor atual

1. **Memória episódica.** O NPC lembra do que viveu (nascimento de filho, morte de alguém próximo, fome, traição, ajuda recebida, briga, mudança de emprego), com força que decai e se reforça ao relembrar, capacidade limitada e viés para o que foi emocionalmente forte. Alimentada pelo log de eventos, sem varredura por tick.
2. **Emoção e humor.** Estado afetivo que reage a eventos (alegria, medo, raiva, tristeza, luto com duração realista) e pesa na decisão, no trabalho, no sono e nos vínculos. Luto por cônjuge, filho, pais.
3. **Pensamento como motivo real.** O rastro cognitivo tem de ser a causa da ação, não uma descrição ao lado. Tudo o que aparece como "por que fiz isso" precisa ser um termo que de fato entrou na escolha.
4. **Necessidades além da sobrevivência**: segurança, pertencimento, estima, propósito, descanso e lazer. `Socialize` só satisfaz com alguém.
5. **Aspirações e planos**: objetivos de médio prazo (casar, juntar para algo, aprender um ofício, mudar de cidade) que organizam o dia.
6. **Rotina individual**, não só por profissão: cronotipo, hábitos, dia de descanso, variação por idade e saúde.
7. **Vieses humanos na decisão**: desconto do futuro (maior na adolescência, ver 02), aversão à perda, hábito, imitação dos pares, erro de percepção e crença errada (o NPC decide pelo que acredita, não pelo que é verdade).
8. **O que foi aprendido em casa**: valores, religião, normas e preconceitos passados por quem criou, além de habilidade e temperamento.

## A fazer, parte 2: cérebro como rede neural legível (decisão de arquitetura pendente)

Pedido: o cérebro do NPC como neurônios e rede, que se possa "ler".

Pontos em aberto, que pedem ADR e experimento antes de implementar:
- **Custo.** A decisão custa hoje ~1,4 µs por despertar. Uma rede por NPC, mesmo pequena, pode custar dezenas de vezes isso, e a meta do dono é desempenho alto. Medir primeiro.
- **Determinismo.** Compatível, desde que a aritmética seja a mesma em toda máquina (sem `Math.Exp` da plataforma; ponto fixo ou `DeterministicMath`) e os pesos façam parte do estado canônico.
- **De onde vêm os pesos.** Herdados dos pais (genética), ajustados pela criação e pela experiência (plasticidade), nunca escritos à mão.
- **Legibilidade.** Hoje "ler o pensamento" é um rastro de utilidades. Com rede, é preciso definir o que o inspetor mostra (ativações, quais entradas pesaram).
- **Tamanho do estado.** Pesos por NPC no snapshot multiplicam o armazenamento.

Experimento proposto: rede mínima por NPC (entradas = necessidades, emoção, percepção, memória relevante; saída = preferência por ação), pesos herdados e plásticos, comparada com a utilidade atual em custo por despertar, bytes por NPC e realismo do comportamento (índice de vivacidade, #143). Só depois decidir se substitui, complementa ou fica só para NPCs observados.

## Critérios mensuráveis

- Todo termo do rastro cognitivo muda a escolha quando alterado (teste de intervenção).
- Luto: queda mensurável de trabalho e socialização depois de uma morte próxima, com recuperação em meses.
- Diversidade de dias entre NPCs do mesmo ofício (hoje quase idênticos).
- Custo por despertar e bytes por NPC antes e depois.
