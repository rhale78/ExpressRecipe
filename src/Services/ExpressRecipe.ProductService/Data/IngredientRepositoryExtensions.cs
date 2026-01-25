using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.ProductService.Data
{
    public static class IngredientRepositoryExtensions
    {
        /// <summary>
        /// Bulk upsert ingredients - insert new ones, update existing ones
        /// </summary>
        public static async Task<int> BulkUpsertIngredientsAsync(
            this IIngredientRepository repository,
            IEnumerable<CreateIngredientRequest> ingredients,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            List<CreateIngredientRequest> ingredientsList = ingredients.ToList();
            if (ingredientsList.Count == 0)
            {
                return 0;
            }

            // Create data table structure
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("AlternativeNames", typeof(string));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("Category", typeof(string));
            dataTable.Columns.Add("IsCommonAllergen", typeof(bool));

            // Map function
            DataRow MapToDataRow(CreateIngredientRequest item, DataRow row)
            {
                row["Name"] = item.Name;
                row["AlternativeNames"] = (object?)item.AlternativeNames ?? DBNull.Value;
                row["Description"] = (object?)item.Description ?? DBNull.Value;
                row["Category"] = item.Category ?? "General";
                row["IsCommonAllergen"] = item.IsCommonAllergen;
                return row;
            }

            return await BulkOperationsHelper.BulkUpsertAsync(
                connectionString,
                ingredientsList,
                "Ingredient",
                "#TempIngredients",
                new[] { "Name" }, // Key column
                MapToDataRow,
                dataTable,
                cancellationToken);
        }

        /// <summary>
        /// Bulk insert product-ingredient relationships
        /// </summary>
        public static async Task<int> BulkInsertProductIngredientsAsync(
            this IIngredientRepository repository,
            IEnumerable<ProductIngredientBulkInsert> items,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            List<ProductIngredientBulkInsert> itemsList = items.ToList();
            if (itemsList.Count == 0)
            {
                return 0;
            }

            using SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                // Create DataTable
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("Id", typeof(Guid));
                dataTable.Columns.Add("ProductId", typeof(Guid));
                dataTable.Columns.Add("IngredientId", typeof(Guid));
                dataTable.Columns.Add("OrderIndex", typeof(int));
                dataTable.Columns.Add("Quantity", typeof(string));
                dataTable.Columns.Add("Notes", typeof(string));
                dataTable.Columns.Add("IngredientListString", typeof(string));
                dataTable.Columns.Add("CreatedAt", typeof(DateTime));
                dataTable.Columns.Add("CreatedBy", typeof(Guid));
                dataTable.Columns.Add("IsDeleted", typeof(bool));

                foreach (ProductIngredientBulkInsert item in itemsList)
                {
                    DataRow row = dataTable.NewRow();
                    row["Id"] = Guid.NewGuid();
                    row["ProductId"] = item.ProductId;
                    row["IngredientId"] = item.IngredientId;
                    row["OrderIndex"] = item.OrderIndex;
                    row["Quantity"] = (object?)item.Quantity ?? DBNull.Value;
                    row["Notes"] = (object?)item.Notes ?? DBNull.Value;
                    row["IngredientListString"] = (object?)item.IngredientListString ?? DBNull.Value;
                    row["CreatedAt"] = DateTime.UtcNow;
                    row["CreatedBy"] = (object?)item.CreatedBy ?? DBNull.Value;
                    row["IsDeleted"] = false;
                    dataTable.Rows.Add(row);
                }

                // Bulk insert
                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
                bulkCopy.DestinationTableName = "ProductIngredient";
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300;

                bulkCopy.ColumnMappings.Add("Id", "Id");
                bulkCopy.ColumnMappings.Add("ProductId", "ProductId");
                bulkCopy.ColumnMappings.Add("IngredientId", "IngredientId");
                bulkCopy.ColumnMappings.Add("OrderIndex", "OrderIndex");
                bulkCopy.ColumnMappings.Add("Quantity", "Quantity");
                bulkCopy.ColumnMappings.Add("Notes", "Notes");
                bulkCopy.ColumnMappings.Add("IngredientListString", "IngredientListString");
                bulkCopy.ColumnMappings.Add("CreatedAt", "CreatedAt");
                bulkCopy.ColumnMappings.Add("CreatedBy", "CreatedBy");
                bulkCopy.ColumnMappings.Add("IsDeleted", "IsDeleted");

                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return dataTable.Rows.Count;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// Bulk query ingredients by names
        /// </summary>
        public static async Task<Dictionary<string, Guid>> BulkGetIngredientIdsByNamesAsync(
            this IIngredientRepository repository,
            IEnumerable<string> ingredientNames,
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            List<string> namesList = ingredientNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (namesList.Count == 0)
            {
                return [];
            }

            using SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Create temp table with names
            using SqlCommand createTempCmd = new SqlCommand(
                "CREATE TABLE #TempNames (Name NVARCHAR(200))",
                connection);
            await createTempCmd.ExecuteNonQueryAsync(cancellationToken);

            // Bulk insert names
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Name", typeof(string));
            foreach (var name in namesList)
            {
                dataTable.Rows.Add(name);
            }

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = "#TempNames";
            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);

            // Query matching ingredients
            const string querySql = @"
            SELECT i.Id, i.Name
            FROM Ingredient i
            INNER JOIN #TempNames t ON LOWER(i.Name) = LOWER(t.Name)
            WHERE i.IsDeleted = 0";

            Dictionary<string, Guid> result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        
            using SqlCommand queryCmd = new SqlCommand(querySql, connection);
            using SqlDataReader reader = await queryCmd.ExecuteReaderAsync(cancellationToken);
        
            while (await reader.ReadAsync(cancellationToken))
            {
                Guid id = reader.GetGuid(0);
                var name = reader.GetString(1);
                result[name] = id;
            }

            return result;
        }
    }

    /// <summary>
    /// Model for bulk product-ingredient insert
    /// </summary>
    public class ProductIngredientBulkInsert
    {
        public Guid ProductId { get; set; }
        public Guid IngredientId { get; set; }
        public int OrderIndex { get; set; }
        public string? Quantity { get; set; }
        public string? Notes { get; set; }
        public string? IngredientListString { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}
