import { useState } from "react";
import { createWorld } from "../api";
import {
  ACTION_TYPES,
  defaultScenarioForm,
  scenarioFormToJson,
  type ScenarioFormState,
} from "../scenarioDefaults";
import { KeyNumberListEditor, ObjectListEditor, type FieldSpec } from "./formFields";

export interface CreateWorldFormProps {
  onCreated?: (npcCount: number) => void;
}

/// Feature ad-hoc "criar mundo" (AD-001 em .specs/STATE.md): formulário campo a campo (não
/// textarea de JSON cru) que monta o mesmo scenario JSON que `ScenarioLoaderV2.LoadWorld`
/// consome, chama `POST /worlds/create` e troca o mundo canônico do processo.
export function CreateWorldForm({ onCreated }: CreateWorldFormProps) {
  const [form, setForm] = useState<ScenarioFormState>(defaultScenarioForm);
  const [status, setStatus] = useState<"idle" | "submitting" | "error">("idle");
  const [error, setError] = useState<string | null>(null);

  function set<K extends keyof ScenarioFormState>(key: K, value: ScenarioFormState[K]) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setStatus("submitting");
    setError(null);
    try {
      const response = await createWorld(scenarioFormToJson(form));
      if (!response.ok) {
        setError(`criar mundo falhou: ${response.status} ${await response.text()}`);
        setStatus("error");
        return;
      }
      const body = (await response.json()) as { npcCount: number };
      setStatus("idle");
      onCreated?.(body.npcCount);
    } catch (err) {
      setError(String(err));
      setStatus("error");
    }
  }

  return (
    <form data-testid="create-world-form" onSubmit={handleSubmit}>
      <h2>Criar mundo</h2>

      <fieldset>
        <legend>Mapa</legend>
        <label>
          Largura:{" "}
          <input
            type="number"
            aria-label="map-width"
            value={form.width}
            onChange={(e) => set("width", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Altura:{" "}
          <input
            type="number"
            aria-label="map-height"
            value={form.height}
            onChange={(e) => set("height", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Seed:{" "}
          <input
            type="number"
            aria-label="map-seed"
            value={form.seed}
            onChange={(e) => set("seed", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Tamanho da região:{" "}
          <input
            type="number"
            aria-label="map-region-size"
            value={form.regionSize}
            onChange={(e) => set("regionSize", Number(e.target.value))}
          />
        </label>
        <br />
        <label>
          Terrenos (ids, csv):{" "}
          <input
            type="text"
            aria-label="map-terrain-ids"
            value={form.terrainIds}
            onChange={(e) => set("terrainIds", e.target.value)}
          />
        </label>{" "}
        <label>
          Biomas (ids, csv):{" "}
          <input
            type="text"
            value={form.biomeIds}
            onChange={(e) => set("biomeIds", e.target.value)}
          />
        </label>{" "}
        <label>
          Recursos (ids, csv):{" "}
          <input
            type="text"
            value={form.resourceIds}
            onChange={(e) => set("resourceIds", e.target.value)}
          />
        </label>
        <br />
        <label>
          Custo base:{" "}
          <input
            type="number"
            step="any"
            value={form.costWeightsBase}
            onChange={(e) => set("costWeightsBase", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Peso de altitude:{" "}
          <input
            type="number"
            step="any"
            value={form.costWeightsAltitude}
            onChange={(e) => set("costWeightsAltitude", Number(e.target.value))}
          />
        </label>

        <KeyNumberListEditor
          label="Peso de custo por terreno"
          keyLabel="terrainId"
          rows={form.terrainWeight}
          onChange={(rows) => set("terrainWeight", rows)}
        />

        <ObjectListEditor
          label="Assentamentos"
          fields={[
            { name: "name", label: "nome", type: "text" },
            { name: "x", label: "x", type: "number" },
            { name: "y", label: "y", type: "number" },
          ]}
          rows={form.settlements}
          emptyRow={{ name: "", x: 0, y: 0 }}
          onChange={(rows) => set("settlements", rows)}
        />
      </fieldset>

      <fieldset>
        <legend>População</legend>
        <label>
          População inicial:{" "}
          <input
            type="number"
            aria-label="population-initial"
            value={form.initialPopulation}
            onChange={(e) => set("initialPopulation", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Cultura:{" "}
          <input
            type="number"
            aria-label="population-culture"
            value={form.culture}
            onChange={(e) => set("culture", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Vila X:{" "}
          <input
            type="number"
            value={form.villageX}
            onChange={(e) => set("villageX", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Vila Y:{" "}
          <input
            type="number"
            value={form.villageY}
            onChange={(e) => set("villageY", Number(e.target.value))}
          />
        </label>
        <br />
        <label>
          Culturas (ids, csv):{" "}
          <input
            type="text"
            value={form.cultureIds}
            onChange={(e) => set("cultureIds", e.target.value)}
          />
        </label>{" "}
        <label>
          Profissões (ids, csv):{" "}
          <input
            type="text"
            value={form.professionIds}
            onChange={(e) => set("professionIds", e.target.value)}
          />
        </label>{" "}
        <label>
          Tipos de local (ids, csv):{" "}
          <input
            type="text"
            value={form.locationTypeIds}
            onChange={(e) => set("locationTypeIds", e.target.value)}
          />
        </label>
        <br />
        <label>
          Longevidade máxima (anos):{" "}
          <input
            type="number"
            value={form.maxLongevityYears}
            onChange={(e) => set("maxLongevityYears", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Idade mínima de fertilidade:{" "}
          <input
            type="number"
            value={form.fertilityMinAge}
            onChange={(e) => set("fertilityMinAge", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Idade máxima de fertilidade:{" "}
          <input
            type="number"
            value={form.fertilityMaxAge}
            onChange={(e) => set("fertilityMaxAge", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Chance anual de concepção:{" "}
          <input
            type="number"
            step="any"
            value={form.annualConceptionChance}
            onChange={(e) => set("annualConceptionChance", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Gestação (dias):{" "}
          <input
            type="number"
            value={form.gestationDays}
            onChange={(e) => set("gestationDays", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Bytes máx. por NPC/ano:{" "}
          <input
            type="number"
            value={form.maxBytesPerNpcPerYear}
            onChange={(e) => set("maxBytesPerNpcPerYear", Number(e.target.value))}
          />
        </label>

        <ObjectListEditor
          label="Tabela de mortalidade"
          fields={[
            { name: "minAgeYears", label: "idade min", type: "number" },
            { name: "maxAgeYears", label: "idade max", type: "number" },
            { name: "baseAnnualMortality", label: "mortalidade anual", type: "number" },
          ]}
          rows={form.lifeTableBrackets}
          emptyRow={{ minAgeYears: 0, maxAgeYears: 0, baseAnnualMortality: 0 }}
          onChange={(rows) => set("lifeTableBrackets", rows)}
        />
      </fieldset>

      <fieldset>
        <legend>Comportamento</legend>
        <label>
          Decaimento fome/h:{" "}
          <input
            type="number"
            step="any"
            value={form.hungerDecayPerHour}
            onChange={(e) => set("hungerDecayPerHour", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Decaimento sede/h:{" "}
          <input
            type="number"
            step="any"
            value={form.thirstDecayPerHour}
            onChange={(e) => set("thirstDecayPerHour", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Decaimento sono/h:{" "}
          <input
            type="number"
            step="any"
            value={form.sleepDecayPerHour}
            onChange={(e) => set("sleepDecayPerHour", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Decaimento social/h:{" "}
          <input
            type="number"
            step="any"
            value={form.socialDecayPerHour}
            onChange={(e) => set("socialDecayPerHour", Number(e.target.value))}
          />
        </label>
        <br />
        <label>
          Limiar de urgência:{" "}
          <input
            type="number"
            value={form.urgencyThreshold}
            onChange={(e) => set("urgencyThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Passos máx. de seleção de ação:{" "}
          <input
            type="number"
            value={form.maxActionSelectionSteps}
            onChange={(e) => set("maxActionSelectionSteps", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Histerese:{" "}
          <input
            type="checkbox"
            checked={form.hysteresisEnabled}
            onChange={(e) => set("hysteresisEnabled", e.target.checked)}
          />
        </label>{" "}
        <label>
          Bônus de continuidade:{" "}
          <input
            type="number"
            step="any"
            value={form.continuityBonus}
            onChange={(e) => set("continuityBonus", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Eficiência de sono sem-teto:{" "}
          <input
            type="number"
            step="any"
            value={form.homelessSleepEfficiency}
            onChange={(e) => set("homelessSleepEfficiency", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Ação default:{" "}
          <select
            value={form.defaultAction}
            onChange={(e) => set("defaultAction", e.target.value as ScenarioFormState["defaultAction"])}
          >
            {ACTION_TYPES.map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </select>
        </label>

        <fieldset>
          <legend>Duração máx. por ação (horas)</legend>
          {ACTION_TYPES.map((action) => (
            <label key={action}>
              {action}:{" "}
              <input
                type="number"
                aria-label={`max-duration-${action}`}
                value={form.maxDurationHours[action]}
                onChange={(e) =>
                  set("maxDurationHours", { ...form.maxDurationHours, [action]: Number(e.target.value) })
                }
              />
            </label>
          ))}
        </fieldset>

        <ObjectListEditor
          label="Slots de rotina"
          fields={
            [
              { name: "professionId", label: "profissão (vazio=qualquer)", type: "nullable-number" },
              { name: "stage", label: "estágio", type: "select", options: ["Child", "Adult", "Elder"] },
              { name: "hourStart", label: "hora início", type: "number" },
              { name: "hourEnd", label: "hora fim", type: "number" },
              { name: "action", label: "ação", type: "select", options: ACTION_TYPES },
            ] as const
          }
          rows={form.routineSlots}
          emptyRow={{ professionId: null, stage: "Adult", hourStart: 0, hourEnd: 0, action: "Idle" }}
          onChange={(rows) => set("routineSlots", rows)}
        />
      </fieldset>

      <fieldset>
        <legend>Economia</legend>
        <label>
          Habilitada:{" "}
          <input
            type="checkbox"
            checked={form.economyEnabled}
            onChange={(e) => set("economyEnabled", e.target.checked)}
          />
        </label>{" "}
        <label>
          Recurso comida (id):{" "}
          <input
            type="number"
            value={form.foodResourceId}
            onChange={(e) => set("foodResourceId", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Recurso água (id):{" "}
          <input
            type="number"
            value={form.waterResourceId}
            onChange={(e) => set("waterResourceId", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Sensibilidade de preço:{" "}
          <input
            type="number"
            step="any"
            value={form.priceSensitivity}
            onChange={(e) => set("priceSensitivity", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Tipos de local de mercado (ids, csv):{" "}
          <input
            type="text"
            value={form.marketLocationTypeIds}
            onChange={(e) => set("marketLocationTypeIds", e.target.value)}
          />
        </label>

        <KeyNumberListEditor
          label="Capacidade por recurso+local (chave: resourceId,locationTypeId)"
          keyLabel="resourceId,locationTypeId"
          rows={form.capacityByResourceLocation}
          onChange={(rows) => set("capacityByResourceLocation", rows)}
        />
        <KeyNumberListEditor
          label="Deterioração por dia por recurso"
          keyLabel="resourceId"
          rows={form.spoilagePerDayByResource}
          onChange={(rows) => set("spoilagePerDayByResource", rows)}
        />
        <KeyNumberListEditor
          label="Salário por profissão"
          keyLabel="professionId"
          rows={form.wageByProfession}
          onChange={(rows) => set("wageByProfession", rows)}
        />
        <KeyNumberListEditor
          label="Preço mínimo por recurso"
          keyLabel="resourceId"
          rows={form.priceFloor}
          onChange={(rows) => set("priceFloor", rows)}
        />
        <KeyNumberListEditor
          label="Preço máximo por recurso"
          keyLabel="resourceId"
          rows={form.priceCeiling}
          onChange={(rows) => set("priceCeiling", rows)}
        />
        <KeyNumberListEditor
          label="Demanda base por NPC por recurso"
          keyLabel="resourceId"
          rows={form.demandBaselinePerNpc}
          onChange={(rows) => set("demandBaselinePerNpc", rows)}
        />
        <KeyNumberListEditor
          label="Tipo de local por profissão"
          keyLabel="professionId"
          rows={form.locationTypeByProfession}
          onChange={(rows) => set("locationTypeByProfession", rows)}
        />

        <ObjectListEditor
          label="Receitas (por tipo de local)"
          fields={
            [
              { name: "locationTypeId", label: "tipo de local (id)", type: "number" },
              { name: "inputs", label: "insumos (resId:qtd,...)", type: "text" },
              { name: "outputs", label: "produtos (resId:qtd,...)", type: "text" },
              { name: "maxWorkersPerCycle", label: "trabalhadores máx./ciclo", type: "number" },
              { name: "requiresCellResource", label: "exige recurso de célula (id)", type: "nullable-number" },
            ] as const
          }
          rows={form.recipes}
          emptyRow={{ locationTypeId: 0, inputs: "", outputs: "", maxWorkersPerCycle: 1, requiresCellResource: null }}
          onChange={(rows) => set("recipes", rows)}
        />

        <ObjectListEditor
          label="Locais de trabalho"
          fields={
            [
              { name: "locationTypeId", label: "tipo de local (id)", type: "number" },
              { name: "x", label: "x", type: "number" },
              { name: "y", label: "y", type: "number" },
              { name: "maxVacancies", label: "vagas máx.", type: "number" },
              { name: "treasury", label: "tesouro", type: "number" },
              { name: "stock", label: "estoque (resId:qtd,...)", type: "text" },
              { name: "prices", label: "preços (resId:qtd,...)", type: "text" },
            ] as const
          }
          rows={form.workplaces}
          emptyRow={{ locationTypeId: 0, x: 0, y: 0, maxVacancies: 1, treasury: 0, stock: "", prices: "" }}
          onChange={(rows) => set("workplaces", rows)}
        />
      </fieldset>

      <fieldset>
        <legend>Cidades</legend>
        <label>
          Habilitadas:{" "}
          <input
            type="checkbox"
            checked={form.citiesEnabled}
            onChange={(e) => set("citiesEnabled", e.target.checked)}
          />
        </label>{" "}
        <label>
          Limiar de escassez de comida:{" "}
          <input
            type="number"
            step="any"
            value={form.foodShortageThreshold}
            onChange={(e) => set("foodShortageThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Limiar de escassez de moradia:{" "}
          <input
            type="number"
            step="any"
            value={form.housingShortageThreshold}
            onChange={(e) => set("housingShortageThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Limiar de escassez de segurança:{" "}
          <input
            type="number"
            step="any"
            value={form.securityShortageThreshold}
            onChange={(e) => set("securityShortageThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Taxa de emigração por déficit:{" "}
          <input
            type="number"
            step="any"
            value={form.emigrationRatePerDeficitUnit}
            onChange={(e) => set("emigrationRatePerDeficitUnit", Number(e.target.value))}
          />
        </label>
        <br />
        <label>
          Peso migração - emprego:{" "}
          <input
            type="number"
            step="any"
            value={form.migrationEmploymentWeight}
            onChange={(e) => set("migrationEmploymentWeight", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Peso migração - comida:{" "}
          <input
            type="number"
            step="any"
            value={form.migrationFoodWeight}
            onChange={(e) => set("migrationFoodWeight", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Peso migração - segurança:{" "}
          <input
            type="number"
            step="any"
            value={form.migrationSecurityWeight}
            onChange={(e) => set("migrationSecurityWeight", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Peso migração - laços familiares:{" "}
          <input
            type="number"
            step="any"
            value={form.migrationFamilyTiesWeight}
            onChange={(e) => set("migrationFamilyTiesWeight", Number(e.target.value))}
          />
        </label>
        <br />
        <label>
          Limiar fundação - concentração:{" "}
          <input
            type="number"
            step="any"
            value={form.foundingConcentrationThreshold}
            onChange={(e) => set("foundingConcentrationThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Limiar fundação - recurso:{" "}
          <input
            type="number"
            step="any"
            value={form.foundingResourceThreshold}
            onChange={(e) => set("foundingResourceThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Limiar fundação - rota:{" "}
          <input
            type="number"
            step="any"
            value={form.foundingRouteThreshold}
            onChange={(e) => set("foundingRouteThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Limiar fundação - defensabilidade:{" "}
          <input
            type="number"
            step="any"
            value={form.foundingDefensibilityThreshold}
            onChange={(e) => set("foundingDefensibilityThreshold", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Limiar fundação - liderança:{" "}
          <input
            type="number"
            step="any"
            value={form.foundingLeadershipThreshold}
            onChange={(e) => set("foundingLeadershipThreshold", Number(e.target.value))}
          />
        </label>
        <br />
        <label>
          Ticks de organização:{" "}
          <input
            type="number"
            value={form.organizationTicks}
            onChange={(e) => set("organizationTicks", Number(e.target.value))}
          />
        </label>{" "}
        <label>
          Ticks ociosos até elegível p/ materialização:{" "}
          <input
            type="number"
            value={form.materializationIdleTicksBeforeEligible}
            onChange={(e) => set("materializationIdleTicksBeforeEligible", Number(e.target.value))}
          />
        </label>

        <ObjectListEditor
          label="Receitas de construção"
          fields={
            [
              { name: "buildingTypeId", label: "tipo de construção (id)", type: "number" },
              { name: "inputs", label: "insumos (resId:qtd,...)", type: "text" },
              { name: "ticksToBuild", label: "ticks p/ construir", type: "number" },
              { name: "housingCapacityProvided", label: "capacidade de moradia", type: "number" },
            ] as const
          }
          rows={form.buildingRecipes}
          emptyRow={{ buildingTypeId: 0, inputs: "", ticksToBuild: 1, housingCapacityProvided: 0 }}
          onChange={(rows) => set("buildingRecipes", rows)}
        />

        <ObjectListEditor
          label="Cidades iniciais"
          fields={[
            { name: "x", label: "x", type: "number" },
            { name: "y", label: "y", type: "number" },
            { name: "foundedAtTick", label: "fundada no tick", type: "number" },
            { name: "count", label: "população", type: "number" },
            { name: "wealthSum", label: "riqueza total", type: "number" },
            { name: "healthSum", label: "saúde total", type: "number" },
          ]}
          rows={form.cities}
          emptyRow={{ x: 0, y: 0, foundedAtTick: 0, count: 0, wealthSum: 0, healthSum: 0 }}
          onChange={(rows) => set("cities", rows)}
        />
      </fieldset>

      <fieldset>
        <legend>Dinâmica (opcional)</legend>
        <ObjectListEditor
          label="Vieses de profissão"
          fields={[
            { name: "professionId", label: "profissão (id)", type: "number" },
            { name: "weight", label: "peso", type: "number" },
            { name: "name", label: "nome (opcional)", type: "text" },
          ]}
          rows={form.professionBiases}
          emptyRow={{ professionId: 0, weight: 1, name: "" }}
          onChange={(rows) => set("professionBiases", rows)}
        />
        <ObjectListEditor
          label="Vieses de habilidade"
          fields={[
            { name: "skillId", label: "habilidade (id)", type: "number" },
            { name: "weight", label: "peso", type: "number" },
            { name: "name", label: "nome (opcional)", type: "text" },
          ]}
          rows={form.skillBiases}
          emptyRow={{ skillId: 0, weight: 1, name: "" }}
          onChange={(rows) => set("skillBiases", rows)}
        />
        <ObjectListEditor
          label="Regras de transformação"
          fields={
            [
              {
                name: "kind",
                label: "tipo",
                type: "select",
                options: ["Emerge", "Merge", "Split", "Disappear"],
              },
              { name: "sourceProfessionIds", label: "profissões origem (ids, csv)", type: "text" },
              { name: "targetProfessionIds", label: "profissões destino (ids, csv)", type: "text" },
              { name: "triggerTick", label: "tick de disparo (vazio=imediato)", type: "nullable-number" },
            ] as const
          }
          rows={form.transformationRules}
          emptyRow={{ kind: "Emerge", sourceProfessionIds: "", targetProfessionIds: "", triggerTick: null }}
          onChange={(rows) => set("transformationRules", rows)}
        />
      </fieldset>

      <button type="submit" disabled={status === "submitting"}>
        {status === "submitting" ? "Criando…" : "Criar mundo"}
      </button>
      {error && <p role="alert">{error}</p>}
    </form>
  );
}
