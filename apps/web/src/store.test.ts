import { beforeEach, describe, expect, it } from "vitest";
import { useUiStore } from "./store";

beforeEach(() => {
  useUiStore.setState({
    activeTab: "albums",
    searchMode: false,
    searchQuery: "",
    statsPeriod: "week",
    drilldown: null,
  });
});

describe("member ranking drilldown", () => {
  it("opens an album search scoped to the selected stats period", () => {
    useUiStore.getState().startMemberDrilldown("林间", "month");

    expect(useUiStore.getState()).toMatchObject({
      activeTab: "albums",
      searchMode: true,
      searchQuery: "林间",
      drilldown: { memberName: "林间", period: "month" },
    });
  });

  it("returns to stats when the drilldown search is cancelled", () => {
    useUiStore.getState().startMemberDrilldown("林间", "year");
    useUiStore.getState().cancelSearch();

    expect(useUiStore.getState()).toMatchObject({
      activeTab: "stats",
      statsPeriod: "year",
      searchMode: false,
      searchQuery: "",
      drilldown: null,
    });
  });
});
