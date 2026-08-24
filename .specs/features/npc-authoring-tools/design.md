# NPC authoring tools — Design

## Fluxo

`NpcInspector -> AuthoringSource -> HTTP -> WorldAuthoringCommands -> Domain/WorldState`.

Os comandos validam tudo antes da primeira escrita. Personalidade usa a factory existente;
relações são removidas pelas duas chaves direcionais; ação usa `SetCurrentAction` no tick atual.
Cada aceite ou rejeição emite um evento de autoria com payload estável para auditoria.

Templates extraordinários pertencem ao formulário de cenário: são dados clonáveis/editáveis,
nunca branches nominais no motor. Invocação recebe alvo NPC e, opcionalmente, célula de mapa;
efeitos de construto usam a célula somente depois das mesmas validações de custo e ocupação.
