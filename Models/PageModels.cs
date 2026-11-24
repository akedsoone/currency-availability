using Microsoft.AspNetCore.Mvc.Rendering;

namespace CurrencyAvailability.Models;

public static class AvailabilityLabels
{
    public static string Text(AvailabilityLevel? level) => level switch
    {
        AvailabilityLevel.None => "Нет валюты",
        AvailabilityLevel.Low => "Мало",
        AvailabilityLevel.Enough => "Достаточно",
        AvailabilityLevel.High => "Много",
        _ => "Нет данных"
    };

    public static string Key(AvailabilityLevel? level) => level switch
    {
        AvailabilityLevel.None => "none",
        AvailabilityLevel.Low => "low",
        AvailabilityLevel.Enough => "enough",
        AvailabilityLevel.High => "high",
        _ => "unknown"
    };
}

public record CurrencyStatus(Currency Currency, AvailabilityLevel? Level, DateTime? MeasuredAt);
public record DepartmentCard(Department Department, List<CurrencyStatus> Statuses);
public record DepartmentPage(List<DepartmentCard> Cards, List<SelectListItem> Cities,
    List<Currency> Currencies, int? CityId, int? CurrencyId, string Query, HistoryPage? History);

public record LevelInterval(DateTime Start, DateTime End, AvailabilityLevel Level);
public record LevelShare(AvailabilityLevel? Level, double Percent, TimeSpan Duration);

public record HistoryPage(Department Department, Currency Currency, int Days, DateTime Start,
    DateTime End, List<LevelInterval> Intervals, List<LevelShare> Shares,
    List<SelectListItem> Departments, List<SelectListItem> Currencies);
