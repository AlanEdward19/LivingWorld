import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { App } from "../src/App";

describe("App", () => {
  it("mounts and renders the World View by default", () => {
    render(<App />);
    expect(screen.getByTestId("world-view")).toBeInTheDocument();
    expect(screen.getByTestId("breadcrumb")).toBeInTheDocument();
    expect(screen.getByTestId("search-bar")).toBeInTheDocument();
  });
});
