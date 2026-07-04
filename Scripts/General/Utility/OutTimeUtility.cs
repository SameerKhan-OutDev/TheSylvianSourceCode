using System;

public static class OutTimeUtility
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public static string GetTimeAgo(string dateString)
    {
        if (!DateTime.TryParseExact(dateString, DateFormat, null, System.Globalization.DateTimeStyles.None, out DateTime lastPlayed))
        {
            return "Unknown";
        }

        TimeSpan diff = DateTime.Now - lastPlayed;

        // Years
        if (diff.TotalDays >= 365)
        {
            int units = (int)(diff.TotalDays / 365);
            return $"{units} {(units == 1 ? "year" : "years")} ago";
        }
        // Months (Approximate)
        if (diff.TotalDays >= 30)
        {
            int units = (int)(diff.TotalDays / 30);
            return $"{units} {(units == 1 ? "month" : "months")} ago";
        }
        // Days
        if (diff.TotalDays >= 1)
        {
            int units = (int)diff.TotalDays;
            return $"{units} {(units == 1 ? "day" : "days")} ago";
        }
        // Hours
        if (diff.TotalHours >= 1)
        {
            int units = (int)diff.TotalHours;
            return $"{units} {(units == 1 ? "hour" : "hours")} ago";
        }
        // Minutes
        if (diff.TotalMinutes >= 1)
        {
            int units = (int)diff.TotalMinutes;
            return $"{units} {(units == 1 ? "minute" : "minutes")} ago";
        }

        return "Just now";
    }
}