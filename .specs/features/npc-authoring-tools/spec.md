# NPC authoring tools

## Objetivo

Permitir ajustes explícitos durante uma simulação sem confundir autoria do operador com decisão
autônoma do NPC. Toda intervenção é validada, causal e persistida no estado canônico.

## Requisitos

- **AUT-01** — editar personalidade SHALL validar os dez traços em `[0,100]` e substituir o
  conjunto atomicamente; entrada inválida não altera o NPC.
- **AUT-02** — romper relações SHALL remover somente os pares direcionais entre os dois NPCs,
  sem alterar parentesco, casamento ou demais relações.
- **AUT-03** — ordenar uma ação SHALL aceitar apenas ações do catálogo e registrar início no tick
  atual; a decisão autônoma poderá substituí-la em ticks posteriores conforme as regras normais.
- **AUT-04** — a web SHALL identificar essas operações como intervenção de autoria e mostrar
  sucesso ou rejeição; o cliente nunca altera o snapshot local como fonte da verdade.

## Fora do escopo

Controle mental permanente, edição de identidade genética, apagar parentesco e scripts arbitrários.
