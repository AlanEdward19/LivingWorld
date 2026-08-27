import type { WorldFixture } from "../fixture/types";

export interface LifeViewProps {
  fixture: WorldFixture;
  agentId: string;
}

/**
 * Marcos de vida do agent (doc#122) — nascimento, mudança, aprendizado, casamento, filhos,
 * conquista profissional, luto, presente — numa timeline vertical simples.
 */
export function LifeView({ fixture, agentId }: LifeViewProps) {
  const agent = fixture.agents.find((a) => a.id === agentId);
  if (!agent) return null;

  return (
    <div data-testid="life-view">
      <h1>{agent.name}'s Life</h1>
      <ol data-testid="life-milestones">
        {agent.lifeMilestones.map((milestone, index) => (
          <li key={index}>
            <span>{milestone.approxDate}</span> — <span>{milestone.label}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}
