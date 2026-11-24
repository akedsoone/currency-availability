using CurrencyAvailability.Data;
using CurrencyAvailability.Models;
using CurrencyAvailability.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CurrencyAvailability.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(int? cityId, int? currencyId, string? q, int? departmentId,
        int? historyCurrencyId, int days = 1, CancellationToken cancellationToken = default)
    {
        var query = q?.Trim() ?? "";
        if (!ModelState.IsValid || query.Length > 100 || days is not (1 or 7 or 30))
        {
            return BadRequest("Проверьте параметры фильтров.");
        }

        var cities = await db.Cities.AsNoTracking().OrderBy(city => city.Name).ToListAsync(cancellationToken);
        var currencies = await GetCurrenciesAsync(cancellationToken);
        var currency = currencies.Find(item => item.Id == currencyId);
        var selectedCurrencyId = currency?.Id;
        var visibleCurrencies = currency is null ? currencies : [currency];
        var departments = await db.Departments.AsNoTracking().Include(item => item.City)
            .Where(item => cityId == null || item.CityId == cityId)
            .OrderBy(item => item.City.Name).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        if (query.Length > 0)
        {
            departments = departments.Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Address.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var departmentIds = departments.Select(item => item.Id).ToArray();
        var now = DateTime.UtcNow;
        var latest = await db.AvailabilityRecords.AsNoTracking()
            .Where(item => (selectedCurrencyId == null || item.CurrencyId == selectedCurrencyId) && departmentIds.Contains(item.DepartmentId))
            .GroupBy(item => new { item.DepartmentId, item.CurrencyId })
            .Select(group => group.OrderByDescending(item => item.MeasuredAt).First())
            .ToListAsync(cancellationToken);
        var byDepartmentAndCurrency = latest.ToDictionary(item => (item.DepartmentId, item.CurrencyId));
        var cards = new List<DepartmentCard>();
        foreach (var department in departments)
        {
            var statuses = new List<CurrencyStatus>();
            foreach (var item in visibleCurrencies)
            {
                var record = byDepartmentAndCurrency.GetValueOrDefault((department.Id, item.Id));
                var level = record != null && now < record.MeasuredAt + AvailabilityWorker.Interval
                    ? record.Level : (AvailabilityLevel?)null;
                statuses.Add(new CurrencyStatus(item, level, record?.MeasuredAt));
            }

            cards.Add(new DepartmentCard(department, statuses));
        }

        var selectedDepartment = departments.Find(item => item.Id == departmentId);
        var historyCurrency = currencies.Find(item => item.Id == historyCurrencyId) ?? currency ?? currencies[0];
        var history = selectedDepartment is null ? null
            : await LoadHistoryAsync(selectedDepartment, historyCurrency, days, departments, currencies, now, cancellationToken);

        return View(new DepartmentPage(cards,
            cities.Select(item => new SelectListItem(item.Name, item.Id.ToString(), item.Id == cityId)).ToList(),
            currencies, cityId, selectedCurrencyId, query, history));
    }

    [HttpGet("/departments/{id:int}")]
    public Task<IActionResult> Details(int id, int? currencyId, int days = 1, CancellationToken cancellationToken = default) =>
        ShowHistoryAsync("Details", id, currencyId, days, cancellationToken);

    [HttpGet("/statistics")]
    public Task<IActionResult> Statistics(int? departmentId, int? currencyId, int days = 7, CancellationToken cancellationToken = default) =>
        ShowHistoryAsync("Statistics", departmentId, currencyId, days, cancellationToken);

    [Route("/error")]
    public IActionResult Error() => View();

    private async Task<IActionResult> ShowHistoryAsync(string view, int? departmentId, int? currencyId, int days, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || days is not (1 or 7 or 30))
        {
            return BadRequest("Допустимый период – 1, 7 или 30 дней.");
        }

        var departments = await db.Departments.AsNoTracking().Include(item => item.City)
            .OrderBy(item => item.City.Name).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        var department = departmentId.HasValue ? departments.Find(item => item.Id == departmentId) : departments.FirstOrDefault();
        if (department is null)
        {
            return NotFound();
        }

        var currencies = await GetCurrenciesAsync(cancellationToken);
        var currency = currencyId.HasValue ? currencies.Find(item => item.Id == currencyId) : currencies[0];
        if (currency is null)
        {
            return NotFound();
        }

        return View(view, await LoadHistoryAsync(department, currency, days, departments, currencies, DateTime.UtcNow, cancellationToken));
    }

    private async Task<HistoryPage> LoadHistoryAsync(Department department, Currency currency, int days,
        List<Department> departments, List<Currency> currencies, DateTime end, CancellationToken cancellationToken)
    {
        var start = end.AddDays(-days);
        var previous = start - AvailabilityWorker.Interval;
        var records = await db.AvailabilityRecords.AsNoTracking()
            .Where(item => item.DepartmentId == department.Id && item.CurrencyId == currency.Id
                && item.MeasuredAt >= previous && item.MeasuredAt < end)
            .OrderBy(item => item.MeasuredAt).ToListAsync(cancellationToken);
        var intervals = AvailabilityHistory.BuildIntervals(records, start, end);

        return new HistoryPage(department, currency, days, start, end, intervals,
            AvailabilityHistory.CalculateShares(intervals, end - start),
            departments.Select(item => new SelectListItem($"{item.City.Name} · {item.Name}",
                item.Id.ToString(), item.Id == department.Id)).ToList(),
            currencies.Select(item => new SelectListItem($"{item.Code} · {item.Name}",
                item.Id.ToString(), item.Id == currency.Id)).ToList());
    }

    private Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        db.Currencies.AsNoTracking().OrderByDescending(item => item.Code == "USD")
            .ThenBy(item => item.Code).ToListAsync(cancellationToken);
}
