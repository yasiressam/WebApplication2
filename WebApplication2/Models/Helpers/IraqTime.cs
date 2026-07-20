namespace WebApplication2.Models.Helpers
{
    public static class IraqTime
    {
        private static readonly TimeZoneInfo BaghdadTimeZone = ResolveBaghdadTimeZone();

        public static DateTime Now()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BaghdadTimeZone);
        }

        private static TimeZoneInfo ResolveBaghdadTimeZone()
        {
            string[] timeZoneIds =
            [
                "Arabic Standard Time",
                "Asia/Baghdad"
            ];

            foreach (var id in timeZoneIds)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Local;
        }
    }
}
