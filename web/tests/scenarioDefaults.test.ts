import { describe, expect, it } from "vitest";
import { defaultScenarioForm, scenarioFormToJson } from "../src/scenarioDefaults";

describe("scenarioFormToJson", () => {
  it("turns every settlement painted on the map into a founded city at the same coordinate", () => {
    const form = defaultScenarioForm();
    form.settlements = [
      { name: "Norte", x: 1, y: 2 },
      { name: "Sul", x: 3, y: 7 },
      { name: "Leste", x: 8, y: 4 },
      { name: "Oeste", x: 0, y: 5 },
    ];

    const scenario = JSON.parse(scenarioFormToJson(form));

    expect(scenario.Cities).toEqual([
      { X: 1, Y: 2, Name: "Norte", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
      { X: 3, Y: 7, Name: "Sul", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
      { X: 8, Y: 4, Name: "Leste", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
      { X: 0, Y: 5, Name: "Oeste", FoundedAtTick: 0, AggregatePool: { Count: 0, WealthSum: 0, HealthSum: 0 } },
    ]);
    expect([scenario.VillageX, scenario.VillageY]).toEqual([1, 2]);
  });

  it("keeps the blank-world economy off until a productive economy is authored", () => {
    const scenario = JSON.parse(scenarioFormToJson(defaultScenarioForm()));

    expect(scenario.EconomyEnabled).toBe(false);
    expect(scenario.Workplaces).toEqual([]);
  });
});
