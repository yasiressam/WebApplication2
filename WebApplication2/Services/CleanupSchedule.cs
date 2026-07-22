namespace WebApplication2.Services
{
    internal static class CleanupSchedule
    {
        private static readonly TimeSpan ScheduledTime = TimeSpan.FromHours(3);
        private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

        public static TimeSpan GetDelayUntilNextRun(DateTimeOffset now)
        {
            var nextRun = new DateTimeOffset(
                now.Year,
                now.Month,
                now.Day,
                ScheduledTime.Hours,
                ScheduledTime.Minutes,
                ScheduledTime.Seconds,
                now.Offset);

            if (now >= nextRun)
            {
                nextRun = nextRun.Add(Interval);
            }

            return nextRun - now;
        }
    }
}
