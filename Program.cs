using CurrencyAvailability.Data;
using CurrencyAvailability.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddOptions<BankApiOptions>()
    .BindConfiguration("BankApi")
    .Validate(options => options.IsDemo || options.HasBankUrl,
        "Укажите режим Demo или режим Bank с URL банка и отдельным каталогом отделений.")
    .ValidateOnStart();
builder.Services.AddSingleton<BankCatalog>();
builder.Services.AddHttpClient<BankApiClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHostedService<AvailabilityWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseStaticFiles();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseSetup.InitializeAsync(scope.ServiceProvider);
}

await app.RunAsync();
