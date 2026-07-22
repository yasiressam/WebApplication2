namespace WebApplication2.Services
{
    internal static class CleanupSchedule
    {
        private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

        public static TimeSpan GetDelayUntilNextRun(DateTime now, TimeSpan scheduledTime)
        {
            var nextRun = new DateTime(
                now.Year,
                now.Month,
                now.Day,
                scheduledTime.Hours,
                scheduledTime.Minutes,
                scheduledTime.Seconds,
                now.Kind);

            if (now >= nextRun)
            {
                nextRun = nextRun.Add(Interval);
            }

            return nextRun - now;
        }
    }
}
