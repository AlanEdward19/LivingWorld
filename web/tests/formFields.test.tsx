import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { KeyNumberListEditor, ObjectListEditor, type FieldSpec } from "../src/components/formFields";

describe("KeyNumberListEditor with a catalog", () => {
  it("resolves the key to its catalog name by default", () => {
    render(
      <KeyNumberListEditor
        label="Salário por profissão"
        keyLabel="professionId"
        rows={[{ key: "3", value: 10 }]}
        onChange={() => {}}
        labels={{ 3: "Ferreiro" }}
      />,
    );
    expect(screen.getByLabelText("Salário por profissão-professionId-0")).toHaveValue("3");
    expect(screen.getByText(/Ferreiro/)).toBeInTheDocument();
  });

  it("shows the id itself as an unlabeled option when it has no catalog entry", () => {
    render(
      <KeyNumberListEditor
        label="Salário por profissão"
        keyLabel="professionId"
        rows={[{ key: "99", value: 10 }]}
        onChange={() => {}}
        labels={{ 3: "Ferreiro" }}
      />,
    );
    expect(screen.getByText(/99 \(sem nome no catálogo\)/)).toBeInTheDocument();
  });

  it("falls back to a raw text input when no catalog is given (unchanged behavior)", () => {
    render(
      <KeyNumberListEditor
        label="Capacidade"
        keyLabel="resourceId,locationTypeId"
        rows={[{ key: "1,2", value: 10 }]}
        onChange={() => {}}
      />,
    );
    expect(screen.getByLabelText("Capacidade-resourceId,locationTypeId-0")).toHaveValue("1,2");
    expect(screen.queryByRole("checkbox")).not.toBeInTheDocument();
  });

  it("the raw-ids toggle switches the select back to a plain text input", () => {
    render(
      <KeyNumberListEditor
        label="Salário por profissão"
        keyLabel="professionId"
        rows={[{ key: "3", value: 10 }]}
        onChange={() => {}}
        labels={{ 3: "Ferreiro" }}
      />,
    );
    fireEvent.click(screen.getByRole("checkbox", { name: /IDs crus/ }));
    expect(screen.getByLabelText("Salário por profissão-professionId-0")).toHaveValue("3");
    expect(screen.queryByText(/Ferreiro/)).not.toBeInTheDocument();
  });
});

interface Row {
  professionId: number;
  weight: number;
}

describe("ObjectListEditor with a catalog", () => {
  const fields: readonly FieldSpec<Row>[] = [
    { name: "professionId", label: "profissão", type: "number", labels: { 3: "Ferreiro" } },
    { name: "weight", label: "peso", type: "number" },
  ];

  it("resolves a labeled numeric field to its catalog name by default", () => {
    render(
      <ObjectListEditor label="Vieses" fields={fields} rows={[{ professionId: 3, weight: 1 }]} emptyRow={{ professionId: 0, weight: 1 }} onChange={() => {}} />,
    );
    expect(screen.getByText(/Ferreiro/)).toBeInTheDocument();
    // "peso" has no catalog — stays a plain number input, unaffected by the other field's toggle.
    expect(screen.getByLabelText("Vieses-peso-0")).toHaveValue(1);
  });

  it("changing the labeled select updates the row with the numeric id", () => {
    const onChange = vi.fn();
    render(
      <ObjectListEditor label="Vieses" fields={fields} rows={[{ professionId: 3, weight: 1 }]} emptyRow={{ professionId: 0, weight: 1 }} onChange={onChange} />,
    );
    fireEvent.change(screen.getByLabelText("Vieses-profissão-0"), { target: { value: "3" } });
    expect(onChange).toHaveBeenCalledWith([{ professionId: 3, weight: 1 }]);
  });

  it("the raw-ids toggle applies across the whole list, not just the labeled field", () => {
    render(
      <ObjectListEditor label="Vieses" fields={fields} rows={[{ professionId: 3, weight: 1 }]} emptyRow={{ professionId: 0, weight: 1 }} onChange={() => {}} />,
    );
    fireEvent.click(screen.getByRole("checkbox", { name: /IDs crus/ }));
    expect(screen.getByLabelText("Vieses-profissão-0")).toHaveValue(3);
    expect(screen.queryByText(/Ferreiro/)).not.toBeInTheDocument();
  });
});
