import { describe, expect, it } from "vitest";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";

// Risco do design.md: fixture digitado à mão é grande o bastante pra ter uma referência
// quebrada (`agentId` que não existe em `agents`, por exemplo) e quebrar uma tela em silêncio.
// Este teste garante que todo id referenciado em qualquer campo existe na lista correspondente.

const settlementIds = new Set(WORLD_FIXTURE.settlements.map((s) => s.id));
const householdIds = new Set(WORLD_FIXTURE.households.map((h) => h.id));
const agentIds = new Set(WORLD_FIXTURE.agents.map((a) => a.id));
const eventIds = new Set(WORLD_FIXTURE.events.map((e) => e.eventId));

describe("Oakbridge fixture referential integrity", () => {
  it("every household references a settlement that exists", () => {
    for (const household of WORLD_FIXTURE.households) {
      expect(settlementIds.has(household.settlementId)).toBe(true);
    }
  });

  it("every household member id references an agent that exists", () => {
    for (const household of WORLD_FIXTURE.households) {
      for (const memberId of household.memberIds) {
        expect(agentIds.has(memberId)).toBe(true);
      }
    }
  });

  it("every household head id references a member of that household", () => {
    for (const household of WORLD_FIXTURE.households) {
      expect(household.memberIds).toContain(household.headId);
    }
  });

  it("every agent references a settlement that exists", () => {
    for (const agent of WORLD_FIXTURE.agents) {
      expect(settlementIds.has(agent.settlementId)).toBe(true);
    }
  });

  it("every non-null agent householdId references a household that exists", () => {
    for (const agent of WORLD_FIXTURE.agents) {
      if (agent.householdId !== null) {
        expect(householdIds.has(agent.householdId)).toBe(true);
      }
    }
  });

  it("every agent relationship references an agent that exists", () => {
    for (const agent of WORLD_FIXTURE.agents) {
      for (const relationship of agent.relationships) {
        expect(agentIds.has(relationship.withAgentId)).toBe(true);
      }
    }
  });

  it("every agent whyFactors linkedEventId references an event that exists", () => {
    for (const agent of WORLD_FIXTURE.agents) {
      for (const factor of agent.whyFactors) {
        if (factor.linkedEventId !== undefined) {
          expect(eventIds.has(factor.linkedEventId)).toBe(true);
        }
      }
    }
  });

  it("every event causeEventId references an event that exists", () => {
    for (const event of WORLD_FIXTURE.events) {
      if (event.causeEventId !== null) {
        expect(eventIds.has(event.causeEventId)).toBe(true);
      }
    }
  });

  it("every event affectedAgentIds reference agents that exist", () => {
    for (const event of WORLD_FIXTURE.events) {
      for (const agentId of event.affectedAgentIds) {
        expect(agentIds.has(agentId)).toBe(true);
      }
    }
  });

  it("every event affectedHouseholdIds reference households that exist", () => {
    for (const event of WORLD_FIXTURE.events) {
      for (const householdId of event.affectedHouseholdIds) {
        expect(householdIds.has(householdId)).toBe(true);
      }
    }
  });

  it("every event settlementId references a settlement that exists", () => {
    for (const event of WORLD_FIXTURE.events) {
      expect(settlementIds.has(event.settlementId)).toBe(true);
    }
  });

  it("every story thread eventIds reference events that exist", () => {
    for (const thread of WORLD_FIXTURE.storyThreads) {
      for (const eventId of thread.eventIds) {
        expect(eventIds.has(eventId)).toBe(true);
      }
    }
  });

  it("every story thread householdIds reference households that exist", () => {
    for (const thread of WORLD_FIXTURE.storyThreads) {
      for (const householdId of thread.householdIds) {
        expect(householdIds.has(householdId)).toBe(true);
      }
    }
  });

  it("every story thread agentIds reference agents that exist", () => {
    for (const thread of WORLD_FIXTURE.storyThreads) {
      for (const agentId of thread.agentIds) {
        expect(agentIds.has(agentId)).toBe(true);
      }
    }
  });
});
