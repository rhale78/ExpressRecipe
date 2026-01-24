using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace HighSpeedDAL.Core.InMemoryTable;

/// <summary>
/// Parses simple SQL-like WHERE clauses and evaluates them against in-memory rows.
/// 
/// Supported operators:
/// - Comparison: =, !=, &lt;&gt;, &lt;, &lt;=, &gt;, &gt;=
/// - Logical: AND, OR, NOT
/// - Pattern: LIKE, IN, BETWEEN
/// - Null: IS NULL, IS NOT NULL
/// 
/// Examples:
/// - "Name = 'John'"
/// - "Age > 21 AND Status = 'Active'"
/// - "Email LIKE '%@example.com'"
/// - "Id IN (1, 2, 3)"
/// - "CreatedAt BETWEEN '2024-01-01' AND '2024-12-31'"
/// </summary>
public sealed class WhereClauseParser
{
    private readonly InMemoryTableSchema _schema;
    private readonly Dictionary<string, object?> _parameters;

    // Pre-compiled regex patterns
    private static readonly Regex NotPrefixRegex = new(@"^NOT ", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BetweenCheckRegex = new(@"^\s*(\w+)\s+BETWEEN\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IsNullRegex = new(@"^(\w+)\s+IS\s+(NOT\s+)?NULL$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BetweenRegex = new(@"^(\w+)\s+BETWEEN\s+(.+)\s+AND\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InRegex = new(@"^(\w+)\s+IN\s*\((.+)\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LikeRegex = new(@"^(\w+)\s+LIKE\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ComparisonRegex = new(@"^(\w+)\s*(=|!=|<>|<=|>=|<|>)\s*(.+)$", RegexOptions.Compiled);

    public WhereClauseParser(InMemoryTableSchema schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets a parameter value for parameterized queries
    /// </summary>
    public void SetParameter(string name, object? value)
    {
        _parameters[name.TrimStart('@')] = value;
    }

    /// <summary>
    /// Clears all parameters
    /// </summary>
    public void ClearParameters()
    {
        _parameters.Clear();
    }

    /// <summary>
    /// Parses a WHERE clause and returns a predicate function
    /// </summary>
    public Func<InMemoryRow, bool> Parse(string whereClause)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return _ => true; // No filter, match all
        }

        WhereExpression expression = ParseExpression(whereClause.Trim());
        return row => expression.Evaluate(row);
    }

    /// <summary>
    /// Filters rows using a WHERE clause
    /// </summary>
    public IEnumerable<InMemoryRow> Filter(IEnumerable<InMemoryRow> rows, string whereClause)
    {
        Func<InMemoryRow, bool> predicate = Parse(whereClause);
        return rows.Where(predicate);
    }

    /// <summary>
    /// Creates a predicate from a lambda expression
    /// </summary>
    public static Func<InMemoryRow, bool> FromLambda<TEntity>(
        Expression<Func<TEntity, bool>> expression,
        InMemoryTableSchema schema) where TEntity : class, new()
    {
        // Convert the expression to work with InMemoryRow
        LambdaToRowPredicateVisitor visitor = new LambdaToRowPredicateVisitor(schema);
        return visitor.Convert(expression);
    }

    private WhereExpression ParseExpression(string clause)
    {
        // Handle parentheses first
        clause = clause.Trim();

        // Check for NOT prefix
        if (NotPrefixRegex.IsMatch(clause))
        {
            return new NotExpression(ParseExpression(clause.Substring(4).Trim()));
        }

        // Check for outer parentheses
        if (clause.StartsWith("(") && clause.EndsWith(")"))
        {
            int depth = 0;
            bool isOuterParen = true;
            for (int i = 0; i < clause.Length - 1; i++)
            {
                if (clause[i] == '(') depth++;
                else if (clause[i] == ')') depth--;
                if (depth == 0 && i > 0)
                {
                    isOuterParen = false;
                    break;
                }
            }
            if (isOuterParen)
            {
                return ParseExpression(clause.Substring(1, clause.Length - 2));
            }
        }

            // Find top-level OR (lowest precedence)
            int orIndex = FindTopLevelOperator(clause, " OR ");
            if (orIndex >= 0)
            {
                return new OrExpression(
                    ParseExpression(clause.Substring(0, orIndex)),
                    ParseExpression(clause.Substring(orIndex + 4)));
            }

            // Check for BETWEEN...AND before treating AND as a logical operator
            // This prevents "Age BETWEEN 25 AND 35" from being split at " AND "
            Match betweenCheck = BetweenCheckRegex.Match(clause);
            if (!betweenCheck.Success)
            {
                // Find top-level AND only if it's not part of a BETWEEN clause
                int andIndex = FindTopLevelOperator(clause, " AND ");
                if (andIndex >= 0)
                {
                    return new AndExpression(
                        ParseExpression(clause.Substring(0, andIndex)),
                        ParseExpression(clause.Substring(andIndex + 5)));
                }
            }

            // Parse single condition
            return ParseCondition(clause);
        }

    private int FindTopLevelOperator(string clause, string op)
    {
        int depth = 0;
        int searchStart = 0;
        
        while (searchStart < clause.Length)
        {
            int index = clause.IndexOf(op, searchStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return -1;

            // Count parentheses before this position
            depth = 0;
            for (int i = 0; i < index; i++)
            {
                if (clause[i] == '(') depth++;
                else if (clause[i] == ')') depth--;
            }

            if (depth == 0)
            {
                return index;
            }

            searchStart = index + 1;
        }

        return -1;
    }

    private WhereExpression ParseCondition(string condition)
    {
        condition = condition.Trim();

        // IS NULL / IS NOT NULL
        Match isNullMatch = IsNullRegex.Match(condition);
        if (isNullMatch.Success)
        {
            string column = isNullMatch.Groups[1].Value;
            bool isNotNull = isNullMatch.Groups[2].Success;
            return new NullCheckExpression(column, isNotNull);
        }

        // BETWEEN
        Match betweenMatch = BetweenRegex.Match(condition);
        if (betweenMatch.Success)
        {
            string column = betweenMatch.Groups[1].Value;
            object? lower = ParseValue(betweenMatch.Groups[2].Value.Trim());
            object? upper = ParseValue(betweenMatch.Groups[3].Value.Trim());
            return new BetweenExpression(column, lower, upper);
        }

        // IN
        Match inMatch = InRegex.Match(condition);
        if (inMatch.Success)
        {
            string column = inMatch.Groups[1].Value;
            string[] valueStrings = inMatch.Groups[2].Value.Split(',');
            List<object?> values = valueStrings.Select(v => ParseValue(v.Trim())).ToList();
            return new InExpression(column, values);
        }

        // LIKE
        Match likeMatch = LikeRegex.Match(condition);
        if (likeMatch.Success)
        {
            string column = likeMatch.Groups[1].Value;
            string pattern = ParseStringValue(likeMatch.Groups[2].Value.Trim());
            return new LikeExpression(column, pattern);
        }

        // Comparison operators
        Match compMatch = ComparisonRegex.Match(condition);
        if (compMatch.Success)
        {
            string column = compMatch.Groups[1].Value;
            string op = compMatch.Groups[2].Value;
            object? value = ParseValue(compMatch.Groups[3].Value.Trim());
            return new ComparisonExpression(column, op, value);
        }

        throw new ArgumentException($"Unable to parse condition: {condition}");
    }

    private object? ParseValue(string valueStr)
    {
        valueStr = valueStr.Trim();

        // Parameter reference
        if (valueStr.StartsWith("@"))
        {
            string paramName = valueStr.Substring(1);
            if (_parameters.TryGetValue(paramName, out object? paramValue))
            {
                return paramValue;
            }
            throw new ArgumentException($"Parameter @{paramName} not found");
        }

        // NULL
        if (valueStr.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Boolean
        if (valueStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (valueStr.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // String (quoted)
        if ((valueStr.StartsWith("'") && valueStr.EndsWith("'")) ||
            (valueStr.StartsWith("\"") && valueStr.EndsWith("\"")))
        {
            return ParseStringValue(valueStr);
        }

        // Integer
        if (int.TryParse(valueStr, out int intValue))
        {
            return intValue;
        }

        // Long
        if (long.TryParse(valueStr, out long longValue))
        {
            return longValue;
        }

        // Decimal
        if (decimal.TryParse(valueStr, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, out decimal decValue))
        {
            return decValue;
        }

        // DateTime
        if (DateTime.TryParse(valueStr.Trim('\'', '"'), out DateTime dateValue))
        {
            return dateValue;
        }

        // Guid
        if (Guid.TryParse(valueStr.Trim('\'', '"'), out Guid guidValue))
        {
            return guidValue;
        }

        return valueStr;
    }

    private string ParseStringValue(string valueStr)
    {
        if ((valueStr.StartsWith("'") && valueStr.EndsWith("'")) ||
            (valueStr.StartsWith("\"") && valueStr.EndsWith("\"")))
        {
            return valueStr.Substring(1, valueStr.Length - 2)
                .Replace("''", "'")
                .Replace("\"\"", "\"");
        }
        return valueStr;
    }
}

#region Expression Types

internal abstract class WhereExpression
{
    public abstract bool Evaluate(InMemoryRow row);
}

internal sealed class ComparisonExpression : WhereExpression
{
    private readonly string _column;
    private readonly string _operator;
    private readonly object? _value;

    public ComparisonExpression(string column, string op, object? value)
    {
        _column = column;
        _operator = op;
        _value = value;
    }

    public override bool Evaluate(InMemoryRow row)
    {
        object? rowValue = row[_column];
        
        if (rowValue == null && _value == null)
        {
            return _operator == "=" || _operator == "==" || _operator == "<=>" ;
        }
        
        if (rowValue == null || _value == null)
        {
            return _operator == "!=" || _operator == "<>";
        }

        int comparison;
        try
        {
            // Try to compare as IComparable
            if (rowValue is IComparable comparable)
            {
                object convertedValue = Convert.ChangeType(_value, rowValue.GetType(), 
                    System.Globalization.CultureInfo.InvariantCulture);
                comparison = comparable.CompareTo(convertedValue);
            }
            else
            {
                comparison = rowValue.Equals(_value) ? 0 : 1;
            }
        }
        catch
        {
            // If conversion fails, do string comparison
            comparison = string.Compare(rowValue.ToString(), _value.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return _operator switch
        {
            "=" => comparison == 0,
            "==" => comparison == 0,
            "!=" => comparison != 0,
            "<>" => comparison != 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            _ => false
        };
    }
}

internal sealed class NullCheckExpression : WhereExpression
{
    private readonly string _column;
    private readonly bool _isNotNull;

    public NullCheckExpression(string column, bool isNotNull)
    {
        _column = column;
        _isNotNull = isNotNull;
    }

    public override bool Evaluate(InMemoryRow row)
    {
        object? value = row[_column];
        bool isNull = value == null || value == DBNull.Value;
        return _isNotNull ? !isNull : isNull;
    }
}

internal sealed class BetweenExpression : WhereExpression
{
    private readonly string _column;
    private readonly object? _lower;
    private readonly object? _upper;

    public BetweenExpression(string column, object? lower, object? upper)
    {
        _column = column;
        _lower = lower;
        _upper = upper;
    }

    public override bool Evaluate(InMemoryRow row)
    {
        object? value = row[_column];
        if (value == null)
        {
            return false;
        }

        try
        {
            if (value is IComparable comparable)
            {
                Type valueType = value.GetType();
                object lowerConverted = Convert.ChangeType(_lower!, valueType, 
                    System.Globalization.CultureInfo.InvariantCulture);
                object upperConverted = Convert.ChangeType(_upper!, valueType, 
                    System.Globalization.CultureInfo.InvariantCulture);
                
                return comparable.CompareTo(lowerConverted) >= 0 && 
                       comparable.CompareTo(upperConverted) <= 0;
            }
        }
        catch
        {
            // Fall through
        }

        return false;
    }
}

internal sealed class InExpression : WhereExpression
{
    private readonly string _column;
    private readonly HashSet<object> _values;

    public InExpression(string column, IEnumerable<object?> values)
    {
        _column = column;
        _values = new HashSet<object>(values.Where(v => v != null)!);
    }

    public override bool Evaluate(InMemoryRow row)
    {
        object? value = row[_column];
        if (value == null)
        {
            return false;
        }

        // Direct match
        if (_values.Contains(value))
        {
            return true;
        }

        // Try type conversion
        foreach (object inValue in _values)
        {
            try
            {
                object converted = Convert.ChangeType(inValue, value.GetType(), 
                    System.Globalization.CultureInfo.InvariantCulture);
                if (value.Equals(converted))
                {
                    return true;
                }
            }
            catch
            {
                // Continue
            }
        }

        return false;
    }
}

internal sealed class LikeExpression : WhereExpression
{
    private readonly string _column;
    private readonly string _pattern;
    private readonly Regex _regex;

    public LikeExpression(string column, string pattern)
    {
        _column = column;
        _pattern = pattern;

        // Convert SQL LIKE pattern to regex
        string regexPattern = "^" + Regex.Escape(pattern)
            .Replace("%", ".*")
            .Replace("_", ".")
            + "$";
        // Compile this specific pattern since it's unique per instance
        _regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public override bool Evaluate(InMemoryRow row)
    {
        object? value = row[_column];
        if (value == null)
        {
            return false;
        }

        return _regex.IsMatch(value.ToString() ?? string.Empty);
    }
}

internal sealed class AndExpression : WhereExpression
{
    private readonly WhereExpression _left;
    private readonly WhereExpression _right;

    public AndExpression(WhereExpression left, WhereExpression right)
    {
        _left = left;
        _right = right;
    }

    public override bool Evaluate(InMemoryRow row)
    {
        return _left.Evaluate(row) && _right.Evaluate(row);
    }
}

internal sealed class OrExpression : WhereExpression
{
    private readonly WhereExpression _left;
    private readonly WhereExpression _right;

    public OrExpression(WhereExpression left, WhereExpression right)
    {
        _left = left;
        _right = right;
    }

    public override bool Evaluate(InMemoryRow row)
    {
        return _left.Evaluate(row) || _right.Evaluate(row);
    }
}

internal sealed class NotExpression : WhereExpression
{
    private readonly WhereExpression _inner;

    public NotExpression(WhereExpression inner)
    {
        _inner = inner;
    }

    public override bool Evaluate(InMemoryRow row)
    {
        return !_inner.Evaluate(row);
    }
}

#endregion

#region Lambda Expression Visitor

/// <summary>
/// Converts a lambda expression to work with InMemoryRow
/// </summary>
internal sealed class LambdaToRowPredicateVisitor
{
    private readonly InMemoryTableSchema _schema;

    public LambdaToRowPredicateVisitor(InMemoryTableSchema schema)
    {
        _schema = schema;
    }

    public Func<InMemoryRow, bool> Convert<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class, new()
    {
        // For now, compile the expression and use reflection
        // A more sophisticated implementation would rewrite the expression tree
        Func<TEntity, bool> compiledFunc = expression.Compile();

        return row =>
        {
            TEntity entity = row.ToEntity<TEntity>();
            return compiledFunc(entity);
        };
    }
}

#endregion
