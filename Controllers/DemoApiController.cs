using CurrencyAvailability.Services;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyAvailability.Controllers;

[ApiController]
public class DemoApiController(BankCatalog catalog) : ControllerBase
{
    [HttpGet("/api/demo/availability")]
    [ResponseCache(NoStore = true)]
    public ActionResult<List<BankQuote>> GetAvailability()
    {
        var now = DateTime.UtcNow;
        var quotes = new List<BankQuote>();
        for (var department = 0; department < catalog.Departments.Count; department++)
        {
            for (var currency = 0; currency < catalog.Currencies.Count; currency++)
            {
                quotes.Add(new BankQuote(catalog.Departments[department].Code,
                    catalog.Currencies[currency].Code, DemoAvailability.GetLevel(now, department, currency).ToString()));
            }
        }

        return quotes;
    }
}
