import { motion } from "motion/react";
import type { CSSProperties } from "react";
import type { AlbumSummary } from "./domain";

export function artworkHue(id: string): number {
  let hash = 2_166_136_261;
  for (const character of id) {
    hash ^= character.charCodeAt(0);
    hash = Math.imul(hash, 16_777_619);
  }
  hash ^= hash >>> 16;
  hash = Math.imul(hash, 2_246_822_507);
  hash ^= hash >>> 13;
  return (hash >>> 0) % 360;
}

export function Artwork({
  album,
  layoutId,
  className = "",
  priority = false,
}: {
  album: AlbumSummary;
  layoutId?: string;
  className?: string;
  priority?: boolean;
}) {
  const hue = artworkHue(album.netease_album_id);
  const style = {
    "--art-hue": hue,
    "--art-hue-alt": (hue + 48) % 360,
  } as CSSProperties;

  return (
    <motion.div
      className={`artwork ${className}`}
      layoutId={layoutId}
      style={style}
      transition={{ type: "spring", stiffness: 310, damping: 34, mass: 0.92 }}
    >
      {album.cover_url ? (
        <img
          src={album.cover_url}
          alt=""
          loading={priority ? "eager" : "lazy"}
          decoding="async"
        />
      ) : (
        <div className="artwork-fallback" aria-hidden="true">
          <span />
        </div>
      )}
    </motion.div>
  );
}
