import { describe, expect, it } from "vitest";
import { artworkHue } from "./Artwork";

describe("fallback artwork color", () => {
  it("is deterministic and stays within the CSS hue range", () => {
    expect(artworkHue("980004")).toBe(artworkHue("980004"));
    expect(artworkHue("980004")).toBeGreaterThanOrEqual(0);
    expect(artworkHue("980004")).toBeLessThan(360);
  });

  it("visually separates sequential album identifiers", () => {
    const colors = new Set(
      Array.from({ length: 12 }, (_, index) => artworkHue(`${980000 + index}`)),
    );
    expect(colors.size).toBeGreaterThanOrEqual(10);
  });
});
