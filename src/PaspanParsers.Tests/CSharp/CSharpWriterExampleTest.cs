namespace PaspanParsers.Tests.CSharp;

[TestClass]
public class CSharpWriterExampleTest
{
    [TestMethod]
    public void GeneratePersonClass_ProducesValidCode()
    {
        var code = CSharpWriterExample.GeneratePersonClass();
        
        Console.WriteLine("Generated Person Class:");
        Console.WriteLine(code);
        
        Assert.Contains("using System;", code);
        Assert.Contains("namespace MyApp.Models", code);
        Assert.Contains("public class Person", code);
        Assert.Contains("public string Name", code);
        Assert.Contains("public int Age", code);
        Assert.Contains("public string GetInfo()", code);
    }

    [TestMethod]
    public void GenerateRepositoryInterface_ProducesValidCode()
    {
        var code = CSharpWriterExample.GenerateRepositoryInterface();
        
        Console.WriteLine("Generated Repository Interface:");
        Console.WriteLine(code);
        
        Assert.Contains("using System;", code);
        Assert.Contains("using System.Collections.Generic;", code);
        Assert.Contains("namespace MyApp.Data;", code);
        Assert.Contains("public interface IRepository<T>", code);
        Assert.Contains("T GetById(int id);", code);
        Assert.Contains("IEnumerable<T> GetAll();", code);
        Assert.Contains("void Add(T entity);", code);
        Assert.Contains("void Delete(int id);", code);
    }

    [TestMethod]
    public void GenerateProductRecord_ProducesValidCode()
    {
        var code = CSharpWriterExample.GenerateProductRecord();
        
        Console.WriteLine("Generated Product Record:");
        Console.WriteLine(code);
        
        Assert.Contains("namespace MyApp.Models;", code);
        Assert.Contains("public record Product(int Id, string Name, decimal Price);", code);
    }
}

