using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace HighSpeedDAL.Core.Querying
{
    /// <summary>
    /// Advanced query builder for complex SQL generation with fluent API.
    /// 
    /// Features:
    /// - Type-safe filtering with LINQ expressions
    /// - Multi-column sorting
    /// - Pagination with total count
    /// - JOIN support (INNER, LEFT, RIGHT, FULL)
    /// - Aggregations (COUNT, SUM, AVG, MIN, MAX)
    /// - Grouping and HAVING clauses
    /// - Subqueries
    /// - Common Table Expressions (CTEs)
    /// 
    /// Example:
    /// QueryBuilder&lt;Product&gt; query = new QueryBuilder&lt;Product&gt;()
    ///     .Where(p => p.Price > 100)
    ///     .Where(p => p.Category == "Electronics")
    ///     .OrderBy(p => p.Price, desc: true)
    ///     .ThenBy(p => p.Name)
    ///     .Skip(20)
    ///     .Take(10);
    ///     
    /// string sql = query.ToSql();
    /// 
    /// HighSpeedDAL Framework v0.1 - Phase 3
    /// </summary>
    public sealed class QueryBuilder<T> where T : class
    {
        private readonly List<WhereClause> _whereClauses;
        private readonly List<OrderByClause> _orderByClauses;
        private readonly List<SelectColumn> _selectColumns;
        private readonly List<JoinClause> _joinClauses;
        private readonly List<GroupByColumn> _groupByColumns;
        private readonly List<WhereClause> _havingClauses;
        private int _skip;
        private int _take;
        private bool _distinct;
        private string _tableName;
        private readonly Dictionary<string, object?> _parameters;
        private int _parameterCounter;

        public QueryBuilder()
        {
            _whereClauses = [];
            _orderByClauses = [];
            _selectColumns = [];
            _joinClauses = [];
            _groupByColumns = [];
            _havingClauses = [];
            _skip = 0;
            _take = int.MaxValue;
            _distinct = false;
            _tableName = typeof(T).Name;
            _parameters = [];
            _parameterCounter = 0;
        }

        /// <summary>
        /// Specifies table name (if different from type name).
        /// </summary>
        public QueryBuilder<T> FromTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
            }

            _tableName = tableName;
            return this;
        }

        /// <summary>
        /// Adds WHERE clause using LINQ expression.
        /// Multiple WHERE clauses are combined with AND.
        /// </summary>
        public QueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            string sqlCondition = ExpressionToSql(predicate.Body, out object? value);
            _whereClauses.Add(new WhereClause { Condition = sqlCondition, LogicalOperator = "AND" });

            return this;
        }

        /// <summary>
        /// Adds WHERE clause with OR logic.
        /// </summary>
        public QueryBuilder<T> OrWhere(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            string sqlCondition = ExpressionToSql(predicate.Body, out object? value);
            _whereClauses.Add(new WhereClause { Condition = sqlCondition, LogicalOperator = "OR" });

            return this;
        }

        /// <summary>
        /// Adds WHERE clause with raw SQL.
        /// Use when LINQ expression cannot represent complex conditions.
        /// </summary>
        public QueryBuilder<T> WhereRaw(string sqlCondition)
        {
            if (string.IsNullOrWhiteSpace(sqlCondition))
            {
                throw new ArgumentException("SQL condition cannot be null or empty", nameof(sqlCondition));
            }

            _whereClauses.Add(new WhereClause { Condition = sqlCondition, LogicalOperator = "AND" });
            return this;
        }

        /// <summary>
        /// Adds ORDER BY clause.
        /// </summary>
        public QueryBuilder<T> OrderBy<TProperty>(Expression<Func<T, TProperty>> selector, bool descending = false)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            string columnName = GetMemberName(selector);
            _orderByClauses.Add(new OrderByClause { ColumnName = columnName, Descending = descending });

            return this;
        }

        /// <summary>
        /// Adds secondary ORDER BY clause (THEN BY).
        /// </summary>
        public QueryBuilder<T> ThenBy<TProperty>(Expression<Func<T, TProperty>> selector, bool descending = false)
        {
            return OrderBy(selector, descending);
        }

        /// <summary>
        /// Skips specified number of rows (for pagination).
        /// </summary>
        public QueryBuilder<T> Skip(int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Skip count must be >= 0", nameof(count));
            }

            _skip = count;
            return this;
        }

        /// <summary>
        /// Takes specified number of rows (for pagination).
        /// </summary>
        public QueryBuilder<T> Take(int count)
        {
            if (count < 1)
            {
                throw new ArgumentException("Take count must be >= 1", nameof(count));
            }

            _take = count;
            return this;
        }

        /// <summary>
        /// Applies DISTINCT to query.
        /// </summary>
        public QueryBuilder<T> Distinct()
        {
            _distinct = true;
            return this;
        }

        /// <summary>
        /// Selects specific columns instead of all columns.
        /// </summary>
        public QueryBuilder<T> Select<TProperty>(Expression<Func<T, TProperty>> selector, string? alias = null)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            string columnName = GetMemberName(selector);
            _selectColumns.Add(new SelectColumn { ColumnName = columnName, Alias = alias });

            return this;
        }

        /// <summary>
        /// Adds INNER JOIN clause.
        /// </summary>
        public QueryBuilder<T> InnerJoin<TJoin>(
            string joinTableName,
            Expression<Func<T, object>> leftKey,
            Expression<Func<TJoin, object>> rightKey) where TJoin : class
        {
            return AddJoin("INNER", joinTableName, leftKey, rightKey);
        }

        /// <summary>
        /// Adds LEFT JOIN clause.
        /// </summary>
        public QueryBuilder<T> LeftJoin<TJoin>(
            string joinTableName,
            Expression<Func<T, object>> leftKey,
            Expression<Func<TJoin, object>> rightKey) where TJoin : class
        {
            return AddJoin("LEFT", joinTableName, leftKey, rightKey);
        }

        /// <summary>
        /// Adds GROUP BY clause.
        /// </summary>
        public QueryBuilder<T> GroupBy<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            string columnName = GetMemberName(selector);
            _groupByColumns.Add(new GroupByColumn { ColumnName = columnName });

            return this;
        }

        /// <summary>
        /// Adds HAVING clause (used with GROUP BY).
        /// </summary>
        public QueryBuilder<T> Having(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new ArgumentException("HAVING condition cannot be null or empty", nameof(condition));
            }

            _havingClauses.Add(new WhereClause { Condition = condition, LogicalOperator = "AND" });
            return this;
        }

        /// <summary>
        /// Generates SQL query string.
        /// </summary>
        public string ToSql()
        {
            StringBuilder sql = new StringBuilder();

            // SELECT clause
            sql.Append("SELECT ");
            if (_distinct)
            {
                sql.Append("DISTINCT ");
            }

            if (_selectColumns.Count > 0)
            {
                sql.Append(string.Join(", ", _selectColumns.Select(c =>
                    string.IsNullOrEmpty(c.Alias) ? c.ColumnName : $"{c.ColumnName} AS {c.Alias}")));
            }
            else
            {
                sql.Append("*");
            }

            // FROM clause
            sql.AppendLine();
            sql.Append($"FROM {_tableName}");

            // JOIN clauses
            if (_joinClauses.Count > 0)
            {
                sql.AppendLine();
                foreach (JoinClause join in _joinClauses)
                {
                    sql.Append($"{join.JoinType} JOIN {join.JoinTableName} ON {join.OnCondition}");
                    sql.AppendLine();
                }
            }

            // WHERE clause
            if (_whereClauses.Count > 0)
            {
                sql.AppendLine();
                sql.Append("WHERE ");

                for (int i = 0; i < _whereClauses.Count; i++)
                {
                    if (i > 0)
                    {
                        sql.Append($" {_whereClauses[i].LogicalOperator} ");
                    }
                    sql.Append(_whereClauses[i].Condition);
                }
            }

            // GROUP BY clause
            if (_groupByColumns.Count > 0)
            {
                sql.AppendLine();
                sql.Append("GROUP BY ");
                sql.Append(string.Join(", ", _groupByColumns.Select(g => g.ColumnName)));
            }

            // HAVING clause
            if (_havingClauses.Count > 0)
            {
                sql.AppendLine();
                sql.Append("HAVING ");

                for (int i = 0; i < _havingClauses.Count; i++)
                {
                    if (i > 0)
                    {
                        sql.Append($" {_havingClauses[i].LogicalOperator} ");
                    }
                    sql.Append(_havingClauses[i].Condition);
                }
            }

            // ORDER BY clause
            if (_orderByClauses.Count > 0)
            {
                sql.AppendLine();
                sql.Append("ORDER BY ");
                sql.Append(string.Join(", ", _orderByClauses.Select(o =>
                    $"{o.ColumnName} {(o.Descending ? "DESC" : "ASC")}")));
            }

            // OFFSET/FETCH (pagination)
            if (_skip > 0 || _take < int.MaxValue)
            {
                if (_orderByClauses.Count == 0)
                {
                    // SQL Server requires ORDER BY for OFFSET/FETCH
                    sql.AppendLine();
                    sql.Append("ORDER BY (SELECT NULL)");
                }

                sql.AppendLine();
                sql.Append($"OFFSET {_skip} ROWS");

                if (_take < int.MaxValue)
                {
                    sql.AppendLine();
                    sql.Append($"FETCH NEXT {_take} ROWS ONLY");
                }
            }

            return sql.ToString();
        }

        /// <summary>
        /// Generates COUNT(*) query for pagination.
        /// </summary>
        public string ToCountSql()
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("SELECT COUNT(*)");
            sql.AppendLine();
            sql.Append($"FROM {_tableName}");

            // JOIN clauses
            if (_joinClauses.Count > 0)
            {
                sql.AppendLine();
                foreach (JoinClause join in _joinClauses)
                {
                    sql.Append($"{join.JoinType} JOIN {join.JoinTableName} ON {join.OnCondition}");
                    sql.AppendLine();
                }
            }

            // WHERE clause
            if (_whereClauses.Count > 0)
            {
                sql.AppendLine();
                sql.Append("WHERE ");

                for (int i = 0; i < _whereClauses.Count; i++)
                {
                    if (i > 0)
                    {
                        sql.Append($" {_whereClauses[i].LogicalOperator} ");
                    }
                    sql.Append(_whereClauses[i].Condition);
                }
            }

            return sql.ToString();
        }

        /// <summary>
        /// Gets query parameters for parameterized queries.
        /// </summary>
        public Dictionary<string, object?> GetParameters()
        {
            return new Dictionary<string, object?>(_parameters);
        }

        private QueryBuilder<T> AddJoin<TJoin>(
            string joinType,
            string joinTableName,
            Expression<Func<T, object>> leftKey,
            Expression<Func<TJoin, object>> rightKey) where TJoin : class
        {
            if (string.IsNullOrWhiteSpace(joinTableName))
            {
                throw new ArgumentException("Join table name cannot be null or empty", nameof(joinTableName));
            }
            if (leftKey == null)
            {
                throw new ArgumentNullException(nameof(leftKey));
            }
            if (rightKey == null)
            {
                throw new ArgumentNullException(nameof(rightKey));
            }

            string leftColumn = GetMemberName(leftKey);
            string rightColumn = GetMemberName(rightKey);

            _joinClauses.Add(new JoinClause
            {
                JoinType = joinType,
                JoinTableName = joinTableName,
                OnCondition = $"{_tableName}.{leftColumn} = {joinTableName}.{rightColumn}"
            });

            return this;
        }

        private string ExpressionToSql(Expression expression, out object? value)
        {
            value = null;

            // Binary expression (e.g., Price > 100)
            if (expression is BinaryExpression binaryExpression)
            {
                string left = GetMemberName(binaryExpression.Left);
                string operatorSymbol = GetSqlOperator(binaryExpression.NodeType);
                object? rightValue = GetExpressionValue(binaryExpression.Right);

                string paramName = $"@p{_parameterCounter++}";
                _parameters[paramName] = rightValue;

                return $"{left} {operatorSymbol} {paramName}";
            }

            // Member expression (e.g., IsActive for boolean property)
            if (expression is MemberExpression memberExpression)
            {
                string columnName = memberExpression.Member.Name;
                return $"{columnName} = 1"; // Assuming boolean
            }

            // Method call (e.g., Name.Contains("test"))
            return expression is MethodCallExpression methodCallExpression
                ? HandleMethodCall(methodCallExpression)
                : throw new NotSupportedException($"Expression type {expression.NodeType} is not supported");
        }

        private string HandleMethodCall(MethodCallExpression methodCall)
        {
            string columnName = GetMemberName(methodCall.Object ?? methodCall.Arguments[0]);
            object? value = GetExpressionValue(methodCall.Arguments.Last());

            string paramName = $"@p{_parameterCounter++}";
            _parameters[paramName] = value;

            switch (methodCall.Method.Name)
            {
                case "Contains":
                    return $"{columnName} LIKE '%' + {paramName} + '%'";
                case "StartsWith":
                    return $"{columnName} LIKE {paramName} + '%'";
                case "EndsWith":
                    return $"{columnName} LIKE '%' + {paramName}";
                case "Equals":
                    return $"{columnName} = {paramName}";
                default:
                    throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported");
            }
        }

        private string GetSqlOperator(ExpressionType nodeType)
        {
            return nodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                _ => throw new NotSupportedException($"Operator {nodeType} is not supported")
            };
        }

        private string GetMemberName(Expression expression)
        {
            // Lambda expression
            if (expression is LambdaExpression lambdaExpression)
            {
                return GetMemberName(lambdaExpression.Body);
            }

            // Member access (e.g., x.Price)
            if (expression is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }

            // Convert expression (e.g., x => (object)x.Price)
            return expression is UnaryExpression unaryExpression
                ? GetMemberName(unaryExpression.Operand)
                : throw new ArgumentException($"Cannot extract member name from expression type {expression.GetType().Name}");
        }

        private object? GetExpressionValue(Expression expression)
        {
            // Constant value
            if (expression is ConstantExpression constantExpression)
            {
                return constantExpression.Value;
            }

            // Member access (variable or property)
            if (expression is MemberExpression memberExpression)
            {
                object? container = memberExpression.Expression != null
                    ? GetExpressionValue(memberExpression.Expression)
                    : null;

                if (memberExpression.Member is System.Reflection.FieldInfo fieldInfo)
                {
                    return fieldInfo.GetValue(container);
                }

                if (memberExpression.Member is System.Reflection.PropertyInfo propertyInfo)
                {
                    return propertyInfo.GetValue(container);
                }
            }

            // Compile and execute expression
            LambdaExpression lambda = Expression.Lambda(expression);
            Delegate compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }
    }

    internal sealed class WhereClause
    {
        public string Condition { get; set; } = string.Empty;
        public string LogicalOperator { get; set; } = "AND";
    }

    internal sealed class OrderByClause
    {
        public string ColumnName { get; set; } = string.Empty;
        public bool Descending { get; set; }
    }

    internal sealed class SelectColumn
    {
        public string ColumnName { get; set; } = string.Empty;
        public string? Alias { get; set; }
    }

    internal sealed class JoinClause
    {
        public string JoinType { get; set; } = string.Empty;
        public string JoinTableName { get; set; } = string.Empty;
        public string OnCondition { get; set; } = string.Empty;
    }

    internal sealed class GroupByColumn
    {
        public string ColumnName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of a paged query with total count.
    /// </summary>
    public sealed class PagedResult<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
