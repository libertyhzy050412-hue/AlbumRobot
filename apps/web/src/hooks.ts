import { useEffect, useState } from "react";

export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() =>
    typeof window === "undefined" ? false : window.matchMedia(query).matches,
  );

  useEffect(() => {
    const media = window.matchMedia(query);
    const update = () => setMatches(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, [query]);

  return matches;
}

export function useCompactHeader(disabled = false): boolean {
  const [compact, setCompact] = useState(false);

  useEffect(() => {
    if (disabled) {
      setCompact(false);
      return;
    }
    const update = () => setCompact(window.scrollY > 54);
    update();
    window.addEventListener("scroll", update, { passive: true });
    return () => window.removeEventListener("scroll", update);
  }, [disabled]);

  return compact;
}
