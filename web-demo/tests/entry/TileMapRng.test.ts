import { describe, expect, it } from "vitest";
import { rngDoubleAt } from "../../src/entry/creator/TileMapPreview";

// Ground truth: a standalone C# program running the REAL `WorldRng`
// (src/LivingWorld.Domain/WorldRng.cs) for seed 123456789012345, printing 12 sequential
// `NextDouble()` calls with "R" (round-trip) formatting. If this ever fails, the frontend's
// map preview has silently drifted from what the backend would actually generate for the same
// seed — treat it as a correctness bug, not a snapshot to casually update.
const SEED = 123456789012345n;
const EXPECTED = [
  0.8451854762395156, 0.7278810839370495, 0.09147965220425491, 0.05727885303695546, 0.6092344409614054,
  0.0760861427975037, 0.9594227383722655, 0.77905553594224, 0.885620885036372, 0.3423411102610786, 0.4141015885230642,
  0.7901822964205419,
];

describe("TileMapPreview RNG — faithful to the real backend WorldRng", () => {
  it("matches the real C# WorldRng bit-for-bit for the same seed and draw sequence", () => {
    const actual = EXPECTED.map((_, i) => rngDoubleAt(SEED, i));
    expect(actual).toEqual(EXPECTED);
  });

  it("is a pure function of (seed, drawIndex) — jumping straight to draw i matches sequential replay", () => {
    // Sanity check for the O(1)-jump trick used to sample far-apart cells cheaply.
    expect(rngDoubleAt(SEED, 7)).toBe(EXPECTED[7]);
    expect(rngDoubleAt(SEED, 0)).toBe(EXPECTED[0]);
  });
});
