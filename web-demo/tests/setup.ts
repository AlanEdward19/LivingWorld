import "@testing-library/jest-dom/vitest";
import { vi } from "vitest";

// jsdom não implementa um contexto de canvas de verdade (sem o pacote opcional `canvas`), então
// a `Application` REAL do Pixi (AD-020) quebra em qualquer teste que monte `SettlementStage`.
// Mock mínimo, porém com relação pai/filho e listeners de verdade — o suficiente pra exercitar
// a lógica do componente (quem tá dentro de qual container, cliques) sem WebGL/Canvas real.
// `__runTick`/`__resetPixiMock` são extras só-de-teste, usados por `tests/render/SettlementStage.test.tsx`.
vi.mock("pixi.js", () => {
  class FakeDisplayObject {
    parent: FakeDisplayObject | null = null;
    children: FakeDisplayObject[] = [];
    alpha = 1;
    eventMode = "auto";
    cursor = "default";
    hitArea: unknown = null;
    private listeners = new Map<string, Array<(...args: unknown[]) => void>>();
    private _position = {
      x: 0,
      y: 0,
      set(x: number, y: number = x) {
        this.x = x;
        this.y = y;
      },
    };
    private _scale = {
      x: 1,
      y: 1,
      set(x: number, y: number = x) {
        this.x = x;
        this.y = y;
      },
    };

    get position() {
      return this._position;
    }
    get scale() {
      return this._scale;
    }
    addChild(...items: FakeDisplayObject[]) {
      for (const item of items) {
        if (item.parent && item.parent !== this) {
          item.parent.children = item.parent.children.filter((child) => child !== item);
        }
        item.parent = this;
        if (!this.children.includes(item)) this.children.push(item);
      }
      return items[0];
    }
    removeChildren() {
      for (const child of this.children) child.parent = null;
      this.children = [];
    }
    on(event: string, handler: (...args: unknown[]) => void) {
      const list = this.listeners.get(event) ?? [];
      list.push(handler);
      this.listeners.set(event, list);
      return this;
    }
    emit(event: string, ...args: unknown[]) {
      for (const handler of this.listeners.get(event) ?? []) handler(...args);
    }
  }

  class Container extends FakeDisplayObject {}

  class Graphics extends FakeDisplayObject {
    rect() {
      return this;
    }
    roundRect() {
      return this;
    }
    fill() {
      return this;
    }
    stroke() {
      return this;
    }
    circle() {
      return this;
    }
    ellipse() {
      return this;
    }
    moveTo() {
      return this;
    }
    lineTo() {
      return this;
    }
    clear() {
      return this;
    }
  }

  class Sprite extends FakeDisplayObject {
    anchor = { set: () => {} };
    texture: unknown;
    constructor(texture?: unknown) {
      super();
      this.texture = texture;
    }
  }

  class Texture {
    static from(_src: unknown) {
      return new Texture();
    }
  }

  let tickers: Array<() => void> = [];
  let createdApplications: InstanceType<typeof Application>[] = [];

  class Application {
    stage = new Container();
    canvas = document.createElement("canvas");
    screen = { width: 800, height: 600 };
    ticker = {
      add: (fn: () => void) => {
        tickers.push(fn);
      },
      remove: (fn: () => void) => {
        tickers = tickers.filter((t) => t !== fn);
      },
    };
    constructor() {
      createdApplications.push(this);
    }
    async init() {}
    destroy() {}
  }

  return {
    Application,
    Container,
    Graphics,
    Sprite,
    Texture,
    __runTick: () => {
      for (const fn of tickers) fn();
    },
    __lastApplication: () => createdApplications[createdApplications.length - 1],
    __resetPixiMock: () => {
      tickers = [];
      createdApplications = [];
    },
  };
});
