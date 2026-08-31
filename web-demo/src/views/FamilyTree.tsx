import type { WorldFixture } from "../fixture/types";
import { NpcToken } from "../npc/NpcToken";

export interface FamilyTreeProps {
  fixture: WorldFixture;
  agentId: string;
  onSelectAgent: (agentId: string) => void;
  /** Agent to render muted/disabled (e.g. the panel's own subject). Omit when no member is in focus. */
  focusAgentId?: string;
}

/**
 * Árvore genealógica estilo Sims (pedido do usuário 2026-08-26) — gerações empilhadas
 * (parents/self+siblings+spouse/children), ligadas por linhas, não uma lista de texto. Percorre
 * só `familyRole` (doc de tipos em `fixture/types.ts`) — nunca infere parentesco de `label`
 * livre, por isso funciona pra qualquer agent que tenha dados de família estruturados.
 */
export function FamilyTree({ fixture, agentId, onSelectAgent, focusAgentId }: FamilyTreeProps) {
  const agent = fixture.agents.find((a) => a.id === agentId);
  if (!agent) return null;

  const byRole = (role: "parent" | "spouse" | "sibling" | "child") =>
    agent.relationships
      .filter((r) => r.familyRole === role)
      .map((r) => fixture.agents.find((a) => a.id === r.withAgentId))
      .filter((a): a is NonNullable<typeof a> => a !== undefined);

  const parents = byRole("parent");
  const spouse = byRole("spouse")[0];
  const siblings = byRole("sibling");
  const children = byRole("child");

  function Node({ id, name, muted }: { id: string; name: string; muted?: boolean }) {
    return (
      <button
        type="button"
        className={`family-tree-node${muted ? " family-tree-node--self" : ""}`}
        onClick={() => onSelectAgent(id)}
        disabled={muted}
        data-testid="family-tree-node"
      >
        <NpcToken id={id} size={40} />
        <span>{name}</span>
      </button>
    );
  }

  return (
    <div data-testid="family-tree">
      {parents.length > 0 && (
        <div className="family-tree-row">
          {parents.map((p) => (
            <Node key={p.id} id={p.id} name={p.name} />
          ))}
        </div>
      )}
      <div className="family-tree-row family-tree-row--self">
        {siblings.map((s) => (
          <Node key={s.id} id={s.id} name={s.name} />
        ))}
        <Node id={agent.id} name={agent.name} muted={agent.id === focusAgentId} />
        {spouse && <Node key={spouse.id} id={spouse.id} name={spouse.name} />}
      </div>
      {children.length > 0 && (
        <div className="family-tree-row">
          {children.map((c) => (
            <Node key={c.id} id={c.id} name={c.name} />
          ))}
        </div>
      )}
      {parents.length === 0 && children.length === 0 && siblings.length === 0 && !spouse && (
        <p className="family-tree-empty">No recorded family for {agent.name}.</p>
      )}
    </div>
  );
}
