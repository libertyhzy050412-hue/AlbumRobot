import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { useEffect } from "react";
import { ApiError, fetchStats } from "./api";
import type { StatsPeriod } from "./domain";
import { useCompactHeader } from "./hooks";
import { Icon } from "./icons";
import { motionTokens } from "./motion";
import { PageHeader } from "./PageHeader";
import { useUiStore } from "./store";

const periods: Array<{ value: StatsPeriod; label: string }> = [
  { value: "week", label: "本周" },
  { value: "month", label: "本月" },
  { value: "year", label: "本年" },
];

export function StatsPage({
  authenticated,
  onUnauthorized,
}: {
  authenticated: boolean;
  onUnauthorized: () => void;
}) {
  const compact = useCompactHeader();
  const reducedMotion = useReducedMotion();
  const { statsPeriod, setStatsPeriod, startMemberDrilldown } = useUiStore();
  const stats = useQuery({
    queryKey: ["stats", statsPeriod],
    queryFn: () => fetchStats(statsPeriod),
    enabled: authenticated,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  });

  useEffect(() => {
    if (stats.error instanceof ApiError && stats.error.status === 401)
      onUnauthorized();
  }, [onUnauthorized, stats.error]);

  const periodLabel =
    periods.find((period) => period.value === statsPeriod)?.label ?? "本周";

  return (
    <div className="page page-stats">
      <PageHeader
        title="统计"
        subtitle="只看群里的专辑与分享者。"
        compact={compact}
      />

      <div className="stats-period segmented-control" aria-label="统计周期">
        {periods.map((period) => (
          <button
            key={period.value}
            type="button"
            className={statsPeriod === period.value ? "is-selected" : ""}
            onClick={() => setStatsPeriod(period.value)}
          >
            {statsPeriod === period.value ? (
              <motion.span
                className="segment-selection"
                layoutId="stats-period-selection"
                transition={motionTokens.responsiveSpring}
              />
            ) : null}
            <span>{period.label}</span>
          </button>
        ))}
      </div>

      <div className="stats-layout">
        <section className="stats-summary" aria-live="polite">
          <AnimatePresence mode="popLayout" initial={false}>
            <motion.div
              key={`${statsPeriod}-${stats.data?.album_count ?? 0}`}
              initial={reducedMotion ? { opacity: 0 } : { opacity: 0, y: 12 }}
              animate={{ opacity: 1, y: 0 }}
              exit={reducedMotion ? { opacity: 0 } : { opacity: 0, y: -8 }}
              transition={motionTokens.enter}
            >
              <strong>{stats.data?.album_count ?? 0}</strong>
              <span>张不同专辑</span>
              <p>{periodLabel}群内分享</p>
            </motion.div>
          </AnimatePresence>
        </section>

        <section className="ranking-section">
          <div className="section-heading">
            <h2>成员排行</h2>
            <span>按不同专辑数</span>
          </div>
          {stats.isLoading ? (
            <div className="light-loading-row">正在计算…</div>
          ) : stats.isError ? (
            <div className="page-message compact-message" role="alert">
              <strong>暂时无法读取统计</strong>
              <button type="button" onClick={() => void stats.refetch()}>
                重试
              </button>
            </div>
          ) : stats.data?.ranking.length ? (
            <motion.ol layout>
              <AnimatePresence mode="popLayout" initial={false}>
                {stats.data.ranking.map((member, index) => (
                  <motion.li
                    key={member.member_id}
                    layout
                    initial={{ opacity: 0, y: 6 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -4 }}
                    transition={motionTokens.responsiveSpring}
                  >
                    <button
                      type="button"
                      onClick={() =>
                        startMemberDrilldown(
                          member.member_nickname,
                          statsPeriod,
                        )
                      }
                    >
                      <span
                        className={`rank-number ${index < 3 ? "is-top" : ""}`}
                      >
                        {index + 1}
                      </span>
                      <strong>{member.member_nickname}</strong>
                      <span className="rank-count">{member.album_count}</span>
                      <Icon name="chevron" />
                    </button>
                  </motion.li>
                ))}
              </AnimatePresence>
            </motion.ol>
          ) : (
            <p className="ranking-empty">{periodLabel}还没有专辑分享</p>
          )}
        </section>
      </div>
    </div>
  );
}
