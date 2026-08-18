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

  it("converts a city draft's buildings from the local canvas into world-absolute coordinates", () => {
    const form = defaultScenarioForm();
    form.width = 100;
    form.height = 100;
    form.initialPopulation = 64; // citySide(64, ...) = 4 (redondo, sem deslocamento fracionário)
    form.settlements = [{ name: "Vila", x: 50, y: 50 }];

    const scenario = JSON.parse(scenarioFormToJson(form, {
      0: { buildings: [{ x: 2, y: 2, rotation: 90 }, { x: 0, y: 0 }] },
    }));

    expect(scenario.Buildings).toEqual([
      { CityIndex: 0, BuildingTypeId: 1, X: 50, Y: 50, Orientation: 90 },
      { CityIndex: 0, BuildingTypeId: 1, X: 48, Y: 48, Orientation: 0 },
    ]);
  });

  it("clamps out-of-map draft buildings onto the nearest valid cell and drops collisions instead of failing", () => {
    const form = defaultScenarioForm();
    form.width = 10;
    form.height = 10;
    form.initialPopulation = 64; // citySide(64, 10, 10) = 4 (limitado pelo mapa, ainda redondo)
    form.settlements = [{ name: "Vila", x: 5, y: 5 }];

    const scenario = JSON.parse(scenarioFormToJson(form, {
      // 2,2 é o centro do canvas local (side/2): mapeia pra célula exata do assentamento.
      0: { buildings: [{ x: 2, y: 2 }, { x: 2, y: 2 }, { x: -1000, y: -1000 }] },
    }));

    expect(scenario.Buildings).toEqual([
      { CityIndex: 0, BuildingTypeId: 1, X: 5, Y: 5, Orientation: 0 },
      { CityIndex: 0, BuildingTypeId: 1, X: 0, Y: 0, Orientation: 0 },
    ]);
  });

  it("omits Buildings entirely when the creator was never opened for a settlement", () => {
    const form = defaultScenarioForm();
    form.settlements = [{ name: "Vila", x: 5, y: 5 }];

    const scenario = JSON.parse(scenarioFormToJson(form));

    expect(scenario.Buildings).toEqual([]);
  });
});
