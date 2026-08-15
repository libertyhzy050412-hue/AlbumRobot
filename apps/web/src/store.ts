import { create } from "zustand";
import type {
  AlbumPeriod,
  AlbumSelection,
  AlbumSort,
  StatsPeriod,
  TabKey,
} from "./domain";

type Drilldown = {
  memberName: string;
  period: StatsPeriod;
};

type UiState = {
  activeTab: TabKey;
  albumPeriod: AlbumPeriod;
  albumSort: AlbumSort;
  searchMode: boolean;
  searchQuery: string;
  statsPeriod: StatsPeriod;
  selectedAlbum: AlbumSelection | null;
  drilldown: Drilldown | null;
  dateNavigatorOpen: boolean;
  scrollPositions: Record<TabKey, number>;
  setActiveTab: (tab: TabKey) => void;
  setAlbumPeriod: (period: AlbumPeriod) => void;
  setAlbumSort: (sort: AlbumSort) => void;
  enterSearch: () => void;
  setSearchQuery: (query: string) => void;
  cancelSearch: () => void;
  startMemberDrilldown: (memberName: string, period: StatsPeriod) => void;
  setStatsPeriod: (period: StatsPeriod) => void;
  selectAlbum: (selection: AlbumSelection | null) => void;
  setDateNavigatorOpen: (open: boolean) => void;
  saveScroll: (tab: TabKey, position: number) => void;
};

export const useUiStore = create<UiState>((set, get) => ({
  activeTab: "albums",
  albumPeriod: "all",
  albumSort: "recent",
  searchMode: false,
  searchQuery: "",
  statsPeriod: "week",
  selectedAlbum: null,
  drilldown: null,
  dateNavigatorOpen: false,
  scrollPositions: { albums: 0, feed: 0, stats: 0 },
  setActiveTab: (tab) => {
    const drilldown = get().drilldown;
    set({
      activeTab: tab,
      dateNavigatorOpen: false,
      ...(drilldown && tab !== "albums"
        ? { drilldown: null, searchMode: false, searchQuery: "" }
        : {}),
    });
  },
  setAlbumPeriod: (albumPeriod) => set({ albumPeriod }),
  setAlbumSort: (albumSort) => set({ albumSort }),
  enterSearch: () => set({ searchMode: true, drilldown: null }),
  setSearchQuery: (searchQuery) => set({ searchQuery }),
  cancelSearch: () => {
    const drilldown = get().drilldown;
    if (drilldown) {
      set({
        activeTab: "stats",
        statsPeriod: drilldown.period,
        drilldown: null,
        searchMode: false,
        searchQuery: "",
      });
      return;
    }
    set({ searchMode: false, searchQuery: "" });
  },
  startMemberDrilldown: (memberName, period) =>
    set({
      activeTab: "albums",
      searchMode: true,
      searchQuery: memberName,
      drilldown: { memberName, period },
    }),
  setStatsPeriod: (statsPeriod) => set({ statsPeriod }),
  selectAlbum: (selectedAlbum) => set({ selectedAlbum }),
  setDateNavigatorOpen: (dateNavigatorOpen) => set({ dateNavigatorOpen }),
  saveScroll: (tab, position) =>
    set((state) => ({
      scrollPositions: { ...state.scrollPositions, [tab]: position },
    })),
}));
