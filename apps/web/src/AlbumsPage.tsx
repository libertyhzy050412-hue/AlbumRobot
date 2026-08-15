import { keepPreviousData, useQuery } from "@tanstack/react-query";
import {
  AnimatePresence,
  LayoutGroup,
  motion,
  useReducedMotion,
} from "motion/react";
import { useEffect, useRef } from "react";
import { ApiError, fetchAlbums } from "./api";
import { Artwork } from "./Artwork";
import type { AlbumSelection } from "./domain";
import { useCompactHeader } from "./hooks";
import { Icon } from "./icons";
import { motionTokens } from "./motion";
import { PageHeader } from "./PageHeader";
import { useUiStore } from "./store";

const periodOptions = [
  { value: "all", label: "全部" },
  { value: "month", label: "本月" },
  { value: "year", label: "今年" },
] as const;

export function AlbumsPage({
  authenticated,
  onOpenAlbum,
  onUnauthorized,
}: {
  authenticated: boolean;
  onOpenAlbum: (selection: AlbumSelection) => void;
  onUnauthorized: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const reducedMotion = useReducedMotion();
  const {
    albumPeriod,
    albumSort,
    searchMode,
    searchQuery,
    drilldown,
    selectedAlbum,
    setAlbumPeriod,
    setAlbumSort,
    enterSearch,
    setSearchQuery,
    cancelSearch,
  } = useUiStore();
  const compact = useCompactHeader(searchMode);
  const effectivePeriod = drilldown?.period ?? albumPeriod;
  const albums = useQuery({
    queryKey: ["albums", searchQuery, effectivePeriod, albumSort],
    queryFn: () =>
      fetchAlbums({
        search: searchQuery,
        period: effectivePeriod,
        sort: albumSort,
      }),
    enabled: authenticated,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  useEffect(() => {
    if (searchMode) inputRef.current?.focus();
  }, [searchMode]);

  useEffect(() => {
    if (albums.error instanceof ApiError && albums.error.status === 401)
      onUnauthorized();
  }, [albums.error, onUnauthorized]);

  const changePeriod = (period: typeof albumPeriod) => {
    setAlbumPeriod(period);
    window.scrollTo({ top: 0, behavior: reducedMotion ? "auto" : "smooth" });
  };

  const changeSort = (sort: typeof albumSort) => {
    setAlbumSort(sort);
    window.scrollTo({ top: 0, behavior: reducedMotion ? "auto" : "smooth" });
  };

  return (
    <div className="page page-albums">
      <PageHeader
        title="专辑"
        subtitle="群里分享过的声音，都留在这里。"
        compact={compact}
      />

      <section className={`album-tools ${searchMode ? "is-searching" : ""}`}>
        <div className="search-row" role="search">
          <motion.div
            className="search-field"
            layout
            transition={motionTokens.responsiveSpring}
          >
            <Icon name="search" />
            {searchMode ? (
              <input
                ref={inputRef}
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                placeholder="专辑、艺术家或分享者"
                aria-label="搜索专辑、艺术家或分享者"
              />
            ) : (
              <button type="button" onClick={enterSearch}>
                搜索专辑、艺术家或分享者
              </button>
            )}
            {searchMode && searchQuery ? (
              <button
                type="button"
                className="search-clear"
                aria-label="清空搜索"
                onClick={() => setSearchQuery("")}
              >
                <Icon name="close" />
              </button>
            ) : null}
          </motion.div>
          <AnimatePresence>
            {searchMode ? (
              <motion.button
                type="button"
                className="search-cancel"
                onClick={cancelSearch}
                initial={{ opacity: 0, width: 0 }}
                animate={{ opacity: 1, width: "auto" }}
                exit={{ opacity: 0, width: 0 }}
                transition={motionTokens.enter}
              >
                取消
              </motion.button>
            ) : null}
          </AnimatePresence>
        </div>

        <AnimatePresence initial={false}>
          {!searchMode ? (
            <motion.div
              className="browse-controls"
              initial={{ opacity: 0, y: -6 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -6 }}
              transition={motionTokens.enter}
            >
              <div className="segmented-control" aria-label="时间范围">
                {periodOptions.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    className={
                      albumPeriod === option.value ? "is-selected" : ""
                    }
                    onClick={() => changePeriod(option.value)}
                  >
                    {albumPeriod === option.value ? (
                      <motion.span
                        className="segment-selection"
                        layoutId="album-period-selection"
                        transition={motionTokens.responsiveSpring}
                      />
                    ) : null}
                    <span>{option.label}</span>
                  </button>
                ))}
              </div>
              <label className="sort-control">
                <span className="sr-only">专辑排序</span>
                <select
                  value={albumSort}
                  onChange={(event) =>
                    changeSort(event.target.value as typeof albumSort)
                  }
                >
                  <option value="recent">最近分享</option>
                  <option value="first">首次收录</option>
                  <option value="popular">分享最多</option>
                </select>
                <span aria-hidden="true">⌄</span>
              </label>
            </motion.div>
          ) : null}
        </AnimatePresence>

        {drilldown ? (
          <motion.p
            className="drilldown-context"
            initial={{ opacity: 0, y: -4 }}
            animate={{ opacity: 1, y: 0 }}
            transition={motionTokens.enter}
          >
            {drilldown.memberName} ·
            {drilldown.period === "week"
              ? " 本周"
              : drilldown.period === "month"
                ? " 本月"
                : " 本年"}
          </motion.p>
        ) : null}
      </section>

      <LayoutGroup id="album-grid">
        <motion.section className="album-grid" layout aria-label="专辑库">
          {albums.isLoading ? (
            Array.from({ length: 8 }, (_, index) => (
              <div className="album-skeleton" key={index} aria-hidden="true">
                <span />
                <i />
                <i />
              </div>
            ))
          ) : albums.isError ? (
            <div className="page-message" role="alert">
              <strong>暂时无法读取专辑库</strong>
              <button type="button" onClick={() => void albums.refetch()}>
                重试
              </button>
            </div>
          ) : albums.data?.length ? (
            <AnimatePresence mode="popLayout" initial={false}>
              {albums.data.map((album, index) => {
                const transitionKey = `grid-${album.netease_album_id}`;
                const selected =
                  selectedAlbum?.album.netease_album_id ===
                  album.netease_album_id;
                return (
                  <motion.button
                    type="button"
                    className={`album-card ${selected ? "is-selected" : ""}`}
                    key={album.netease_album_id}
                    layout
                    initial={{ opacity: 0, scale: 0.975 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.965 }}
                    transition={{
                      ...motionTokens.responsiveSpring,
                      delay: reducedMotion ? 0 : Math.min(index * 0.015, 0.12),
                    }}
                    whileTap={reducedMotion ? undefined : { scale: 0.975 }}
                    onClick={() => onOpenAlbum({ album, transitionKey })}
                  >
                    <Artwork
                      album={album}
                      layoutId={reducedMotion ? undefined : transitionKey}
                    />
                    <span className="album-card-copy">
                      <strong>{album.title}</strong>
                      <span>{album.artist}</span>
                    </span>
                  </motion.button>
                );
              })}
            </AnimatePresence>
          ) : (
            <div className="page-message empty-state">
              <strong>
                {searchQuery ? "没有匹配的专辑" : "还没有同步的专辑"}
              </strong>
              <span>
                {searchQuery
                  ? "试试专辑名、艺术家或分享者昵称。"
                  : "在桌面端导入 QCE JSON 并上传后，专辑会显示在这里。"}
              </span>
            </div>
          )}
        </motion.section>
      </LayoutGroup>
    </div>
  );
}
