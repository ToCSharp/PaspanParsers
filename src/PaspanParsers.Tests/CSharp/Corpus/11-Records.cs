using System;

namespace Corpus.Records;

public record Person(string FirstName, string LastName)
{
    public string FullName => $"{FirstName} {LastName}";
}

public record class Employee(string FirstName, string LastName, decimal Salary) : Person(FirstName, LastName);

public record struct Coordinate(double Latitude, double Longitude);

public readonly record struct Money(decimal Amount, string Currency);

public sealed record Empty;

public record Mutable
{
    public required int Id { get; init; }
    public string Name { get; set; } = "";
}

public class Service(ILogger logger, int retries = 3)
{
    public void Run() => logger.Log(retries.ToString());
}

public interface ILogger { void Log(string message); }

public static class Usage
{
    public static void Use()
    {
        var p = new Person("Ada", "Lovelace");
        var q = p with { LastName = "Byron" };
        var c = new Coordinate(1, 2) with { Latitude = 3 };
        var (first, last) = p;
    }
}
