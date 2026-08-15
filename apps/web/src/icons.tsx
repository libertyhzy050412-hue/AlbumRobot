import type { SVGProps } from "react";

export type IconName =
  | "albums"
  | "calendar"
  | "chevron"
  | "close"
  | "feed"
  | "more"
  | "search"
  | "stats";

export function Icon({
  name,
  ...props
}: { name: IconName } & SVGProps<SVGSVGElement>) {
  const common = {
    fill: "none",
    stroke: "currentColor",
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    strokeWidth: 1.9,
  };

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" {...common} {...props}>
      {name === "albums" ? (
        <>
          <rect x="3.5" y="3.5" width="17" height="17" rx="4" />
          <circle cx="12" cy="12" r="4.2" />
          <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />
        </>
      ) : name === "feed" ? (
        <>
          <path d="M5 6.5h14M5 12h14M5 17.5h14" />
          <circle cx="7" cy="6.5" r="1.2" fill="currentColor" stroke="none" />
        </>
      ) : name === "stats" ? (
        <>
          <path d="M5 19V11M12 19V5M19 19v-6" />
          <path d="M3 19.5h18" />
        </>
      ) : name === "search" ? (
        <>
          <circle cx="10.7" cy="10.7" r="6.3" />
          <path d="m15.4 15.4 4.1 4.1" />
        </>
      ) : name === "calendar" ? (
        <>
          <rect x="3.5" y="5" width="17" height="15.5" rx="3.5" />
          <path d="M7.5 3.5v3M16.5 3.5v3M3.8 9h16.4" />
        </>
      ) : name === "close" ? (
        <path d="m7 7 10 10M17 7 7 17" />
      ) : name === "chevron" ? (
        <path d="m9 5 7 7-7 7" />
      ) : (
        <>
          <circle cx="5" cy="12" r="1.25" fill="currentColor" stroke="none" />
          <circle cx="12" cy="12" r="1.25" fill="currentColor" stroke="none" />
          <circle cx="19" cy="12" r="1.25" fill="currentColor" stroke="none" />
        </>
      )}
    </svg>
  );
}
