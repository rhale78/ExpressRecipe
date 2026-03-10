using System.Collections.Generic;
using System.Linq;

namespace ExpressRecipe.MealPlanningService.Services;

public interface IHolidayService
{
    string? GetHolidayLabel(DateOnly date);
    List<HolidayDto> GetHolidaysForMonth(int year, int month, IReadOnlyList<string> enabledCategories);
}

public sealed record HolidayDto
{
    public DateOnly Date { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}

public sealed class HolidayService : IHolidayService
{
    private readonly Dictionary<DateOnly, (string Name, string Category)> _cache = new();
    private int _lastComputedYear = 0;

    public string? GetHolidayLabel(DateOnly date)
    {
        EnsureYear(date.Year);
        return _cache.TryGetValue(date, out (string Name, string Category) h) ? h.Name : null;
    }

    public List<HolidayDto> GetHolidaysForMonth(int year, int month, IReadOnlyList<string> enabledCategories)
    {
        EnsureYear(year);
        return _cache
            .Where(kvp => kvp.Key.Year == year && kvp.Key.Month == month
                && (kvp.Value.Category == "Federal" || enabledCategories.Contains(kvp.Value.Category)))
            .Select(kvp => new HolidayDto { Date = kvp.Key, Name = kvp.Value.Name, Category = kvp.Value.Category })
            .OrderBy(h => h.Date)
            .ToList();
    }

    private void EnsureYear(int year)
    {
        if (_lastComputedYear == year) { return; }
        _lastComputedYear = year;
        _cache.Clear();
        AddFederal(year);
        AddReligiousAndCultural(year);
    }

    private void AddFederal(int year)
    {
        Add(new DateOnly(year, 1, 1),                          "New Year's Day",   "Federal");
        Add(NthWeekday(year, 1,  DayOfWeek.Monday, 3),         "MLK Jr. Day",      "Federal");
        Add(NthWeekday(year, 2,  DayOfWeek.Monday, 3),         "Presidents' Day",  "Federal");
        Add(LastWeekday(year, 5, DayOfWeek.Monday),            "Memorial Day",     "Federal");
        Add(new DateOnly(year, 6, 19),                         "Juneteenth",       "Federal");
        Add(new DateOnly(year, 7, 4),                          "Independence Day", "Federal");
        Add(NthWeekday(year, 9,  DayOfWeek.Monday, 1),         "Labor Day",        "Federal");
        Add(NthWeekday(year, 10, DayOfWeek.Monday, 2),         "Columbus Day",     "Federal");
        Add(new DateOnly(year, 11, 11),                        "Veterans Day",     "Federal");
        Add(NthWeekday(year, 11, DayOfWeek.Thursday, 4),       "Thanksgiving",     "Federal");
        Add(new DateOnly(year, 12, 25),                        "Christmas Day",    "Federal");
    }

    private void AddReligiousAndCultural(int year)
    {
        DateOnly easter = ComputeEaster(year);
        Add(easter,                     "Easter Sunday",     "Christian");
        Add(easter.AddDays(-2),         "Good Friday",       "Christian");
        Add(easter.AddDays(-46),        "Ash Wednesday",     "Christian");
        Add(new DateOnly(year, 12, 24), "Christmas Eve",     "Christian");

        Add(new DateOnly(year, 2, 14),  "Valentine's Day",   "Cultural");
        Add(new DateOnly(year, 3, 17),  "St. Patrick's Day", "Cultural");
        Add(new DateOnly(year, 10, 31), "Halloween",         "Cultural");
        Add(NthWeekday(year, 5, DayOfWeek.Sunday, 2),        "Mother's Day",  "Cultural");
        Add(NthWeekday(year, 6, DayOfWeek.Sunday, 3),        "Father's Day",  "Cultural");

        AddLunarHolidays(year);
    }

    private void Add(DateOnly date, string name, string category)
    {
        if (category == "Federal")
        {
            date = date.DayOfWeek switch
            {
                DayOfWeek.Saturday => date.AddDays(-1),
                DayOfWeek.Sunday   => date.AddDays(1),
                _                  => date
            };
        }
        _cache[date] = (name, category);
    }

    private static DateOnly NthWeekday(int year, int month, DayOfWeek dow, int n)
    {
        DateOnly first = new(year, month, 1);
        int offset = ((int)dow - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + (n - 1) * 7);
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek dow)
    {
        DateOnly last = new(year, month, DateTime.DaysInMonth(year, month));
        return last.AddDays(-((int)last.DayOfWeek - (int)dow + 7) % 7);
    }

    internal static DateOnly ComputeEaster(int year)
    {
        // Butcher's algorithm (Gregorian)
        int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4, f = (b + 8) / 25;
        int g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    // Pre-seeded Jewish/Islamic holidays 2024–2030 (lunar calendar approximations)
    private static readonly Dictionary<(int year, string holiday), DateOnly> LunarHolidays = new()
    {
        { (2024, "Rosh Hashanah"), new DateOnly(2024, 10, 2)  }, { (2024, "Yom Kippur"),  new DateOnly(2024, 10, 11) },
        { (2024, "Passover"),      new DateOnly(2024, 4,  22) }, { (2024, "Hanukkah"),    new DateOnly(2024, 12, 25) },
        { (2024, "Eid al-Fitr"),   new DateOnly(2024, 4,  10) }, { (2024, "Eid al-Adha"), new DateOnly(2024, 6,  16) },
        { (2025, "Rosh Hashanah"), new DateOnly(2025, 9,  22) }, { (2025, "Yom Kippur"),  new DateOnly(2025, 10, 1)  },
        { (2025, "Passover"),      new DateOnly(2025, 4,  12) }, { (2025, "Hanukkah"),    new DateOnly(2025, 12, 14) },
        { (2025, "Eid al-Fitr"),   new DateOnly(2025, 3,  30) }, { (2025, "Eid al-Adha"), new DateOnly(2025, 6,  6)  },
        { (2026, "Rosh Hashanah"), new DateOnly(2026, 9,  11) }, { (2026, "Yom Kippur"),  new DateOnly(2026, 9,  20) },
        { (2026, "Passover"),      new DateOnly(2026, 4,  1)  }, { (2026, "Eid al-Fitr"), new DateOnly(2026, 3,  20) },
        { (2026, "Eid al-Adha"),   new DateOnly(2026, 5,  27) },
        { (2027, "Rosh Hashanah"), new DateOnly(2027, 9,  1)  }, { (2027, "Yom Kippur"),  new DateOnly(2027, 9,  10) },
        { (2027, "Passover"),      new DateOnly(2027, 4,  21) }, { (2027, "Hanukkah"),    new DateOnly(2027, 12, 4)  },
        { (2027, "Eid al-Fitr"),   new DateOnly(2027, 3,  9)  }, { (2027, "Eid al-Adha"), new DateOnly(2027, 5,  16) },
        { (2028, "Rosh Hashanah"), new DateOnly(2028, 9,  20) }, { (2028, "Yom Kippur"),  new DateOnly(2028, 9,  29) },
        { (2028, "Passover"),      new DateOnly(2028, 4,  10) }, { (2028, "Hanukkah"),    new DateOnly(2028, 12, 22) },
        { (2028, "Eid al-Fitr"),   new DateOnly(2028, 2,  26) }, { (2028, "Eid al-Adha"), new DateOnly(2028, 5,  4)  },
        { (2029, "Rosh Hashanah"), new DateOnly(2029, 9,  9)  }, { (2029, "Yom Kippur"),  new DateOnly(2029, 9,  18) },
        { (2029, "Passover"),      new DateOnly(2029, 3,  30) }, { (2029, "Hanukkah"),    new DateOnly(2029, 12, 11) },
        { (2029, "Eid al-Fitr"),   new DateOnly(2029, 2,  14) }, { (2029, "Eid al-Adha"), new DateOnly(2029, 4,  23) },
        { (2030, "Rosh Hashanah"), new DateOnly(2030, 9,  27) }, { (2030, "Yom Kippur"),  new DateOnly(2030, 10, 6)  },
        { (2030, "Passover"),      new DateOnly(2030, 4,  18) }, { (2030, "Hanukkah"),    new DateOnly(2030, 12, 1)  },
        { (2030, "Eid al-Fitr"),   new DateOnly(2030, 2,  3)  }, { (2030, "Eid al-Adha"), new DateOnly(2030, 4,  12) },
    };

    private void AddLunarHolidays(int year)
    {
        foreach ((int y, string name) in LunarHolidays.Keys.Where(k => k.year == year))
        {
            string category = name is "Rosh Hashanah" or "Yom Kippur" or "Passover" or "Hanukkah" ? "Jewish" : "Islamic";
            Add(LunarHolidays[(y, name)], name, category);
        }
    }
}
