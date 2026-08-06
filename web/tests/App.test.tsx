import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";
import { App } from "../src/App";
import type { GlobalSnapshot, VisualSnapshotEnvelope } from "../src/types";
import { VisualScopeKind, ViewerMode } from "../src/types";

class MockWebSocket {
  static instances: MockWebSocket[] = [];
  onopen: (() => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onerror: (() => void) | null = null;
  onclose: (() => void) | null = null;
  url: string;

  constructor(url: string) {
    this.url = url;
    MockWebSocket.instances.push(this);
  }

  close() {}
}

function worldEnvelope(): VisualSnapshotEnvelope<GlobalSnapshot> {
  return {
    scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: "world", sequence: 0 },
    activeLayers: [],
    payload: {
      width: 10,
      height: 10,
      cities: [{ id: { value: "city-1" }, location: { x: 0, y: 0 }, population: 10 }],
      externalNpcs: [],
      activeEvents: [],
      layers: {} as GlobalSnapshot["layers"],
    },
  };
}

function cityEnvelope(): VisualSnapshotEnvelope<Record<string, unknown>> {
  return {
    scope: { kind: VisualScopeKind.City, refId: "city-1", scopeKey: "city:city-1" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: "city:city-1", sequence: 0 },
    activeLayers: [],
    payload: {
      id: { value: "city-1" },
      location: { x: 0, y: 0 },
      aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
      residents: [],
      buildings: [],
      layers: {},
    },
  };
}

describe("App", () => {
  beforeEach(() => {
    MockWebSocket.instances = [];
    vi.stubGlobal("WebSocket", MockWebSocket as unknown as typeof WebSocket);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders the world map after the first realtime frame, then drills into a city on click", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    const socket = MockWebSocket.instances[0];
    act(() => socket.onmessage?.({ data: JSON.stringify(worldEnvelope()) }));

    await screen.findByTestId("world-map-view");

    const canvas = screen.getByTestId("grid-canvas") as HTMLCanvasElement;
    vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({
      left: 0,
      top: 0,
      width: canvas.width,
      height: canvas.height,
      right: canvas.width,
      bottom: canvas.height,
      x: 0,
      y: 0,
      toJSON: () => "",
    });
    fireEvent.click(canvas, { clientX: 8, clientY: 8 }); // city at (0,0), zoom 16 -> center (8,8)
    expect(screen.getByText(/População: 10/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Entrar" }));

    await waitFor(() => expect(MockWebSocket.instances.length).toBe(2));
    const citySocket = MockWebSocket.instances[1];
    act(() => citySocket.onmessage?.({ data: JSON.stringify(cityEnvelope()) }));

    await screen.findByTestId("city-view");
  });

  it("starts on the start menu and navigates to settings and back", () => {
    render(<App />);

    expect(screen.getByTestId("start-menu")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Configurações" }));
    expect(screen.getByTestId("settings-view")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "← menu" }));
    expect(screen.getByTestId("start-menu")).toBeInTheDocument();
  });
});
