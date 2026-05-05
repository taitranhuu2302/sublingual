type MetricBarsProps = {
  bars: number[];
};

export function MetricBars({ bars }: MetricBarsProps) {
  return (
    <div className="flex h-56 items-end gap-1 rounded-md border border-border/50 p-2">
      {bars.map((height, index) => (
        <div
          key={`${height}-${index}`}
          className="w-full rounded-sm bg-primary/80"
          style={{ height: `${height}%` }}
        />
      ))}
    </div>
  );
}
