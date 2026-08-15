import { useQueryClient } from "@tanstack/react-query";
import {
  AnimatePresence,
  LayoutGroup,
  motion,
  useReducedMotion,
} from "motion/react";
import { useCallback, useEffect, useRef, useState } from "react";
import { AlbumDetailSheet } from "./AlbumDetailSheet";
import { AlbumsPage } from "./AlbumsPage";
import { AuthOverlay, type AuthState } from "./AuthOverlay";
import type { AlbumSelection, TabKey } from "./domain";
import { FeedPage } from "./FeedPage";
import { motionTokens } from "./motion";
import { Navigation } from "./Navigation";
import { StatsPage } from "./StatsPage";
import { useUiStore } from "./store";

const detailHistoryLayer = "albumrobot-album-detail";
const tabOrder: TabKey[] = ["albums", "feed", "stats"];

export default function App() {
  const queryClient = useQueryClient();
  const reducedMotion = useReducedMotion();
  const previousTab = useRef<TabKey>("albums");
  const [authState, setAuthState] = useState<AuthState>("checking");
  const [authError, setAuthError] = useState("");
  const {
    activeTab,
    selectedAlbum,
    scrollPositions,
    setActiveTab,
    selectAlbum,
    saveScroll,
  } = useUiStore();

  const requireAuthentication = useCallback(() => {
    setAuthState("required");
    setAuthError("访问授权已失效，请重新输入群共享密码。");
    queryClient.clear();
    selectAlbum(null);
  }, [queryClient, selectAlbum]);

  useEffect(() => {
    const checkSession = async () => {
      try {
        const response = await fetch("/api/auth/session", {
          credentials: "same-origin",
        });
        if (response.ok) {
          setAuthState("authenticated");
          return;
        }
        setAuthState("required");
        if (response.status !== 401)
          setAuthError("服务暂时不可用，请稍后重试。");
      } catch {
        setAuthState("required");
        setAuthError("无法连接 AlbumRobot，请检查网络后重试。");
      }
    };
    void checkSession();
  }, []);

  useEffect(() => {
    const handlePopState = () => selectAlbum(null);
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [selectAlbum]);

  const openAlbum = useCallback(
    (selection: AlbumSelection) => {
      if (!selectedAlbum) {
        window.history.pushState(
          { ...window.history.state, albumrobotLayer: detailHistoryLayer },
          "",
        );
      }
      selectAlbum(selection);
    },
    [selectAlbum, selectedAlbum],
  );

  const closeAlbum = useCallback(() => {
    if (window.history.state?.albumrobotLayer === detailHistoryLayer) {
      window.history.back();
    } else {
      selectAlbum(null);
    }
  }, [selectAlbum]);

  const switchTab = (tab: TabKey) => {
    if (tab === activeTab || selectedAlbum) return;
    saveScroll(activeTab, window.scrollY);
    previousTab.current = activeTab;
    setActiveTab(tab);
    requestAnimationFrame(() => {
      window.scrollTo({ top: scrollPositions[tab], behavior: "auto" });
    });
  };

  const direction =
    tabOrder.indexOf(activeTab) >= tabOrder.indexOf(previousTab.current)
      ? 1
      : -1;
  const authenticated = authState === "authenticated";

  return (
    <LayoutGroup id="albumrobot-shell">
      <div
        className={`app-shell ${selectedAlbum ? "detail-is-open" : ""}`}
        aria-hidden={!authenticated}
      >
        <main className="page-stage">
          <AnimatePresence mode="popLayout" initial={false}>
            <motion.div
              className="page-transition"
              key={activeTab}
              initial={
                reducedMotion
                  ? { opacity: 0 }
                  : { opacity: 0, x: 22 * direction }
              }
              animate={{ opacity: 1, x: 0 }}
              exit={
                reducedMotion
                  ? { opacity: 0 }
                  : { opacity: 0, x: -14 * direction }
              }
              transition={motionTokens.enter}
            >
              {activeTab === "albums" ? (
                <AlbumsPage
                  authenticated={authenticated}
                  onOpenAlbum={openAlbum}
                  onUnauthorized={requireAuthentication}
                />
              ) : activeTab === "feed" ? (
                <FeedPage
                  authenticated={authenticated}
                  onOpenAlbum={openAlbum}
                  onUnauthorized={requireAuthentication}
                />
              ) : (
                <StatsPage
                  authenticated={authenticated}
                  onUnauthorized={requireAuthentication}
                />
              )}
            </motion.div>
          </AnimatePresence>
        </main>

        <Navigation activeTab={activeTab} onSelect={switchTab} />
      </div>

      <AlbumDetailSheet
        selection={selectedAlbum}
        onClose={closeAlbum}
        onUnauthorized={requireAuthentication}
      />

      <AuthOverlay
        authState={authState}
        initialError={authError}
        onAuthenticated={() => {
          setAuthError("");
          setAuthState("authenticated");
          void queryClient.invalidateQueries();
        }}
      />
    </LayoutGroup>
  );
}
