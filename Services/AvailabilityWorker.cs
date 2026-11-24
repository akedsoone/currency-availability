using CurrencyAvailability.Data;
using CurrencyAvailability.Models;
using Microsoft.EntityFrameworkCore;

namespace CurrencyAvailability.Services;

public class AvailabilityWorker(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<AvailabilityWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public static DateTime GetSlot(DateTime utcTime) =>
        new(utcTime.Ticks - utcTime.Ticks % Interval.Ticks, DateTimeKind.Utc);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var slot = GetSlot(DateTime.UtcNow);

            try
            {
                await SaveMeasurementsAsync(slot, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Не удалось сохранить замеры. Следующая попытка через интервал опроса.");
            }

            var delay = slot + Interval - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task SaveMeasurementsAsync(DateTime slot, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<BankApiClient>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quotes = await client.GetAvailabilityAsync(cancellationToken);

        var departments = await db.Departments.ToDictionaryAsync(item => item.ExternalCode, item => item.Id, cancellationToken);
        var currencies = await db.Currencies.ToDictionaryAsync(item => item.Code, item => item.Id, cancellationToken);
        var existing = await db.AvailabilityRecords.Where(record => record.MeasuredAt == slot)
            .Select(record => new { record.DepartmentId, record.CurrencyId })
            .ToListAsync(cancellationToken);
        var saved = existing.Select(item => (item.DepartmentId, item.CurrencyId)).ToHashSet();
        var skipped = 0;

        foreach (var quote in quotes)
        {
            if (quote is null || string.IsNullOrWhiteSpace(quote.DepartmentCode) || string.IsNullOrWhiteSpace(quote.CurrencyCode)
                || !departments.TryGetValue(quote.DepartmentCode, out var departmentId)
                || !currencies.TryGetValue(quote.CurrencyCode, out var currencyId)
                || !Enum.TryParse<AvailabilityLevel>(quote.Level, ignoreCase: true, out var level)
                || !string.Equals(quote.Level, level.ToString(), StringComparison.OrdinalIgnoreCase)
                || !Enum.IsDefined(level))
            {
                skipped++;
                continue;
            }

            if (saved.Add((departmentId, currencyId)))
            {
                db.AvailabilityRecords.Add(new AvailabilityRecord
                {
                    DepartmentId = departmentId,
                    CurrencyId = currencyId,
                    MeasuredAt = slot,
                    Level = level
                });
            }
        }

        var count = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Замер {Time:O}. Сохранено: {Count}. Пропущено некорректных: {Skipped}.", slot, count, skipped);
    }
}
