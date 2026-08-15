import { useQuery } from "@tanstack/react-query";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useEffect, useMemo, useState } from "react";
import { ApiError, fetchFeed } from "./api";
import { Artwork } from "./Artwork";
import type { AlbumSelection, FeedItem } from "./domain";
import {
  feedDateKey,
  feedDateLabel,
  feedMonthKey,
  feedMonthLabel,
  formatShareTime,
} from "./format";
import { useCompactHeader } from "./hooks";
import { Icon } from "./icons";
import { motionTokens } from "./motion";
import { PageHeader } from "./PageHeader";
import { useUiStore } from "./store";

type FeedGroup = { key: string; label: string; items: FeedItem[] };

function groupFeed(items: FeedItem[]): FeedGroup[] {
  const groups = new Map<string, FeedGroup>();
  for (const item of items) {
    const key = feedDateKey(item.shared_at);
    const group = groups.get(key) ?? {
      key,
      label: feedDateLabel(item.shared_at),
      items: [],
    };
    group.items.push(item);
    groups.set(key, group);
  }
  return [...groups.values()];
}

export function FeedPage({
  authenticated,
  onOpenAlbum,
  onUnauthorized,
}: {
  authenticated: boolean;
  onOpenAlbum: (selection: AlbumSelection) => void;
  onUnauthorized: () => void;
}) {
  const reducedMotion = useReducedMotion();
  const compact = useCompactHeader();
  const [navigatorYear, setNavigatorYear] = useState<string | null>(null);
  const [navigatorDirection, setNavigatorDirection] = useState<1 | -1>(1);
  const { dateNavigatorOpen, setDateNavigatorOpen } = useUiStore();
  const feed = useQuery({
    queryKey: ["feed"],
    queryFn: fetchFeed,
    enabled: authenticated,
    staleTime: 20_000,
  });
  const groups = useMemo(() => groupFeed(feed.data ?? []), [feed.data]);
  const months = useMemo(() => {
    const values = new Map<string, string>();
    for (const item of feed.data ?? []) {
      values.set(feedMonthKey(item.shared_at), feedMonthLabel(item.shared_at));
    }
    return [...values.entries()];
  }, [feed.data]);
  const years = useMemo(() => {
    const values = new Map<string, Array<[string, string]>>();
    for (const month of months) {
      const year = month[0].slice(0, 4);
      values.set(year, [...(values.get(year) ?? []), month]);
    }
    return [...values.entries()];
  }, [months]);

  useEffect(() => {
    if (feed.error instanceof ApiError && feed.error.status === 401)
      onUnauthorized();
  }, [feed.error, onUnauthorized]);

  useEffect(() => {
    if (!dateNavigatorOpen) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setDateNavigatorOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [dateNavigatorOpen, setDateNavigatorOpen]);

  const historyButton = (
    <button
      type="button"
      className="round-icon-button"
      aria-label="按时间定位"
      onClick={() => {
        setNavigatorDirection(1);
        setNavigatorYear(null);
        setDateNavigatorOpen(true);
      }}
    >
      <Icon name="calendar" />
    </button>
  );

  const jumpToMonth = (month: string) => {
    setDateNavigatorOpen(false);
    requestAnimationFrame(() => {
      document.getElementById(`feed-month-${month}`)?.scrollIntoView({
        behavior: reducedMotion ? "auto" : "smooth",
        block: "start",
      });
    });
  };

  return (
    <div className="page page-feed">
      <PageHeader
        title="动态"
        subtitle="谁在什么时候，分享了什么。"
        compact={compact}
        action={historyButton}
      />

      <div className="feed-column">
        {feed.isLoading ? (
          <div className="light-loading-row">正在读取动态…</div>
        ) : feed.isError ? (
          <div className="page-message" role="alert">
            <strong>暂时无法读取动态</strong>
            <button type="button" onClick={() => void feed.refetch()}>
              重试
            </button>
          </div>
        ) : groups.length ? (
          groups.map((group, groupIndex) => {
            const month = feedMonthKey(group.key);
            const previousMonth =
              groupIndex > 0 ? feedMonthKey(groups[groupIndex - 1].key) : null;
            return (
              <section
                className="feed-group"
                key={group.key}
                id={month !== previousMonth ? `feed-month-${month}` : undefined}
              >
                <h2>{group.label}</h2>
                <div className="feed-rows">
                  {group.items.map((item) => {
                    const transitionKey = `feed-${item.share_id}`;
                    return (
                      <motion.button
                        type="button"
                        className="feed-row"
                        key={item.share_id}
                        whileTap={reducedMotion ? undefined : { scale: 0.99 }}
                        transition={motionTokens.micro}
                        onClick={() =>
                          onOpenAlbum({
                            album: item,
                            transitionKey,
                            shareContext: item,
                          })
                        }
                      >
                        <Artwork
                          album={item}
                          layoutId={reducedMotion ? undefined : transitionKey}
                          className="feed-artwork"
                        />
                        <span className="feed-copy">
                          <strong>{item.title}</strong>
                          <span>{item.artist}</span>
                          <small>
                            {item.member_nickname}
                            {item.is_repeat ? " 再次分享了" : " 分享了"}
                            {item.date_precision === "second"
                              ? ` · ${formatShareTime(item.shared_at, item.date_precision)}`
                              : ""}
                          </small>
                        </span>
                        <Icon name="chevron" className="row-chevron" />
                      </motion.button>
                    );
                  })}
                </div>
              </section>
            );
          })
        ) : (
          <div className="page-message empty-state">
            <strong>还没有动态</strong>
            <span>同步第一张专辑后，分享记录会按日期出现在这里。</span>
          </div>
        )}
      </div>

      <AnimatePresence>
        {dateNavigatorOpen ? (
          <motion.div
            className="action-sheet-layer"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={motionTokens.exit}
          >
            <button
              type="button"
              className="action-sheet-backdrop"
              aria-label="关闭时间选择"
              onClick={() => setDateNavigatorOpen(false)}
            />
            <motion.section
              className="date-navigator"
              role="dialog"
              aria-modal="true"
              aria-labelledby="date-navigator-title"
              initial={reducedMotion ? { opacity: 0 } : { y: 34, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              exit={reducedMotion ? { opacity: 0 } : { y: 24, opacity: 0 }}
              transition={motionTokens.softSpring}
            >
              <div className="action-sheet-handle" />
              <div className="date-navigator-heading">
                {navigatorYear ? (
                  <button
                    type="button"
                    aria-label="返回年份"
                    onClick={() => {
                      setNavigatorDirection(-1);
                      setNavigatorYear(null);
                    }}
                  >
                    <Icon name="chevron" />
                    <span>年份</span>
                  </button>
                ) : null}
                <h2 id="date-navigator-title">
                  {navigatorYear ? `${navigatorYear}年` : "跳转到年份"}
                </h2>
              </div>
              {years.length ? (
                <AnimatePresence mode="wait" initial={false}>
                  <motion.div
                    className="date-options"
                    key={navigatorYear ?? "years"}
                    initial={
                      reducedMotion
                        ? { opacity: 0 }
                        : { opacity: 0, x: 18 * navigatorDirection }
                    }
                    animate={{ opacity: 1, x: 0 }}
                    exit={
                      reducedMotion
                        ? { opacity: 0 }
                        : { opacity: 0, x: -12 * navigatorDirection }
                    }
                    transition={motionTokens.enter}
                  >
                    {navigatorYear
                      ? (
                          years.find(([year]) => year === navigatorYear)?.[1] ??
                          []
                        ).map(([month]) => (
                          <button
                            type="button"
                            key={month}
                            onClick={() => jumpToMonth(month)}
                          >
                            <span>{Number(month.slice(5))}月</span>
                            <Icon name="chevron" />
                          </button>
                        ))
                      : years.map(([year, yearMonths]) => (
                          <button
                            type="button"
                            key={year}
                            onClick={() => {
                              setNavigatorDirection(1);
                              setNavigatorYear(year);
                            }}
                          >
                            <span>{year}年</span>
                            <span className="date-option-count">
                              {yearMonths.length}个月
                            </span>
                            <Icon name="chevron" />
                          </button>
                        ))}
                  </motion.div>
                </AnimatePresence>
              ) : (
                <p className="detail-empty">暂无可跳转的月份</p>
              )}
            </motion.section>
          </motion.div>
        ) : null}
      </AnimatePresence>
    </div>
  );
}
