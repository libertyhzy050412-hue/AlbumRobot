import { describe, expect, it } from "vitest";
import {
  feedDateKey,
  feedMonthKey,
  feedMonthLabel,
  formatShareTime,
} from "./format";

describe("UTC+8 display helpers", () => {
  it("keeps date-only values on their recorded day", () => {
    expect(feedDateKey("2026-08-15")).toBe("2026-08-15");
  });

  it("groups instants by their Asia/Shanghai calendar day", () => {
    expect(feedDateKey("2026-08-14T17:30:00.000Z")).toBe("2026-08-15");
  });

  it("formats precise share time in UTC+8", () => {
    expect(formatShareTime("2026-08-15T07:42:00.000Z", "second")).toBe("15:42");
  });

  it("uses stable month keys and Chinese labels", () => {
    expect(feedMonthKey("2026-08-14T17:30:00.000Z")).toBe("2026-08");
    expect(feedMonthLabel("2026-08-14T17:30:00.000Z")).toBe("2026年8月");
  });
});
