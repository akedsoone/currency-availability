using System.Text.Json;

namespace CurrencyAvailability.Services;

public record CatalogCurrency(string Code, string Name);
public record CatalogDepartment(string Code, string Name, string Address, string City);

public class BankCatalog
{
    private record CatalogData(CatalogCurrency[] Currencies, CatalogDepartment[] Departments);

    public IReadOnlyList<CatalogCurrency> Currencies { get; }
    public IReadOnlyList<CatalogDepartment> Departments { get; }

    public BankCatalog(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "demo-catalog.json");
        var catalog = JsonSerializer.Deserialize<CatalogData>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (catalog is null || catalog.Currencies is not { Length: > 0 } || catalog.Departments is not { Length: > 0 })
        {
            throw new InvalidOperationException("Проверьте поля и уникальность кодов в каталоге отделений и валют.");
        }

        Currencies = catalog.Currencies;
        Departments = catalog.Departments;
    }
}
