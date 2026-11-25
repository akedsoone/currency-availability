using CurrencyAvailability.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace CurrencyAvailability.Services;

public record BankQuote(string DepartmentCode, string CurrencyCode, string Level);

public class BankApiClient(HttpClient http, IServer server)
{
    public async Task<List<BankQuote>> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var url = new Uri(new Uri(address), "/api/demo/availability");
        return await http.GetFromJsonAsync<List<BankQuote>>(url, cancellationToken)
            ?? throw new InvalidDataException("Демо-API не вернул данные.");
    }
}

public static class DemoAvailability
{
    public static AvailabilityLevel GetLevel(DateTime measuredAt, int departmentIndex, int currencyIndex)
    {
        var slot = measuredAt.Ticks / AvailabilityWorker.Interval.Ticks;
        var phase = (slot / 31 + departmentIndex * 5 + currencyIndex * 3) % 29;
        return phase switch
        {
            < 3 => AvailabilityLevel.None,
            < 9 => AvailabilityLevel.Low,
            < 21 => AvailabilityLevel.Enough,
            _ => AvailabilityLevel.High
        };
    }
}
