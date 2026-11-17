# Cidade: rua de verdade, lote na obra e bairro denso (pedidos do dono, 2026-11-18)

> Continuação da AD-173 (ruas, lotes, quintal). Só começa depois que a AD-173 juntar. Índice: [09](09-cliente-visual-e-jogador.md).

## Palavras do dono
- "Preciso que tenha sistema real de rua. Nada deve ficar apenas no visual (exceto os NPCs não se esbarrarem, e contornarem,
  isso ok ficar no visual apenas)."
- "Quintal depende, nem toda casa vai ter quintal e isso afeta a construção da casa (focado em realismo lembra)."
- "Nem toda rua é de asfalto também."
- "Até podemos ter prédios colados (estilo favela), mas isso deveria afetar a aparência da cidade e como ela é vista. Em alguns
  casos até vira ponto turístico (igual as pessoas vão visitar o Rio de Janeiro para ver as favelas, mas as pessoas que são do
  Brasil acham feio ou têm medo, além de ter higiene pior), porém tem sempre que ter como chegar na casa, seja via telhado,
  via escada, etc."

## A fazer
1. **Rua nasce do trajeto real**: o NPC anda por caminho de verdade (não em linha reta); o pisoteio abre trilha; trecho sem uso
   se desgasta e some.
2. **Superfície com consequência**: trilha, terra, cascalho e pedra mudam passo, carga e veículo; chuva faz lama onde não é
   calçado; o NPC escolhe o caminho melhor quando compensa.
3. **Calçar é obra**: técnica (invenção/tecnologia), material, mão de obra, quem decide e paga, manutenção.
4. **Lote e quintal na obra**: custo e tempo de construir, onde a casa pode nascer, valor; usos reais do quintal (horta, bichos,
   poço, ampliar a casa); meios diferentes já na fundação.
5. **Bairro denso emerge** (terra cara, renda baixa): casas coladas e empilhadas, com acesso real sempre (viela, escada, laje,
   telhado) por onde o NPC anda; casa sem acesso não nasce.
6. **Consequências do bairro denso**: higiene pior (contágio, água, escoamento, ligado à saúde); como a cidade é vista, por
   pessoa (origem, traços, experiência, crime): quem é de perto pode achar feio ou ter medo, quem é de fora pode visitar;
   efeito em onde morar, valor dos imóveis e migração; visita de fora usa hospedagem e mercado.
7. **Pendências da AD-173** (08d, itens 158–173): lotação das casas na cidade média, custo de escala acima do teto, números
   sem fonte, queda da fome sem causa achada.
8. **Cliente**: desenha só o que o motor disser (lote, rua por superfície, bairro denso, acessos); sai o quintal e a rua
   inventados na AD-171.
   **Feito na AD-177** (item 8, parte do cliente): rua pela camada `Roads` e superfície, lote pelo `lot`, ficha de rua e
   de lote, raio-x, planta, Tab. Bairro denso e acessos esperam o motor (AD-176).

## Lacunas do cliente deixadas pela AD-177
9. **Sem delta de rua**: o motor só manda `Roads` no retrato do mundo; o cliente relê a camada a cada 30 s (e logo depois de
   um lote apontar rua desconhecida), um retrato do mundo inteiro por vez. Pedir `roadUpserts`/`roadRemoved` no delta.
10. **Acesso e obra de rua** (viela, escada, laje, calçamento em andamento): só o ponto único em `street_style.gd`
    (`ACCESS_STYLES`, `WORK_STYLE`); falta o campo no contrato.
11. **Cerca ou sebe**: o motor diz "cercado" sem o tipo; o cliente desenha só cerca de varas. Quintal sem uso modelado
    (horta, bichos, poço; item 4): só o chão do terreiro.
12. **Estação no chão da cidade**: neve e seca recolorem o terreno, não a rua nem o quintal.
13. **Árvores fora da cidade**: a vegetação de enfeite (`scenery.gd`) ainda evita toda a caixa da cidade; o motor não diz
    onde há árvore.
14. **Raio-x sem interior**: o escopo Cidade não traz cômodos; a planta não mostra pessoas; o rótulo só usa o nome do
    prédio depois que a ficha dele foi lida (o escopo não traz nome).
15. **Andar pela rua**: o NPC segue as células que o servidor manda; se o motor andar em linha reta (item 1), o desenho
    também anda.
