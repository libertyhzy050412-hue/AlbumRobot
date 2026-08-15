import { describe, expect, it } from "vitest";
import { businessPeriodStart } from "../src/business-time";

describe("UTC+8 business periods", () => {
  const now = new Date("2026-08-15T08:30:00.000Z");

  it("starts a natural week on Monday midnight UTC+8", () => {
    expect(businessPeriodStart("week", now)).toBe("2026-08-09T16:00:00.000Z");
  });

  it("starts a natural month at local midnight", () => {
    expect(businessPeriodStart("month", now)).toBe("2026-07-31T16:00:00.000Z");
  });

  it("starts a natural year at local midnight", () => {
    expect(businessPeriodStart("year", now)).toBe("2025-12-31T16:00:00.000Z");
  });
});
