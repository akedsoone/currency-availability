using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CurrencyAvailability.Services;

public class BankApiOptions
{
    public string Mode { get; set; } = "Demo";
    public string Url { get; set; } = "";
    public string CatalogPath { get; set; } = "Data/demo-catalog.json";

    public bool IsDemo => Mode.Equals("Demo", StringComparison.OrdinalIgnoreCase);
    public bool HasBankUrl => Mode.Equals("Bank", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(CatalogPath)
        && !string.Equals(Path.GetFileName(CatalogPath), "demo-catalog.json", StringComparison.OrdinalIgnoreCase)
        && Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public record CatalogCurrency(string Code, string Name);
public record CatalogDepartment(string Code, string Name, string Address, string City);

public class BankCatalog
{
    private record CatalogData(CatalogCurrency[] Currencies, CatalogDepartment[] Departments);

    public IReadOnlyList<CatalogCurrency> Currencies { get; }
    public IReadOnlyList<CatalogDepartment> Departments { get; }

    public BankCatalog(IOptions<BankApiOptions> options, IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, options.Value.CatalogPath);
        var catalog = JsonSerializer.Deserialize<CatalogData>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (catalog is null || catalog.Currencies is not { Length: > 0 } || catalog.Departments is not { Length: > 0 }
            || catalog.Currencies.Any(item => string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name))
            || catalog.Departments.Any(item => string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name)
                || string.IsNullOrWhiteSpace(item.Address) || string.IsNullOrWhiteSpace(item.City))
            || catalog.Currencies.Select(item => item.Code).Distinct().Count() != catalog.Currencies.Length
            || catalog.Departments.Select(item => item.Code).Distinct().Count() != catalog.Departments.Length)
        {
            throw new InvalidOperationException("Проверьте поля и уникальность кодов в каталоге отделений и валют.");
        }

        Currencies = catalog.Currencies;
        Departments = catalog.Departments;
    }
}
