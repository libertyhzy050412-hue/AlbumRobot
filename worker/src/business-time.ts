export type StatsPeriod = "week" | "month" | "year";

const businessOffsetMilliseconds = 8 * 60 * 60 * 1000;

export function businessPeriodStart(
  period: StatsPeriod,
  now = new Date(),
): string {
  const shifted = new Date(now.getTime() + businessOffsetMilliseconds);
  const year = shifted.getUTCFullYear();
  const month = shifted.getUTCMonth();
  const date = shifted.getUTCDate();

  let startYear = year;
  let startMonth = month;
  let startDate = date;

  if (period === "year") {
    startMonth = 0;
    startDate = 1;
  } else if (period === "month") {
    startDate = 1;
  } else {
    const mondayOffset = (shifted.getUTCDay() + 6) % 7;
    startDate -= mondayOffset;
  }

  const shiftedBoundary = Date.UTC(startYear, startMonth, startDate);
  return new Date(shiftedBoundary - businessOffsetMilliseconds).toISOString();
}
