using System.Globalization;
using System.Text.Json;
using CurrencyAvailability.Models;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace CurrencyAvailability.Services;

public record BankQuote(string DepartmentCode, string CurrencyCode, string Level);

public class BankApiClient(HttpClient http, IOptions<BankApiOptions> options, IServer server)
{
    private record RankedResponse(List<RankedDepartment>? DepartmentAmounts);
    private record RankedDepartment(int DepartmentId, List<RankedCurrency>? CurrencyAmounts);
    private record RankedCurrency(string Currency, int Amount);

    public async Task<List<BankQuote>> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var url = options.Value.Url;
        if (options.Value.IsDemo)
        {
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
            url = new Uri(new Uri(address), "/api/demo/availability").AbsoluteUri;
        }

        var response = await http.GetFromJsonAsync<JsonElement>(url, cancellationToken);
        if (response.ValueKind == JsonValueKind.Array)
        {
            return response.Deserialize<List<BankQuote>>(JsonSerializerOptions.Web)!;
        }

        var departments = response.Deserialize<RankedResponse>(JsonSerializerOptions.Web)?.DepartmentAmounts
            ?? throw new InvalidDataException("API не вернул список отделений.");
        var quotes = new List<BankQuote>();
        foreach (var department in departments)
        {
            var code = department.DepartmentId.ToString(CultureInfo.InvariantCulture);
            foreach (var currency in department.CurrencyAmounts ?? [])
            {
                if (currency.Amount is >= 1 and <= 4)
                {
                    // Банк нумерует уровни от 1, в нашем enum они идут от 0.
                    var level = (AvailabilityLevel)(currency.Amount - 1);
                    quotes.Add(new BankQuote(code, currency.Currency, level.ToString()));
                }
            }
        }

        return quotes;
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
