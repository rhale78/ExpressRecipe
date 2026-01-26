using System;
using System.Collections.Generic;
using System.Data;

namespace ExpressRecipe.Data.Common
{
    /// <summary>
    /// High-performance column ordinal cache for mapping operations.
    /// Caches GetOrdinal() results during a single query execution to eliminate redundant lookups.
    /// Ordinal values don't change between rows in the same query result, so caching provides significant performance gains.
    /// Typically used within mapper functions where the same reader is iterated multiple times.
    /// </summary>
    public sealed class ColumnOrdinalCache
    {
        private readonly IDataReader _reader;
        private readonly Dictionary<string, int> _ordinalCache;

        /// <summary>
        /// Creates a new ordinal cache for the given reader.
        /// </summary>
        public ColumnOrdinalCache(IDataReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            // Start with a reasonable capacity to avoid early resizing
            _ordinalCache = new Dictionary<string, int>(capacity: 32);
        }

        /// <summary>
        /// Gets the ordinal for a column name, using cache if available.
        /// </summary>
        public int GetOrdinal(string columnName)
        {
            if (!_ordinalCache.TryGetValue(columnName, out var ordinal))
            {
                ordinal = _reader.GetOrdinal(columnName);
                _ordinalCache[columnName] = ordinal;
            }
            return ordinal;
        }

        /// <summary>
        /// Gets the ordinal for a column name (case-insensitive lookup).
        /// Falls back to case-sensitive lookup if case-insensitive fails.
        /// </summary>
        public int GetOrdinalCaseInsensitive(string columnName)
        {
            // Try exact match first (most common case)
            if (_ordinalCache.TryGetValue(columnName, out var ordinal))
            {
                return ordinal;
            }

            // Try case-insensitive lookup
            foreach (KeyValuePair<string, int> kvp in _ordinalCache)
            {
                if (string.Equals(kvp.Key, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Not in cache, try from reader
            try
            {
                ordinal = _reader.GetOrdinal(columnName);
                _ordinalCache[columnName] = ordinal;
                return ordinal;
            }
            catch (IndexOutOfRangeException)
            {
                // Try case-insensitive on the reader
                for (int i = 0; i < _reader.FieldCount; i++)
                {
                    if (string.Equals(_reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        _ordinalCache[columnName] = i;
                        return i;
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Clears the cache. Useful between different result sets.
        /// </summary>
        public void Clear()
        {
            _ordinalCache.Clear();
        }

        /// <summary>
        /// Gets the number of cached ordinals.
        /// </summary>
        public int Count => _ordinalCache.Count;
    }

    /// <summary>
    /// Extension methods for efficient column reading with ordinal caching.
    /// Usage: var name = reader.GetStringCached(cache, "Name");
    /// </summary>
    public static class ColumnOrdinalCacheExtensions
    {
        /// <summary>
        /// Gets a string value using the ordinal cache.
        /// </summary>
        public static string? GetStringCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        /// <summary>
        /// Gets a Guid value using the ordinal cache.
        /// </summary>
        public static Guid GetGuidCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetGuid(ordinal);
        }

        /// <summary>
        /// Gets a nullable Guid value using the ordinal cache.
        /// </summary>
        public static Guid? GetGuidNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
        }

        /// <summary>
        /// Gets an int value using the ordinal cache.
        /// </summary>
        public static int GetInt32Cached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetInt32(ordinal);
        }

        /// <summary>
        /// Gets a nullable int value using the ordinal cache.
        /// </summary>
        public static int? GetInt32NullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }

        /// <summary>
        /// Gets a DateTime value using the ordinal cache.
        /// </summary>
        public static DateTime GetDateTimeCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetDateTime(ordinal);
        }

        /// <summary>
        /// Gets a nullable DateTime value using the ordinal cache.
        /// </summary>
        public static DateTime? GetDateTimeNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        /// <summary>
        /// Gets a decimal value using the ordinal cache.
        /// </summary>
        public static decimal GetDecimalCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetDecimal(ordinal);
        }

        /// <summary>
        /// Gets a nullable decimal value using the ordinal cache.
        /// </summary>
        public static decimal? GetDecimalNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }

        /// <summary>
        /// Gets a boolean value using the ordinal cache.
        /// </summary>
        public static bool GetBooleanCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetBoolean(ordinal);
        }

        /// <summary>
        /// Gets a nullable boolean value using the ordinal cache.
        /// </summary>
        public static bool? GetBooleanNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
        }

        /// <summary>
        /// Gets a double value using the ordinal cache.
        /// </summary>
        public static double GetDoubleCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetDouble(ordinal);
        }

        /// <summary>
        /// Gets a nullable double value using the ordinal cache.
        /// </summary>
        public static double? GetDoubleNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
        }

        /// <summary>
        /// Gets a float value using the ordinal cache.
        /// </summary>
        public static float GetFloatCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetFloat(ordinal);
        }

        /// <summary>
        /// Gets a nullable float value using the ordinal cache.
        /// </summary>
        public static float? GetFloatNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetFloat(ordinal);
        }

        /// <summary>
        /// Gets a long value using the ordinal cache.
        /// </summary>
        public static long GetInt64Cached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetInt64(ordinal);
        }

        /// <summary>
        /// Gets a nullable long value using the ordinal cache.
        /// </summary>
        public static long? GetInt64NullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        /// <summary>
        /// Gets a byte value using the ordinal cache.
        /// </summary>
        public static byte GetByteCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.GetByte(ordinal);
        }

        /// <summary>
        /// Gets a nullable byte value using the ordinal cache.
        /// </summary>
        public static byte? GetByteNullableCached(this IDataReader reader, ColumnOrdinalCache cache, string columnName)
        {
            var ordinal = cache.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetByte(ordinal);
        }
    }
}
