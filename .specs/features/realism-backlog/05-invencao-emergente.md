# Invenção emergente: tecnologia, veículos, robôs (não começado; pede desenho antes de código)

## O que o dono pediu (palavras dele)

- "Queria puxar também a possibilidade de inventarem tecnologia, veículos (aéreos, terrestres, aquáticos), robôs etc."
- "Lembrando que tudo no livingworld é determinístico e totalmente simulation driven, em nada escrito. Então o npc desenvolve tudo e tem a concepção da ideia (que pode ser reproduzível depois). Incluindo aparência do que foi inventado e a animação utilizada (material e forma que foi feito influencia na aparência)."
- "Levar em consideração métodos anticoncepcionais caso alguém tenha-os inventado."

## O que existe hoje

- `Emergence/TechDiscoverySystem`: uma cidade sob pressão (por exemplo trajeto longo ao mercado) descobre uma "tecnologia" representada por um vetor; culturas isoladas divergem; a adoção se espalha por vínculos entre cidades. Ainda não foi auditado quanto disso é lista fixa. A seed 1 para de inventar depois da primeira tecnologia (pressão cai abaixo do limiar).
- `Objects/ObjectUse`: objetos que dão conforto ou duração a ações.
- O cliente é Godot (ADR-0022); a aparência do que é inventado hoje não deriva de nada.

## O que precisa ser decidido num documento de desenho (TDD + ADR) antes de implementar

1. **Representação de uma invenção sem catálogo.** Proposta a avaliar: uma invenção é uma composição de primitivas (materiais com propriedades físicas, formas, mecanismos como alavanca, roda, vela, engrenagem, mola, combustão, circuito; fontes de energia) mais a função que cumpre. O espaço é gerado, não listado.
2. **O que faz uma composição funcionar.** Regras físicas simples e determinísticas (peso, resistência, flutuação, sustentação, atrito, energia disponível) decidem se o artefato cumpre a função e com que desempenho. Um barco flutua porque a forma e o material dão empuxo, não porque existe a entrada "barco".
3. **Quem inventa e por quê.** Um NPC, não a cidade: necessidade sentida, habilidade, abertura e curiosidade, conhecimento prévio (o que já existe ao redor é pré-requisito), tempo livre, recursos, acaso semeado. Tentativa e erro com custo de material e tempo; a maioria falha.
4. **A ideia como coisa transmissível.** O inventor guarda a concepção; ensina, vende ou esconde; outros copiam vendo o artefato; a ideia se perde se ninguém a aprender. Reprodução exige as habilidades e os materiais.
5. **Árvore que emerge.** Sem pré-requisitos escritos: metalurgia antes de motor porque o motor precisa de peças que só a metalurgia faz.
6. **Veículos** (terrestres, aquáticos, aéreos) como invenções que mudam o custo de deslocamento no mapa, com consumo, capacidade e risco.
7. **Robôs e automação**: artefatos que executam trabalho; mexe em emprego, produção e, no limite, em quem é agente no mundo. Precisa de decisão própria.
8. **Aparência e animação derivadas.** O cliente Godot monta o visual a partir da composição (material dá cor e textura, forma dá malha, mecanismo dá movimento). Nada desenhado à mão por invenção. Precisa de protótipo no cliente.
9. **Efeito social**: quem adota primeiro, resistência, desigualdade, armas, contracepção (ver 02), medicina (liga com saúde).
10. **Custo e estado**: quantas invenções o mundo guarda, como entram no snapshot, custo por NPC-tick.

## Critérios mensuráveis propostos

- Duas culturas isoladas com as mesmas necessidades chegam a soluções diferentes que funcionam.
- Nenhuma invenção aparece sem os pré-requisitos materiais e de conhecimento presentes no mundo.
- Uma invenção perdida (todos os que sabiam morreram) deixa de ser produzida.
- Mesmo mundo, mesma seed: mesma história de invenções.
- O cliente desenha um artefato nunca visto só com os dados da composição.

## Estado (2026-11-13)

Desenho aprovado (ADR-0045, `.specs/features/emergent-invention/design.md`). **Fatia 1 juntada (AD-141, `2b34fb64`)**: materiais brutos, `Design` como composição de partes, avaliador físico só com inteiros, NPC que inventa por frustração de vau, ensino, cópia, lápide, `GET /designs`, malha no Godot. Doc em `docs/domain/invention.md`. Em 20 seeds × 100 anos: 24 invenções (todas as seeds), 40% das travessias de água feitas na embarcação, 0% de sobreposição de designs entre seeds.

## Falta fazer

1. Sonda de terra (rolar e deslizar com eixo) e, depois, de ar.
2. Substituir o `TechDiscoverySystem`: adoção por cidade derivada de `KnownDesign`, migrar `/cultures/{id}/technologies`, o painel do cliente e as métricas de civilização, remover o bloco `Emergence`.
3. Carga por viagem, venda da embarcação no mercado, material tirado de estoque, pesca.
4. Contracepção e remédio como invenção; robô em ADR próprio.
5. Animação por junção e pose de uso no cliente; cópia disparada por evento.
6. Frase proposta para o critério da Fase 24 do `ROADMAP.md` (o dono decide): "Nenhum pré-requisito tecnológico é declarado: uma invenção só existe se o avaliador físico do motor mostrar que a composição de partes e materiais cumpre a função, e o que vem antes emerge do que a física deixa funcionar com os materiais e o saber presentes no mundo."
