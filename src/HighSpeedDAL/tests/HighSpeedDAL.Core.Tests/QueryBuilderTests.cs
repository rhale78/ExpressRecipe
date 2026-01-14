using System;
using System.Collections.Generic;
using Xunit;
using HighSpeedDAL.Core.Querying;

namespace HighSpeedDAL.Phase3.Tests;

/// <summary>
/// Comprehensive tests for advanced query builder.
/// Tests SQL generation, filtering, sorting, paging, and joins.
/// 
/// HighSpeedDAL Framework v0.1 - Phase 3
/// </summary>
public sealed class QueryBuilderTests
{
    [Fact]
    public void ToSql_SimpleSelect_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>();

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("SELECT *", sql);
        Assert.Contains("FROM Product", sql);
    }

    [Fact]
    public void Where_SingleCondition_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Price > 100);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Price > @p0", sql);
        
        Dictionary<string, object?> parameters = query.GetParameters();
        Assert.Contains("@p0", parameters.Keys);
        Assert.Equal(100, parameters["@p0"]);
    }

    [Fact]
    public void Where_MultipleConditions_CombinesWithAnd()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Price > 100)
            .Where(p => p.Category == "Electronics");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Price > @p0 AND Category = @p1", sql);
        
        Dictionary<string, object?> parameters = query.GetParameters();
        Assert.Equal(100, parameters["@p0"]);
        Assert.Equal("Electronics", parameters["@p1"]);
    }

    [Fact]
    public void OrWhere_CombinesWithOr()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Price > 100)
            .OrWhere(p => p.Category == "Electronics");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Price > @p0 OR Category = @p1", sql);
    }

    [Fact]
    public void OrderBy_SingleColumn_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .OrderBy(p => p.Price, descending: true);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("ORDER BY Price DESC", sql);
    }

    [Fact]
    public void OrderBy_MultipleColumns_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Price, descending: true);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("ORDER BY Category ASC, Price DESC", sql);
    }

    [Fact]
    public void SkipTake_Pagination_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .OrderBy(p => p.Id)
            .Skip(20)
            .Take(10);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("OFFSET 20 ROWS", sql);
        Assert.Contains("FETCH NEXT 10 ROWS ONLY", sql);
    }

    [Fact]
    public void Skip_WithoutOrderBy_AddsDefaultOrderBy()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Skip(10);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("ORDER BY (SELECT NULL)", sql);
        Assert.Contains("OFFSET 10 ROWS", sql);
    }

    [Fact]
    public void Distinct_AppliesDistinct()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Distinct()
            .Select(p => p.Category);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("SELECT DISTINCT Category", sql);
    }

    [Fact]
    public void Select_SpecificColumns_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Select(p => p.Id)
            .Select(p => p.Name)
            .Select(p => p.Price, "ProductPrice");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("SELECT Id, Name, Price AS ProductPrice", sql);
        Assert.DoesNotContain("SELECT *", sql);
    }

    [Fact]
    public void InnerJoin_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Order> query = new QueryBuilder<Order>()
            .InnerJoin<Customer>("Customer", o => o.CustomerId, c => c.Id);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("INNER JOIN Customer ON Order.CustomerId = Customer.Id", sql);
    }

    [Fact]
    public void LeftJoin_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Order> query = new QueryBuilder<Order>()
            .LeftJoin<Customer>("Customer", o => o.CustomerId, c => c.Id);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("LEFT JOIN Customer ON Order.CustomerId = Customer.Id", sql);
    }

    [Fact]
    public void GroupBy_WithHaving_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Order> query = new QueryBuilder<Order>()
            .GroupBy(o => o.CustomerId)
            .Having("COUNT(*) > 5");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("GROUP BY CustomerId", sql);
        Assert.Contains("HAVING COUNT(*) > 5", sql);
    }

    [Fact]
    public void WhereRaw_AcceptsRawSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .WhereRaw("Price BETWEEN 100 AND 500");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Price BETWEEN 100 AND 500", sql);
    }

    [Fact]
    public void ToCountSql_GeneratesCountQuery()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Price > 100)
            .OrderBy(p => p.Name)
            .Skip(10)
            .Take(20);

        // Act
        string countSql = query.ToCountSql();

        // Assert
        Assert.Contains("SELECT COUNT(*)", countSql);
        Assert.Contains("FROM Product", countSql);
        Assert.Contains("WHERE Price > @p0", countSql);
        Assert.DoesNotContain("ORDER BY", countSql); // ORDER BY not needed for count
        Assert.DoesNotContain("OFFSET", countSql); // Pagination not needed for count
    }

    [Fact]
    public void ComplexQuery_MultipleOperations_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Price > 50)
            .Where(p => p.Category == "Electronics")
            .OrderBy(p => p.Price, descending: true)
            .ThenBy(p => p.Name)
            .Skip(30)
            .Take(15);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("SELECT *", sql);
        Assert.Contains("FROM Product", sql);
        Assert.Contains("WHERE Price > @p0 AND Category = @p1", sql);
        Assert.Contains("ORDER BY Price DESC, Name ASC", sql);
        Assert.Contains("OFFSET 30 ROWS", sql);
        Assert.Contains("FETCH NEXT 15 ROWS ONLY", sql);
    }

    [Fact]
    public void FromTable_CustomTableName_UsesCustomName()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .FromTable("Products");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("FROM Products", sql);
        Assert.DoesNotContain("FROM Product", sql);
    }

    [Fact]
    public void Where_NotEqualOperator_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Category != "Discontinued");

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Category != @p0", sql);
        Assert.Equal("Discontinued", query.GetParameters()["@p0"]);
    }

    [Fact]
    public void Where_LessThanOperator_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Stock < 10);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Stock < @p0", sql);
        Assert.Equal(10, query.GetParameters()["@p0"]);
    }

    [Fact]
    public void Where_GreaterThanOrEqual_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Price >= 100);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Price >= @p0", sql);
    }

    [Fact]
    public void Where_ContainsMethod_GeneratesLikeSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Name.Contains("Phone"));

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Name LIKE '%' + @p0 + '%'", sql);
        Assert.Equal("Phone", query.GetParameters()["@p0"]);
    }

    [Fact]
    public void Where_StartsWithMethod_GeneratesLikeSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Name.StartsWith("Smart"));

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Name LIKE @p0 + '%'", sql);
        Assert.Equal("Smart", query.GetParameters()["@p0"]);
    }

    [Fact]
    public void Where_EndsWithMethod_GeneratesLikeSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .Where(p => p.Name.EndsWith("Pro"));

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE Name LIKE '%' + @p0", sql);
        Assert.Equal("Pro", query.GetParameters()["@p0"]);
    }

    [Fact]
    public void Take_WithoutSkip_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>()
            .OrderBy(p => p.Id)
            .Take(10);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("OFFSET 0 ROWS", sql);
        Assert.Contains("FETCH NEXT 10 ROWS ONLY", sql);
    }

    [Fact]
    public void MultipleJoins_GeneratesCorrectSql()
    {
        // Arrange
        QueryBuilder<Order> query = new QueryBuilder<Order>()
            .InnerJoin<Customer>("Customer", o => o.CustomerId, c => c.Id)
            .LeftJoin<Product>("Product", o => o.ProductId, p => p.Id);

        // Act
        string sql = query.ToSql();

        // Assert
        Assert.Contains("INNER JOIN Customer ON Order.CustomerId = Customer.Id", sql);
        Assert.Contains("LEFT JOIN Product ON Order.ProductId = Product.Id", sql);
    }

    [Fact]
    public void Skip_NegativeValue_ThrowsException()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>();

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() => query.Skip(-10));
        Assert.Contains("Skip count must be >= 0", exception.Message);
    }

    [Fact]
    public void Take_ZeroValue_ThrowsException()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>();

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() => query.Take(0));
        Assert.Contains("Take count must be >= 1", exception.Message);
    }

    [Fact]
    public void FromTable_EmptyString_ThrowsException()
    {
        // Arrange
        QueryBuilder<Product> query = new QueryBuilder<Product>();

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() => query.FromTable(""));
        Assert.Contains("Table name cannot be null or empty", exception.Message);
    }
}

// Test entities

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Stock { get; set; }
}

public sealed class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public DateTime OrderDate { get; set; }
}

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
