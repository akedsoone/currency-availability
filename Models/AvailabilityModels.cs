namespace CurrencyAvailability.Models;

public class City
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class Department
{
    public int Id { get; set; }
    public required string ExternalCode { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public int CityId { get; set; }
    public City City { get; set; } = null!;
}

public class Currency
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}

public class AvailabilityRecord
{
    public int DepartmentId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime MeasuredAt { get; set; }
    public AvailabilityLevel Level { get; set; }
}

public enum AvailabilityLevel
{
    None,
    Low,
    Enough,
    High
}
