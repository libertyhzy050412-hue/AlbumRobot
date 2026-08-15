const dateFormatter = new Intl.DateTimeFormat("zh-CN", {
  month: "long",
  day: "numeric",
  weekday: "short",
  timeZone: "Asia/Shanghai",
});

const shortDateFormatter = new Intl.DateTimeFormat("zh-CN", {
  year: "numeric",
  month: "short",
  day: "numeric",
  timeZone: "Asia/Shanghai",
});

const timeFormatter = new Intl.DateTimeFormat("zh-CN", {
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
  timeZone: "Asia/Shanghai",
});

const monthFormatter = new Intl.DateTimeFormat("zh-CN", {
  year: "numeric",
  month: "long",
  timeZone: "Asia/Shanghai",
});

function safeDate(value: string): Date | null {
  const date = new Date(
    value.length === 10 ? `${value}T00:00:00+08:00` : value,
  );
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDate(value?: string | null): string {
  if (!value) return "未知日期";
  const date = safeDate(value);
  return date ? shortDateFormatter.format(date) : value;
}

export function formatShareTime(
  value: string,
  precision: "day" | "second",
): string {
  if (precision === "day") return formatDate(value);
  const date = safeDate(value);
  return date ? timeFormatter.format(date) : "";
}

export function feedDateKey(value: string): string {
  if (/^\d{4}-\d{2}-\d{2}$/u.test(value)) return value;
  const date = safeDate(value);
  if (!date) return value.slice(0, 10);
  const parts = new Intl.DateTimeFormat("en-CA", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    timeZone: "Asia/Shanghai",
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";
  return `${part("year")}-${part("month")}-${part("day")}`;
}

export function feedDateLabel(value: string): string {
  const date = safeDate(value);
  return date ? dateFormatter.format(date) : value;
}

export function feedMonthKey(value: string): string {
  return feedDateKey(value).slice(0, 7);
}

export function feedMonthLabel(value: string): string {
  const date = safeDate(`${feedMonthKey(value)}-01`);
  return date ? monthFormatter.format(date) : feedMonthKey(value);
}
