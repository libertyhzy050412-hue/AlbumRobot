import {
  AnimatePresence,
  motion,
  useDragControls,
  useMotionValue,
  useReducedMotion,
  useTransform,
  type PanInfo,
} from "motion/react";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiError, fetchAlbumDetail } from "./api";
import { Artwork } from "./Artwork";
import type { AlbumSelection } from "./domain";
import { formatDate, formatShareTime } from "./format";
import { useMediaQuery } from "./hooks";
import { Icon } from "./icons";
import { motionTokens } from "./motion";

export function AlbumDetailSheet({
  selection,
  onClose,
  onUnauthorized,
}: {
  selection: AlbumSelection | null;
  onClose: () => void;
  onUnauthorized: () => void;
}) {
  const isMobile = useMediaQuery("(max-width: 899px)");
  const reducedMotion = useReducedMotion();
  const controls = useDragControls();
  const y = useMotionValue(0);
  const backdropOpacity = useTransform(y, [0, 520], [1, 0]);
  const detail = useQuery({
    queryKey: ["album-detail", selection?.album.netease_album_id],
    queryFn: () => fetchAlbumDetail(selection!.album.netease_album_id),
    enabled: Boolean(selection),
    staleTime: 30_000,
  });

  useEffect(() => {
    if (detail.error instanceof ApiError && detail.error.status === 401)
      onUnauthorized();
  }, [detail.error, onUnauthorized]);

  useEffect(() => {
    if (selection) y.set(0);
  }, [selection, y]);

  useEffect(() => {
    if (!selection) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [onClose, selection]);

  const finishDrag = (
    _event: MouseEvent | TouchEvent | PointerEvent,
    info: PanInfo,
  ) => {
    if (info.offset.y > 150 || (info.velocity.y > 720 && info.offset.y > 34))
      onClose();
  };

  const album = detail.data?.album ?? selection?.album;
  const recentShares = detail.data?.recent_shares ?? [];

  return (
    <AnimatePresence>
      {selection && album ? (
        <motion.div
          className="detail-layer"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={motionTokens.exit}
        >
          <motion.button
            type="button"
            className="detail-backdrop"
            aria-label="关闭专辑详情"
            onClick={onClose}
            style={{ opacity: backdropOpacity }}
          />
          <motion.section
            className="album-detail-sheet"
            role="dialog"
            aria-modal="true"
            aria-labelledby="album-detail-title"
            initial={
              reducedMotion
                ? { opacity: 0 }
                : { opacity: 0, y: 54, scale: 0.985 }
            }
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={
              reducedMotion
                ? { opacity: 0 }
                : { opacity: 0, y: 48, scale: 0.985 }
            }
            transition={motionTokens.softSpring}
            drag={isMobile && !reducedMotion ? "y" : false}
            dragControls={controls}
            dragListener={false}
            dragConstraints={{ top: 0, bottom: 0 }}
            dragElastic={{ top: 0.025, bottom: 0.58 }}
            dragTransition={{ bounceStiffness: 340, bounceDamping: 34 }}
            onDragEnd={finishDrag}
            style={isMobile ? { y } : undefined}
          >
            <button
              type="button"
              className="sheet-drag-handle"
              aria-label="向下拖动关闭"
              onPointerDown={(event) => controls.start(event)}
            >
              <span />
            </button>
            <button
              type="button"
              className="sheet-close"
              onClick={onClose}
              aria-label="关闭"
            >
              <Icon name="close" />
            </button>

            <div className="detail-scroll-region">
              <div className="detail-primary">
                <Artwork
                  album={album}
                  layoutId={reducedMotion ? undefined : selection.transitionKey}
                  className="detail-artwork"
                  priority
                />
                <motion.div
                  className="detail-copy"
                  key={album.netease_album_id}
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={motionTokens.enter}
                >
                  <p className="eyebrow">专辑</p>
                  <h2 id="album-detail-title">{album.title}</h2>
                  <p className="detail-artist">{album.artist}</p>
                  <p className="detail-summary">
                    {album.distinct_sharers ?? 0} 位成员分享过
                    {album.share_count ? ` · ${album.share_count} 次记录` : ""}
                  </p>
                  {selection.shareContext ? (
                    <div className="share-context">
                      <span>本次分享</span>
                      <strong>{selection.shareContext.member_nickname}</strong>
                      <span>
                        {formatShareTime(
                          selection.shareContext.shared_at,
                          selection.shareContext.date_precision,
                        )}
                      </span>
                    </div>
                  ) : null}
                  {album.netease_url ? (
                    <a
                      className="netease-link"
                      href={album.netease_url}
                      target="_blank"
                      rel="noreferrer"
                    >
                      在网易云音乐中打开
                      <Icon name="chevron" />
                    </a>
                  ) : null}
                </motion.div>
              </div>

              <div className="detail-history">
                <div className="section-heading">
                  <h3>分享记录</h3>
                  {album.first_shared_at ? (
                    <span>首次收录于 {formatDate(album.first_shared_at)}</span>
                  ) : null}
                </div>
                {detail.isLoading ? (
                  <div className="light-loading-row">正在读取分享记录…</div>
                ) : recentShares.length > 0 ? (
                  <ul>
                    {recentShares.map((share) => (
                      <li key={share.share_id}>
                        <strong>{share.member_nickname}</strong>
                        <span>
                          {formatShareTime(
                            share.shared_at,
                            share.date_precision,
                          )}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="detail-empty">暂无更多分享记录</p>
                )}
              </div>
            </div>
          </motion.section>
        </motion.div>
      ) : null}
    </AnimatePresence>
  );
}
