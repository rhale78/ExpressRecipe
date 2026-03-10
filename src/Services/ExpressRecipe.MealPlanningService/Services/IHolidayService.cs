namespace ExpressRecipe.MealPlanningService.Services;

/// <summary>
/// Returns human-readable holiday labels for a given date.
/// </summary>
public interface IHolidayService
{
    /// <summary>Returns a holiday label for the given date, or <c>null</c> if it is not a holiday.</summary>
    string? GetHolidayLabel(DateOnly date);
}

/// <summary>
/// US federal holiday implementation of <see cref="IHolidayService"/>.
/// Calculates fixed and floating holidays for the current year range.
/// </summary>
public sealed class HolidayService : IHolidayService
{
    private readonly Dictionary<DateOnly, string?> _cache = new();

    public string? GetHolidayLabel(DateOnly date)
    {
        if (_cache.TryGetValue(date, out string? cached))
        {
            return cached;
        }

        string? result = ComputeHoliday(date);
        _cache[date] = result;
        return result;
    }

    private static string? ComputeHoliday(DateOnly date)
    {
        int year = date.Year;
        int month = date.Month;
        int day = date.Day;

        // Fixed holidays
        return (month, day) switch
        {
            (1, 1)   => "New Year's Day",
            (6, 19)  => "Juneteenth",
            (7, 4)   => "Independence Day",
            (11, 11) => "Veterans Day",
            (12, 25) => "Christmas Day",
            _        => ComputeFloatingHoliday(year, month, day)
        };
    }

    private static string? ComputeFloatingHoliday(int year, int month, int day)
    {
        // MLK Day: 3rd Monday in January
        if (month == 1 && IsNthWeekday(year, 1, DayOfWeek.Monday, 3, day)) { return "Martin Luther King Jr. Day"; }

        // Presidents' Day: 3rd Monday in February
        if (month == 2 && IsNthWeekday(year, 2, DayOfWeek.Monday, 3, day)) { return "Presidents' Day"; }

        // Memorial Day: last Monday in May
        if (month == 5 && IsLastWeekday(year, 5, DayOfWeek.Monday, day)) { return "Memorial Day"; }

        // Labor Day: 1st Monday in September
        if (month == 9 && IsNthWeekday(year, 9, DayOfWeek.Monday, 1, day)) { return "Labor Day"; }

        // Columbus Day: 2nd Monday in October
        if (month == 10 && IsNthWeekday(year, 10, DayOfWeek.Monday, 2, day)) { return "Columbus Day"; }

        // Thanksgiving: 4th Thursday in November
        if (month == 11 && IsNthWeekday(year, 11, DayOfWeek.Thursday, 4, day)) { return "Thanksgiving Day"; }

        return null;
    }

    private static bool IsNthWeekday(int year, int month, DayOfWeek weekday, int n, int day)
    {
        int count = 0;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        for (int d = 1; d <= daysInMonth; d++)
        {
            if (new DateTime(year, month, d).DayOfWeek == weekday)
            {
                count++;
                if (count == n) { return d == day; }
            }
        }
        return false;
    }

    private static bool IsLastWeekday(int year, int month, DayOfWeek weekday, int day)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        for (int d = daysInMonth; d >= 1; d--)
        {
            if (new DateTime(year, month, d).DayOfWeek == weekday) { return d == day; }
        }
        return false;
    }
}
