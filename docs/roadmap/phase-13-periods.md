# Fase 13 — Múltiplos períodos

**Objetivo**: períodos viram **startpoints dinâmicos** (vieses iniciais), não catálogos
fixos. Profissões, habilidades, recursos e papéis sociais podem surgir, mudar e sumir sem
fork de código. "Pré-histórico/medieval/moderno/futurista/criaturas" ficam como exemplos de
conteúdo, nunca como lista fechada do motor.

## Tasks
1. **Contrato de período como startpoint**: cenário declara vieses (distribuições, pesos,
   restrições e gatilhos) para geração/transformação de profissões, habilidades, tecnologias,
   cultura e organização social. Falha de schema ou referência inválida reprova no load com
   campo e caminho.
2. **Geração dinâmica de catálogos**: o motor materializa catálogos a partir do startpoint +
   seed do mundo. Nenhum `switch (periodo)` e nenhum enum fechado de conteúdo em `src/`.
3. **Evolução de conteúdo em runtime**: profissões/habilidades podem nascer, fundir, dividir
   e desaparecer por regras declaradas no cenário, preservando compatibilidade com economia,
   família, população e cidades já existentes.
4. **Pacotes de referência (não prescrição)**: manter pacotes de exemplo (ex.: medieval,
   pré-histórico, moderno, futurista, criaturas) só como baseline de conteúdo e regressão.
   O motor deve aceitar períodos fora desse conjunto sem mudança de código.
5. **Cadastro de período via API**: expor rota para inserir `periodDefinition` no catálogo
   de períodos do projeto. Períodos cadastrados passam a ser usados como templates oficiais
   da fase (igual os templates base entregues com ela), sem listas estáticas no cliente.
6. **Guia para IA externa**: entregar **documentação operacional**
   (`docs/domain/period-authoring.md`) para enviar a uma IA fora do projeto, ensinando como
   criar períodos válidos (contrato canônico, schema, exemplos, validação e checklist
   determinístico). O projeto **não** executa IA autora de períodos nesta fase.
7. **Baseline do horizonte evolutivo**: para efeitos de seleção declarados, rodar par
   controle/tratamento em vários horizontes e registrar em `tests/baselines/` o menor
   horizonte que separa os braços de forma estável.

## Critérios de verificação
- **Período não é branch de código**: teste de arquitetura reprova se nome de período de
  referência (ou alias de pacote) aparecer como literal de decisão em
  `LivingWorld.Domain`/`LivingWorld.Simulation`.
- **Adicionar período novo = adicionar dados**: teste cria um período sintético em runtime,
  roda 10 anos e passa sem editar `.cs`.
- **Mesmo startpoint + mesma seed = mesmo mundo**; startpoints distintos com a mesma seed
  produzem hash distinto após o mesmo horizonte.
- **Catálogo é dinâmico de verdade**: em 20 seeds, ao menos um período de referência gera
  profissão/habilidade que não existe no snapshot inicial e remove outra por transformação
  declarada; nada fora das regras do cenário pode surgir.
- **Profissão/habilidade fora do contrato é rejeitada no load** com campo apontado (teste
  negativo obrigatório).
- **Cadastro executável de período**: um `periodDefinition` enviado para a rota de cadastro
  passa no validador e vira template utilizável; versão com erro estrutural reprova com
  mensagem determinística.
- **Causalidade com braço de controle**: para um viés declarado (ex.: pressão seletiva ou
  incentivo educacional), par controle/tratamento com a mesma seed em 20 seeds confirma a
  direção esperada em ≥ 18/20; medir só tratamento reprova por desenho de teste inválido.
- Horizonte curto no gate (10 anos, invariantes por tick) e horizonte longo no nightly
  (`Category=Scenario`) para os checks de evolução acima do teto do gate.

## Fora do escopo
Criar novos sistemas de simulação para "encaixar um período". Se um período exigir mecânica
inédita, abre fase própria antes. Arte/asset/cliente 3D ficam na Fase 14 e visual web na 15.
O projeto não inclui agente de IA para autoria de períodos nesta fase. A IA (quando usada) é
externa ao projeto e produz apenas o arquivo/payload que será cadastrado pela rota.

## Ver também
[society.md](../domain/society.md) · [economy.md](../domain/economy.md) ·
[genetics-and-family.md](../domain/genetics-and-family.md) ·
[llm-contract.md](../domain/llm-contract.md) ·
[ADR-0001](../adr/ADR-0001-monolito-modular-dotnet.md) ·
[ADR-0005](../adr/ADR-0005-simulacao-deterministica-semeada.md) ·
[rules/simulation-determinism.md](../../rules/simulation-determinism.md) ·
[rules/llm-boundary.md](../../rules/llm-boundary.md)
