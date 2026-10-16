# ADR-0025: Godot substitui o cliente 3D da Fase 14 (Unreal adiado)

- **Status**: aceito
- **Data**: 2026-10-14
- **Decisores**: Alan (decisão de escopo, issue #66)

## Contexto
A Fase 14 previa um cliente Unreal 3D com personagens, animação e voz sobre a mesma API do
motor. O ADR-0022 adotou o Godot e aposentou o cliente web; o cliente em `client/godot/` já
entrega mundo 3D isométrico, personagens, animação, dia/noite, interior e minimapa. Manter a
Fase 14 como "adiada, não iniciada" esconde que a maior parte do objetivo já existe.

## Decisão
O Godot substitui o cliente 3D da Fase 14 por ora. Coberto pelo Godot: cliente 3D, personagens,
animação, câmera, interior. **Não coberto**: voz e o Unreal em si — seguem adiados, sem issue,
e voltam só por novo ADR. `docs/roadmap/phase-14-unreal.md` é mantido (não apagado nem
renomeado) com status "substituída pelo ADR-0025". A Fase 15 fecha: servidor, FOW e tokens
prontos; a UI é o Godot (telas pendentes em #58 e #59).

## Alternativas consideradas
- Manter a 14 como adiada — esconde o que o Godot já cobre.
- Apagar o arquivo da fase — perde o histórico de intenção (voz) e exige aprovação.

## Consequências
- **Positivas**: roadmap reflete o código; o foco vai para realismo (Fases 21–23).
- **Negativas / trade-offs**: voz e Unreal ficam sem dono até novo ADR.
- **Follow-ups**: se voltar a haver cliente Unreal, novo ADR que substitua este.
