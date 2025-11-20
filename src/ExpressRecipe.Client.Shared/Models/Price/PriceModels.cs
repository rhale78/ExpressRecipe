namespace ExpressRecipe.Client.Shared.Models.Price;

// Product Price History
public class ProductPriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Size { get; set; }
    public string? Unit { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid? RecordedByUserId { get; set; }
}

public class RecordPriceRequest
{
    public Guid ProductId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Size { get; set; }
    public string? Unit { get; set; }
    public DateTime? RecordedAt { get; set; }
}

// Price History Summary
public class ProductPriceHistoryDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal LowestPrice { get; set; }
    public decimal HighestPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public List<ProductPriceDto> PriceHistory { get; set; } = new();
    public List<StorePriceDto> PricesByStore { get; set; } = new();
}

public class StorePriceDto
{
    public string StoreName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public DateTime LastUpdated { get; set; }
    public int PriceCount { get; set; }
}

// Price Alerts
public class PriceAlertDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public string? StoreName { get; set; }
    public bool IsActive { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePriceAlertRequest
{
    public Guid ProductId { get; set; }
    public decimal TargetPrice { get; set; }
    public string? StoreName { get; set; }
}

public class UpdatePriceAlertRequest
{
    public Guid AlertId { get; set; }
    public decimal TargetPrice { get; set; }
    public bool IsActive { get; set; }
}

// Shopping Budget
public class ShoppingBudgetDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public decimal CurrentSpending { get; set; }
    public decimal RemainingBudget => MonthlyLimit - CurrentSpending;
    public int DaysRemaining { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateShoppingBudgetRequest
{
    public string BudgetName { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class UpdateShoppingBudgetRequest
{
    public Guid BudgetId { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public bool IsActive { get; set; }
}

// Budget Transactions
public class BudgetTransactionDto
{
    public Guid Id { get; set; }
    public Guid BudgetId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
    public List<TransactionItemDto> Items { get; set; } = new();
}

public class TransactionItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class RecordBudgetTransactionRequest
{
    public Guid BudgetId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
    public List<TransactionItemRequest> Items { get; set; } = new();
}

public class TransactionItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// Budget Analytics
public class BudgetAnalyticsDto
{
    public Guid BudgetId { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public decimal TotalBudget { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal RemainingBudget { get; set; }
    public double PercentageUsed { get; set; }
    public decimal AverageDailySpending { get; set; }
    public decimal ProjectedEndOfMonthSpending { get; set; }
    public List<DailySpendingDto> DailySpending { get; set; } = new();
    public List<CategorySpendingDto> SpendingByCategory { get; set; } = new();
    public List<StoreSpendingDto> SpendingByStore { get; set; } = new();
}

public class DailySpendingDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

public class CategorySpendingDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
}

public class StoreSpendingDto
{
    public string StoreName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

// Price Comparison
public class PriceComparisonRequest
{
    public List<Guid> ProductIds { get; set; } = new();
    public List<string>? StoreNames { get; set; }
}

public class PriceComparisonResult
{
    public List<ProductComparisonDto> Products { get; set; } = new();
    public List<StoreTotalDto> StoreTotals { get; set; } = new();
    public string BestStore { get; set; } = string.Empty;
    public decimal BestStoreTotal { get; set; }
    public decimal PotentialSavings { get; set; }
}

public class ProductComparisonDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public Dictionary<string, decimal> PricesByStore { get; set; } = new();
    public string BestStore { get; set; } = string.Empty;
    public decimal BestPrice { get; set; }
}

public class StoreTotalDto
{
    public string StoreName { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public int AvailableProducts { get; set; }
    public int MissingProducts { get; set; }
}

// Best Price Alerts
public class BestPriceAlertDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal PreviousBestPrice { get; set; }
    public decimal SavingsAmount { get; set; }
    public double SavingsPercentage { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsNotified { get; set; }
}

// Store Preferences
public class StorePreferenceDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public int? Priority { get; set; } // 1 = highest priority
    public string? Notes { get; set; }
}

public class UpdateStorePreferenceRequest
{
    public string StoreName { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public int? Priority { get; set; }
    public string? Notes { get; set; }
}

// Price Trends
public class PriceTrendDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal PriceChange { get; set; }
    public double PriceChangePercentage { get; set; }
    public string Trend { get; set; } = "Stable"; // Rising, Falling, Stable
    public DateTime LastUpdated { get; set; }
}

public class GetPriceTrendsRequest
{
    public string? Category { get; set; }
    public int Days { get; set; } = 30;
    public string TrendType { get; set; } = "All"; // All, Rising, Falling, Stable
}
