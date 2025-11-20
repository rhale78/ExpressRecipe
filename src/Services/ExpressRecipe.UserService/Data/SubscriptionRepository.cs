using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public class SubscriptionRepository : SqlHelper, ISubscriptionRepository
{
    public SubscriptionRepository(string connectionString) : base(connectionString) { }

    #region Subscription Tiers

    public async Task<List<SubscriptionTierDto>> GetSubscriptionTiersAsync()
    {
        const string sql = @"
            SELECT Id, TierName, DisplayName, MonthlyPrice, YearlyPrice,
                   Features, MaxFamilyMembers, AllowsOfflineSync, AllowsAdvancedReports,
                   AllowsPriceComparison, AllowsRecipeImport, AllowsMenuPlanning,
                   PointsMultiplier, IsActive, SortOrder
            FROM SubscriptionTier
            WHERE IsActive = 1
            ORDER BY SortOrder";

        return await ExecuteReaderAsync(sql, MapTierToDto);
    }

    public async Task<SubscriptionTierDto?> GetSubscriptionTierByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, TierName, DisplayName, MonthlyPrice, YearlyPrice,
                   Features, MaxFamilyMembers, AllowsOfflineSync, AllowsAdvancedReports,
                   AllowsPriceComparison, AllowsRecipeImport, AllowsMenuPlanning,
                   PointsMultiplier, IsActive, SortOrder
            FROM SubscriptionTier
            WHERE Id = @Id";

        var tiers = await ExecuteReaderAsync(sql, MapTierToDto,
            new SqlParameter("@Id", id));

        return tiers.FirstOrDefault();
    }

    public async Task<SubscriptionTierDto?> GetSubscriptionTierByNameAsync(string tierName)
    {
        const string sql = @"
            SELECT Id, TierName, DisplayName, MonthlyPrice, YearlyPrice,
                   Features, MaxFamilyMembers, AllowsOfflineSync, AllowsAdvancedReports,
                   AllowsPriceComparison, AllowsRecipeImport, AllowsMenuPlanning,
                   PointsMultiplier, IsActive, SortOrder
            FROM SubscriptionTier
            WHERE TierName = @TierName AND IsActive = 1";

        var tiers = await ExecuteReaderAsync(sql, MapTierToDto,
            new SqlParameter("@TierName", tierName));

        return tiers.FirstOrDefault();
    }

    private static SubscriptionTierDto MapTierToDto(SqlDataReader reader)
    {
        return new SubscriptionTierDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            TierName = reader.GetString(reader.GetOrdinal("TierName")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            MonthlyPrice = reader.GetDecimal(reader.GetOrdinal("MonthlyPrice")),
            YearlyPrice = reader.GetDecimal(reader.GetOrdinal("YearlyPrice")),
            Features = reader.IsDBNull(reader.GetOrdinal("Features"))
                ? null
                : reader.GetString(reader.GetOrdinal("Features")),
            MaxFamilyMembers = reader.GetInt32(reader.GetOrdinal("MaxFamilyMembers")),
            AllowsOfflineSync = reader.GetBoolean(reader.GetOrdinal("AllowsOfflineSync")),
            AllowsAdvancedReports = reader.GetBoolean(reader.GetOrdinal("AllowsAdvancedReports")),
            AllowsPriceComparison = reader.GetBoolean(reader.GetOrdinal("AllowsPriceComparison")),
            AllowsRecipeImport = reader.GetBoolean(reader.GetOrdinal("AllowsRecipeImport")),
            AllowsMenuPlanning = reader.GetBoolean(reader.GetOrdinal("AllowsMenuPlanning")),
            PointsMultiplier = reader.GetDecimal(reader.GetOrdinal("PointsMultiplier")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder"))
        };
    }

    #endregion

    #region User Subscriptions

    public async Task<UserSubscriptionDto?> GetUserSubscriptionAsync(Guid userId)
    {
        const string sql = @"
            SELECT us.Id, us.UserId, us.SubscriptionTierId, us.Status, us.BillingCycle,
                   us.StartDate, us.EndDate, us.NextBillingDate, us.AutoRenew,
                   us.PaymentMethodId, us.CancellationDate, us.CancellationReason,
                   st.TierName, st.DisplayName, st.MonthlyPrice, st.YearlyPrice
            FROM UserSubscription us
            INNER JOIN SubscriptionTier st ON us.SubscriptionTierId = st.Id
            WHERE us.UserId = @UserId
              AND us.Status IN ('Active', 'PastDue', 'Cancelled')
            ORDER BY us.StartDate DESC";

        var subscriptions = await ExecuteReaderAsync(sql, reader => new UserSubscriptionDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            SubscriptionTierId = reader.GetGuid(reader.GetOrdinal("SubscriptionTierId")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            BillingCycle = reader.GetString(reader.GetOrdinal("BillingCycle")),
            StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")),
            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("EndDate")),
            NextBillingDate = reader.IsDBNull(reader.GetOrdinal("NextBillingDate"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("NextBillingDate")),
            AutoRenew = reader.GetBoolean(reader.GetOrdinal("AutoRenew")),
            PaymentMethodId = reader.IsDBNull(reader.GetOrdinal("PaymentMethodId"))
                ? null
                : reader.GetString(reader.GetOrdinal("PaymentMethodId")),
            CancellationDate = reader.IsDBNull(reader.GetOrdinal("CancellationDate"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("CancellationDate")),
            CancellationReason = reader.IsDBNull(reader.GetOrdinal("CancellationReason"))
                ? null
                : reader.GetString(reader.GetOrdinal("CancellationReason")),
            TierName = reader.GetString(reader.GetOrdinal("TierName")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            MonthlyPrice = reader.GetDecimal(reader.GetOrdinal("MonthlyPrice")),
            YearlyPrice = reader.GetDecimal(reader.GetOrdinal("YearlyPrice"))
        }, new SqlParameter("@UserId", userId));

        return subscriptions.FirstOrDefault();
    }

    public async Task<Guid> SubscribeAsync(Guid userId, SubscribeRequest request)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            // Cancel any existing active subscriptions
            const string cancelSql = @"
                UPDATE UserSubscription
                SET Status = 'Expired',
                    EndDate = GETUTCDATE(),
                    AutoRenew = 0
                WHERE UserId = @UserId AND Status = 'Active'";

            var cancelCmd = new SqlCommand(cancelSql, conn, txn);
            cancelCmd.Parameters.AddWithValue("@UserId", userId);
            await cancelCmd.ExecuteNonQueryAsync();

            // Create new subscription
            var subscriptionId = Guid.NewGuid();
            var startDate = DateTime.UtcNow;
            var nextBillingDate = request.BillingCycle == "Monthly"
                ? startDate.AddMonths(1)
                : startDate.AddYears(1);

            const string insertSql = @"
                INSERT INTO UserSubscription
                    (Id, UserId, SubscriptionTierId, Status, BillingCycle, StartDate,
                     NextBillingDate, AutoRenew, PaymentMethodId, CreatedAt)
                VALUES
                    (@Id, @UserId, @SubscriptionTierId, 'Active', @BillingCycle, @StartDate,
                     @NextBillingDate, @AutoRenew, @PaymentMethodId, GETUTCDATE())";

            var insertCmd = new SqlCommand(insertSql, conn, txn);
            insertCmd.Parameters.AddWithValue("@Id", subscriptionId);
            insertCmd.Parameters.AddWithValue("@UserId", userId);
            insertCmd.Parameters.AddWithValue("@SubscriptionTierId", request.SubscriptionTierId);
            insertCmd.Parameters.AddWithValue("@BillingCycle", request.BillingCycle);
            insertCmd.Parameters.AddWithValue("@StartDate", startDate);
            insertCmd.Parameters.AddWithValue("@NextBillingDate", nextBillingDate);
            insertCmd.Parameters.AddWithValue("@AutoRenew", request.AutoRenew);
            insertCmd.Parameters.AddWithValue("@PaymentMethodId", (object?)request.PaymentMethodId ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync();

            // Add to subscription history
            const string historySql = @"
                INSERT INTO SubscriptionHistory
                    (Id, UserId, SubscriptionTierId, Action, ChangeDate, BillingCycle, Amount)
                SELECT @Id, @UserId, @SubscriptionTierId, 'Subscribed', GETUTCDATE(),
                       @BillingCycle,
                       CASE WHEN @BillingCycle = 'Monthly' THEN MonthlyPrice ELSE YearlyPrice END
                FROM SubscriptionTier
                WHERE Id = @SubscriptionTierId";

            var historyCmd = new SqlCommand(historySql, conn, txn);
            historyCmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            historyCmd.Parameters.AddWithValue("@UserId", userId);
            historyCmd.Parameters.AddWithValue("@SubscriptionTierId", request.SubscriptionTierId);
            historyCmd.Parameters.AddWithValue("@BillingCycle", request.BillingCycle);
            await historyCmd.ExecuteNonQueryAsync();

            return subscriptionId;
        });
    }

    public async Task<bool> CancelSubscriptionAsync(Guid userId, string? cancellationReason = null)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            // Get current subscription
            const string getSql = @"
                SELECT Id, SubscriptionTierId, NextBillingDate
                FROM UserSubscription
                WHERE UserId = @UserId AND Status = 'Active'";

            var getCmd = new SqlCommand(getSql, conn, txn);
            getCmd.Parameters.AddWithValue("@UserId", userId);

            Guid? subscriptionId = null;
            Guid? tierId = null;
            DateTime? nextBillingDate = null;

            using (var reader = await getCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    subscriptionId = reader.GetGuid(0);
                    tierId = reader.GetGuid(1);
                    nextBillingDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                }
            }

            if (!subscriptionId.HasValue)
            {
                return false;
            }

            // Cancel subscription (remains active until end of billing period)
            const string cancelSql = @"
                UPDATE UserSubscription
                SET Status = 'Cancelled',
                    AutoRenew = 0,
                    CancellationDate = GETUTCDATE(),
                    CancellationReason = @Reason,
                    EndDate = @EndDate
                WHERE Id = @Id";

            var cancelCmd = new SqlCommand(cancelSql, conn, txn);
            cancelCmd.Parameters.AddWithValue("@Id", subscriptionId.Value);
            cancelCmd.Parameters.AddWithValue("@Reason", (object?)cancellationReason ?? DBNull.Value);
            cancelCmd.Parameters.AddWithValue("@EndDate", nextBillingDate ?? DateTime.UtcNow);
            await cancelCmd.ExecuteNonQueryAsync();

            // Add to subscription history
            const string historySql = @"
                INSERT INTO SubscriptionHistory
                    (Id, UserId, SubscriptionTierId, Action, ChangeDate, Notes)
                VALUES
                    (@Id, @UserId, @SubscriptionTierId, 'Cancelled', GETUTCDATE(), @Notes)";

            var historyCmd = new SqlCommand(historySql, conn, txn);
            historyCmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            historyCmd.Parameters.AddWithValue("@UserId", userId);
            historyCmd.Parameters.AddWithValue("@SubscriptionTierId", tierId!.Value);
            historyCmd.Parameters.AddWithValue("@Notes", (object?)cancellationReason ?? DBNull.Value);
            await historyCmd.ExecuteNonQueryAsync();

            return true;
        });
    }

    public async Task<bool> RenewSubscriptionAsync(Guid subscriptionId)
    {
        return await ExecuteTransactionAsync(async (conn, txn) =>
        {
            // Get subscription details
            const string getSql = @"
                SELECT UserId, SubscriptionTierId, BillingCycle, NextBillingDate
                FROM UserSubscription
                WHERE Id = @Id AND AutoRenew = 1";

            var getCmd = new SqlCommand(getSql, conn, txn);
            getCmd.Parameters.AddWithValue("@Id", subscriptionId);

            Guid? userId = null;
            Guid? tierId = null;
            string? billingCycle = null;
            DateTime? currentBillingDate = null;

            using (var reader = await getCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    userId = reader.GetGuid(0);
                    tierId = reader.GetGuid(1);
                    billingCycle = reader.GetString(2);
                    currentBillingDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                }
            }

            if (!userId.HasValue || !currentBillingDate.HasValue)
            {
                return false;
            }

            // Calculate next billing date
            var nextBillingDate = billingCycle == "Monthly"
                ? currentBillingDate.Value.AddMonths(1)
                : currentBillingDate.Value.AddYears(1);

            // Update subscription
            const string updateSql = @"
                UPDATE UserSubscription
                SET NextBillingDate = @NextBillingDate,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id";

            var updateCmd = new SqlCommand(updateSql, conn, txn);
            updateCmd.Parameters.AddWithValue("@Id", subscriptionId);
            updateCmd.Parameters.AddWithValue("@NextBillingDate", nextBillingDate);
            await updateCmd.ExecuteNonQueryAsync();

            // Add to subscription history
            const string historySql = @"
                INSERT INTO SubscriptionHistory
                    (Id, UserId, SubscriptionTierId, Action, ChangeDate, BillingCycle, Amount)
                SELECT @Id, @UserId, @SubscriptionTierId, 'Renewed', GETUTCDATE(),
                       @BillingCycle,
                       CASE WHEN @BillingCycle = 'Monthly' THEN MonthlyPrice ELSE YearlyPrice END
                FROM SubscriptionTier
                WHERE Id = @SubscriptionTierId";

            var historyCmd = new SqlCommand(historySql, conn, txn);
            historyCmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            historyCmd.Parameters.AddWithValue("@UserId", userId.Value);
            historyCmd.Parameters.AddWithValue("@SubscriptionTierId", tierId!.Value);
            historyCmd.Parameters.AddWithValue("@BillingCycle", billingCycle!);
            await historyCmd.ExecuteNonQueryAsync();

            return true;
        });
    }

    public async Task<bool> UpdatePaymentMethodAsync(Guid subscriptionId, string paymentMethodId)
    {
        const string sql = @"
            UPDATE UserSubscription
            SET PaymentMethodId = @PaymentMethodId,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND Status = 'Active'";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", subscriptionId),
            new SqlParameter("@PaymentMethodId", paymentMethodId));

        return rowsAffected > 0;
    }

    #endregion

    #region Subscription History

    public async Task<List<SubscriptionHistoryDto>> GetSubscriptionHistoryAsync(Guid userId)
    {
        const string sql = @"
            SELECT sh.Id, sh.UserId, sh.SubscriptionTierId, sh.Action, sh.ChangeDate,
                   sh.BillingCycle, sh.Amount, sh.Notes,
                   st.TierName, st.DisplayName
            FROM SubscriptionHistory sh
            INNER JOIN SubscriptionTier st ON sh.SubscriptionTierId = st.Id
            WHERE sh.UserId = @UserId
            ORDER BY sh.ChangeDate DESC";

        return await ExecuteReaderAsync(sql, reader => new SubscriptionHistoryDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            SubscriptionTierId = reader.GetGuid(reader.GetOrdinal("SubscriptionTierId")),
            Action = reader.GetString(reader.GetOrdinal("Action")),
            ChangeDate = reader.GetDateTime(reader.GetOrdinal("ChangeDate")),
            BillingCycle = reader.IsDBNull(reader.GetOrdinal("BillingCycle"))
                ? null
                : reader.GetString(reader.GetOrdinal("BillingCycle")),
            Amount = reader.IsDBNull(reader.GetOrdinal("Amount"))
                ? null
                : reader.GetDecimal(reader.GetOrdinal("Amount")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("Notes")),
            TierName = reader.GetString(reader.GetOrdinal("TierName")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName"))
        }, new SqlParameter("@UserId", userId));
    }

    #endregion

    #region Feature Access

    public async Task<bool> HasFeatureAccessAsync(Guid userId, string featureName)
    {
        const string sql = @"
            SELECT
                CASE @FeatureName
                    WHEN 'OfflineSync' THEN st.AllowsOfflineSync
                    WHEN 'AdvancedReports' THEN st.AllowsAdvancedReports
                    WHEN 'PriceComparison' THEN st.AllowsPriceComparison
                    WHEN 'RecipeImport' THEN st.AllowsRecipeImport
                    WHEN 'MenuPlanning' THEN st.AllowsMenuPlanning
                    ELSE 0
                END AS HasAccess
            FROM UserSubscription us
            INNER JOIN SubscriptionTier st ON us.SubscriptionTierId = st.Id
            WHERE us.UserId = @UserId
              AND us.Status = 'Active'
              AND (us.EndDate IS NULL OR us.EndDate > GETUTCDATE())";

        var result = await ExecuteScalarAsync<int?>(sql,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@FeatureName", featureName));

        // If no active subscription, default to Free tier (no premium features)
        if (result == null)
        {
            return false;
        }

        return result == 1;
    }

    public async Task<Dictionary<string, bool>> GetFeatureAccessMapAsync(Guid userId)
    {
        const string sql = @"
            SELECT st.AllowsOfflineSync, st.AllowsAdvancedReports, st.AllowsPriceComparison,
                   st.AllowsRecipeImport, st.AllowsMenuPlanning
            FROM UserSubscription us
            INNER JOIN SubscriptionTier st ON us.SubscriptionTierId = st.Id
            WHERE us.UserId = @UserId
              AND us.Status = 'Active'
              AND (us.EndDate IS NULL OR us.EndDate > GETUTCDATE())";

        var features = new Dictionary<string, bool>
        {
            ["OfflineSync"] = false,
            ["AdvancedReports"] = false,
            ["PriceComparison"] = false,
            ["RecipeImport"] = false,
            ["MenuPlanning"] = false
        };

        var results = await ExecuteReaderAsync(sql, reader => new
        {
            AllowsOfflineSync = reader.GetBoolean(0),
            AllowsAdvancedReports = reader.GetBoolean(1),
            AllowsPriceComparison = reader.GetBoolean(2),
            AllowsRecipeImport = reader.GetBoolean(3),
            AllowsMenuPlanning = reader.GetBoolean(4)
        }, new SqlParameter("@UserId", userId));

        var result = results.FirstOrDefault();
        if (result != null)
        {
            features["OfflineSync"] = result.AllowsOfflineSync;
            features["AdvancedReports"] = result.AllowsAdvancedReports;
            features["PriceComparison"] = result.AllowsPriceComparison;
            features["RecipeImport"] = result.AllowsRecipeImport;
            features["MenuPlanning"] = result.AllowsMenuPlanning;
        }

        return features;
    }

    #endregion
}
