import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { SpaceTransition } from "../src/components/SpaceTransition";

describe("SpaceTransition", () => {
  it("applies the fade/zoom transition class", () => {
    const { container } = render(
      <SpaceTransition spaceKey="world">
        <p>content</p>
      </SpaceTransition>,
    );

    expect(container.querySelector(".space-transition")).toBeInTheDocument();
  });

  it("remounts the wrapper (restarting the animation) when the space key changes", () => {
    const { container, rerender } = render(
      <SpaceTransition spaceKey="world">
        <p>content</p>
      </SpaceTransition>,
    );
    const firstNode = container.querySelector(".space-transition");

    rerender(
      <SpaceTransition spaceKey="city:city-1">
        <p>content</p>
      </SpaceTransition>,
    );
    const secondNode = container.querySelector(".space-transition");

    expect(secondNode).not.toBe(firstNode);
  });

  it("does not remount when the space key stays the same", () => {
    const { container, rerender } = render(
      <SpaceTransition spaceKey="world">
        <p>first</p>
      </SpaceTransition>,
    );
    const firstNode = container.querySelector(".space-transition");

    rerender(
      <SpaceTransition spaceKey="world">
        <p>second</p>
      </SpaceTransition>,
    );
    const secondNode = container.querySelector(".space-transition");

    expect(secondNode).toBe(firstNode);
  });
});
