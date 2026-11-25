using CurrencyAvailability.Models;
using CurrencyAvailability.Services;
using Microsoft.EntityFrameworkCore;

namespace CurrencyAvailability.Data;

public static class DatabaseSetup
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var catalog = services.GetRequiredService<BankCatalog>();
        await db.Database.EnsureCreatedAsync();

        await using var transaction = await db.Database.BeginTransactionAsync();
        var cities = await db.Cities.ToDictionaryAsync(item => item.Name);
        var departments = await db.Departments.ToDictionaryAsync(item => item.ExternalCode);
        var currencies = await db.Currencies.ToDictionaryAsync(item => item.Code);

        foreach (var item in catalog.Departments)
        {
            if (!cities.TryGetValue(item.City, out var city))
            {
                city = new City { Name = item.City };
                db.Cities.Add(city);
                cities.Add(item.City, city);
            }

            if (!departments.ContainsKey(item.Code))
            {
                var department = new Department
                {
                    ExternalCode = item.Code, Name = item.Name, Address = item.Address, City = city
                };
                db.Departments.Add(department);
                departments.Add(item.Code, department);
            }
        }

        foreach (var item in catalog.Currencies)
        {
            if (!currencies.ContainsKey(item.Code))
            {
                var currency = new Currency { Code = item.Code, Name = item.Name };
                db.Currencies.Add(currency);
                currencies.Add(item.Code, currency);
            }
        }

        await db.SaveChangesAsync();

        if (!await db.AvailabilityRecords.AnyAsync())
        {
            var departmentIds = catalog.Departments.Select(item => departments[item.Code].Id).ToArray();
            var currencyIds = catalog.Currencies.Select(item => currencies[item.Code].Id).ToArray();
            await SeedHistoryAsync(db, departmentIds, currencyIds);
        }
        
        await transaction.CommitAsync();
    }

    private static async Task SeedHistoryAsync(AppDbContext db, int[] departmentIds, int[] currencyIds)
    {
        var end = AvailabilityWorker.GetSlot(DateTime.UtcNow);
        for (var day = end.AddDays(-30); day < end; day = day.AddDays(1))
        {
            var records = new List<AvailabilityRecord>();
            for (var time = day; time < day.AddDays(1); time += AvailabilityWorker.Interval)
            {
                for (var department = 0; department < departmentIds.Length; department++)
                {
                    for (var currency = 0; currency < currencyIds.Length; currency++)
                    {
                        records.Add(new AvailabilityRecord
                        {
                            DepartmentId = departmentIds[department],
                            CurrencyId = currencyIds[currency],
                            MeasuredAt = time,
                            Level = DemoAvailability.GetLevel(time, department, currency)
                        });
                    }
                }
            }

            db.AvailabilityRecords.AddRange(records);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
