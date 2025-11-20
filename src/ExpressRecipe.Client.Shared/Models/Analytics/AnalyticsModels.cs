namespace ExpressRecipe.Client.Shared.Models.Analytics;

// Dashboard Summary
public class AnalyticsDashboardDto
{
    public SpendingSummaryDto SpendingSummary { get; set; } = new();
    public NutritionSummaryDto NutritionSummary { get; set; } = new();
    public InventorySummaryDto InventorySummary { get; set; } = new();
    public WasteSummaryDto WasteSummary { get; set; } = new();
}

// Spending Analytics
public class SpendingSummaryDto
{
    public decimal TotalSpentThisMonth { get; set; }
    public decimal TotalSpentLastMonth { get; set; }
    public decimal AverageDailySpending { get; set; }
    public decimal BudgetRemaining { get; set; }
    public decimal MonthlyBudget { get; set; }
    public List<SpendingByCategoryDto> SpendingByCategory { get; set; } = new();
    public List<DailySpendingDto> DailySpending { get; set; } = new();
}

public class SpendingByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ItemCount { get; set; }
    public decimal Percentage { get; set; }
}

public class DailySpendingDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

public class SpendingReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Category { get; set; }
    public string? Store { get; set; }
    public string GroupBy { get; set; } = "Day"; // Day, Week, Month
}

public class SpendingReportDto
{
    public decimal TotalSpent { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransactionAmount { get; set; }
    public List<SpendingDataPointDto> DataPoints { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
    public List<TopStoreDto> TopStores { get; set; } = new();
}

public class SpendingDataPointDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int PurchaseCount { get; set; }
}

public class TopStoreDto
{
    public string StoreName { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int TransactionCount { get; set; }
}

// Nutrition Analytics
public class NutritionSummaryDto
{
    public int TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public decimal TotalFiber { get; set; }
    public int DailyAverageCalories { get; set; }
    public List<DailyNutritionDto> DailyBreakdown { get; set; } = new();
}

public class DailyNutritionDto
{
    public DateTime Date { get; set; }
    public int Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbs { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
}

public class NutritionReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IncludeMealPlans { get; set; } = true;
    public bool IncludeConsumed { get; set; } = true;
}

public class NutritionReportDto
{
    public int TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public decimal TotalFiber { get; set; }
    public decimal TotalSugar { get; set; }
    public decimal TotalSodium { get; set; }
    public List<DailyNutritionDto> DailyData { get; set; } = new();
    public Dictionary<string, decimal> MacroBreakdown { get; set; } = new();
    public List<TopNutrientSourceDto> TopProteinSources { get; set; } = new();
    public List<TopNutrientSourceDto> TopFiberSources { get; set; } = new();
}

public class TopNutrientSourceDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
}

// Inventory Analytics
public class InventorySummaryDto
{
    public int TotalItems { get; set; }
    public int UniqueProducts { get; set; }
    public int ItemsExpiringSoon { get; set; }
    public int LowStockItems { get; set; }
    public decimal TotalValue { get; set; }
    public List<CategoryStockDto> StockByCategory { get; set; } = new();
    public List<InventoryTrendDto> InventoryTrend { get; set; } = new();
}

public class CategoryStockDto
{
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalValue { get; set; }
    public int ExpiringCount { get; set; }
}

public class InventoryTrendDto
{
    public DateTime Date { get; set; }
    public int TotalItems { get; set; }
    public int AddedItems { get; set; }
    public int RemovedItems { get; set; }
}

public class InventoryReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Category { get; set; }
    public bool IncludeExpired { get; set; } = false;
}

public class InventoryReportDto
{
    public int CurrentStock { get; set; }
    public int ItemsAdded { get; set; }
    public int ItemsConsumed { get; set; }
    public int ItemsExpired { get; set; }
    public decimal TurnoverRate { get; set; }
    public List<InventoryTrendDto> TrendData { get; set; } = new();
    public List<MostConsumedProductDto> MostConsumedProducts { get; set; } = new();
    public List<MostWastedProductDto> MostWastedProducts { get; set; } = new();
}

public class MostConsumedProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int ConsumedCount { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class MostWastedProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int ExpiredCount { get; set; }
    public decimal EstimatedValue { get; set; }
}

// Waste Analytics
public class WasteSummaryDto
{
    public int TotalItemsWasted { get; set; }
    public decimal TotalValueWasted { get; set; }
    public int ItemsWastedThisMonth { get; set; }
    public decimal ValueWastedThisMonth { get; set; }
    public List<WasteByCategoryDto> WasteByCategory { get; set; } = new();
    public List<WasteTrendDto> WasteTrend { get; set; } = new();
}

public class WasteByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal EstimatedValue { get; set; }
}

public class WasteTrendDto
{
    public DateTime Date { get; set; }
    public int ItemsWasted { get; set; }
    public decimal ValueWasted { get; set; }
}

public class WasteReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Category { get; set; }
}

public class WasteReportDto
{
    public int TotalItemsExpired { get; set; }
    public decimal TotalValueWasted { get; set; }
    public decimal AverageWastePerWeek { get; set; }
    public List<WasteTrendDto> TrendData { get; set; } = new();
    public List<MostWastedProductDto> MostWastedProducts { get; set; } = new();
    public List<WasteByCategoryDto> WasteByCategory { get; set; } = new();
    public List<WasteReasonDto> WasteReasons { get; set; } = new();
}

public class WasteReasonDto
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

// Export Options
public class ExportReportRequest
{
    public string ReportType { get; set; } = string.Empty; // Spending, Nutrition, Inventory, Waste
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = "PDF"; // PDF, CSV, Excel
}

public class ExportReportResponse
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}

// Date Range Presets
public static class DateRangePresets
{
    public static (DateTime Start, DateTime End) Today =>
        (DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1));

    public static (DateTime Start, DateTime End) Yesterday =>
        (DateTime.Today.AddDays(-1), DateTime.Today.AddTicks(-1));

    public static (DateTime Start, DateTime End) Last7Days =>
        (DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1).AddTicks(-1));

    public static (DateTime Start, DateTime End) Last30Days =>
        (DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1).AddTicks(-1));

    public static (DateTime Start, DateTime End) ThisMonth =>
        (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
         DateTime.Today.AddDays(1).AddTicks(-1));

    public static (DateTime Start, DateTime End) LastMonth
    {
        get
        {
            var firstDayLastMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            var lastDayLastMonth = firstDayLastMonth.AddMonths(1).AddTicks(-1);
            return (firstDayLastMonth, lastDayLastMonth);
        }
    }

    public static (DateTime Start, DateTime End) ThisYear =>
        (new DateTime(DateTime.Today.Year, 1, 1),
         DateTime.Today.AddDays(1).AddTicks(-1));

    public static (DateTime Start, DateTime End) LastYear =>
        (new DateTime(DateTime.Today.Year - 1, 1, 1),
         new DateTime(DateTime.Today.Year, 1, 1).AddTicks(-1));
}
