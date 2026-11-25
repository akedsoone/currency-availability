using CurrencyAvailability.Models;
using Microsoft.EntityFrameworkCore;

namespace CurrencyAvailability.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<AvailabilityRecord> AvailabilityRecords => Set<AvailabilityRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>().HasIndex(city => city.Name).IsUnique();
        modelBuilder.Entity<Department>().HasIndex(department => department.ExternalCode).IsUnique();
        modelBuilder.Entity<Currency>().HasIndex(currency => currency.Code).IsUnique();

        var records = modelBuilder.Entity<AvailabilityRecord>();
        records.HasKey(record => new { record.DepartmentId, record.CurrencyId, record.MeasuredAt });
        records.HasIndex(record => record.MeasuredAt); //быстрый поиск
        records.HasOne<Department>().WithMany().HasForeignKey(record => record.DepartmentId);
        records.HasOne<Currency>().WithMany().HasForeignKey(record => record.CurrencyId);
    }
}
