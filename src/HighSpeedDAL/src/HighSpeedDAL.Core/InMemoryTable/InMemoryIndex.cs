using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HighSpeedDAL.Core.InMemoryTable;

/// <summary>
/// Represents an index on one or more columns in an in-memory table.
/// Supports both unique and non-unique indexes.
/// Uses concurrent collections for thread-safe operations.
/// </summary>
public sealed class InMemoryIndex
{
    private readonly string _name;
    private readonly List<string> _columnNames;
    private readonly bool _isUnique;
    private readonly bool _isPrimaryKey;

    // For unique indexes: key -> row reference
    private readonly ConcurrentDictionary<IndexKey, InMemoryRow> _uniqueIndex;

    // For non-unique indexes: key -> list of row references
    private readonly ConcurrentDictionary<IndexKey, ConcurrentBag<InMemoryRow>> _nonUniqueIndex;

    /// <summary>
    /// Name of the index
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Column names included in the index
    /// </summary>
    public IReadOnlyList<string> ColumnNames => _columnNames;

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    public bool IsUnique => _isUnique;

    /// <summary>
    /// Whether this is the primary key index
    /// </summary>
    public bool IsPrimaryKey => _isPrimaryKey;

    /// <summary>
    /// Number of entries in the index
    /// </summary>
    public int Count => _isUnique ? _uniqueIndex.Count : _nonUniqueIndex.Count;

    /// <summary>
    /// Creates a new index
    /// </summary>
    public InMemoryIndex(string name, IEnumerable<string> columnNames, bool isUnique, bool isPrimaryKey = false)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _columnNames = new List<string>(columnNames ?? throw new ArgumentNullException(nameof(columnNames)));
        
        if (_columnNames.Count == 0)
        {
            throw new ArgumentException("Index must have at least one column", nameof(columnNames));
        }

        _isUnique = isUnique || isPrimaryKey; // Primary keys are always unique
        _isPrimaryKey = isPrimaryKey;

        if (_isUnique)
        {
            _uniqueIndex = new ConcurrentDictionary<IndexKey, InMemoryRow>();
            _nonUniqueIndex = null!;
        }
        else
        {
            _nonUniqueIndex = new ConcurrentDictionary<IndexKey, ConcurrentBag<InMemoryRow>>();
            _uniqueIndex = null!;
        }
    }

    /// <summary>
    /// Adds a row to the index
    /// </summary>
    /// <returns>True if successful, false if unique constraint violation</returns>
    public bool Add(InMemoryRow row)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        IndexKey key = CreateKey(row);

        if (_isUnique)
        {
            return _uniqueIndex.TryAdd(key, row);
        }
        else
        {
            _nonUniqueIndex.AddOrUpdate(
                key,
                _ => new ConcurrentBag<InMemoryRow> { row },
                (_, bag) => { bag.Add(row); return bag; });
            return true;
        }
    }

    /// <summary>
    /// Removes a row from the index
    /// </summary>
    public bool Remove(InMemoryRow row)
    {
        if (row == null)
        {
            return false;
        }

        IndexKey key = CreateKey(row);

        if (_isUnique)
        {
            return _uniqueIndex.TryRemove(key, out _);
        }
        else
        {
            if (_nonUniqueIndex.TryGetValue(key, out ConcurrentBag<InMemoryRow>? bag))
            {
                // ConcurrentBag doesn't support removal, so we rebuild
                List<InMemoryRow> remaining = bag.Where(r => !ReferenceEquals(r, row)).ToList();
                if (remaining.Count == 0)
                {
                    _nonUniqueIndex.TryRemove(key, out _);
                }
                else
                {
                    _nonUniqueIndex[key] = new ConcurrentBag<InMemoryRow>(remaining);
                }
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Updates a row's position in the index (removes old key, adds new key)
    /// </summary>
    public bool Update(InMemoryRow row, IndexKey oldKey)
    {
        IndexKey newKey = CreateKey(row);

        // If key hasn't changed, nothing to do
        if (oldKey.Equals(newKey))
        {
            return true;
        }

        if (_isUnique)
        {
            // Check if new key already exists (would be a violation)
            if (_uniqueIndex.ContainsKey(newKey))
            {
                return false;
            }

            _uniqueIndex.TryRemove(oldKey, out _);
            return _uniqueIndex.TryAdd(newKey, row);
        }
        else
        {
            // Remove from old position
            if (_nonUniqueIndex.TryGetValue(oldKey, out ConcurrentBag<InMemoryRow>? oldBag))
            {
                List<InMemoryRow> remaining = oldBag.Where(r => !ReferenceEquals(r, row)).ToList();
                if (remaining.Count == 0)
                {
                    _nonUniqueIndex.TryRemove(oldKey, out _);
                }
                else
                {
                    _nonUniqueIndex[oldKey] = new ConcurrentBag<InMemoryRow>(remaining);
                }
            }

            // Add to new position
            _nonUniqueIndex.AddOrUpdate(
                newKey,
                _ => new ConcurrentBag<InMemoryRow> { row },
                (_, bag) => { bag.Add(row); return bag; });
            return true;
        }
    }

    /// <summary>
    /// Finds rows by index key values
    /// </summary>
    public IEnumerable<InMemoryRow> Find(params object?[] keyValues)
    {
        if (keyValues == null || keyValues.Length != _columnNames.Count)
        {
            throw new ArgumentException(
                $"Expected {_columnNames.Count} key value(s), got {keyValues?.Length ?? 0}");
        }

        IndexKey key = new IndexKey(keyValues);

        if (_isUnique)
        {
            if (_uniqueIndex.TryGetValue(key, out InMemoryRow? row))
            {
                yield return row;
            }
        }
        else
        {
            if (_nonUniqueIndex.TryGetValue(key, out ConcurrentBag<InMemoryRow>? bag))
            {
                foreach (InMemoryRow row in bag)
                {
                    yield return row;
                }
            }
        }
    }

    /// <summary>
    /// Finds a single row by key (for unique indexes)
    /// </summary>
    public InMemoryRow? FindOne(params object?[] keyValues)
    {
        return Find(keyValues).FirstOrDefault();
    }

    /// <summary>
    /// Checks if a key exists in the index
    /// </summary>
    public bool ContainsKey(params object?[] keyValues)
    {
        if (keyValues == null || keyValues.Length != _columnNames.Count)
        {
            return false;
        }

        IndexKey key = new IndexKey(keyValues);

        return _isUnique 
            ? _uniqueIndex.ContainsKey(key) 
            : _nonUniqueIndex.ContainsKey(key);
    }

    /// <summary>
    /// Gets all rows in the index (for full scans)
    /// </summary>
    public IEnumerable<InMemoryRow> GetAllRows()
    {
        if (_isUnique)
        {
            return _uniqueIndex.Values;
        }
        else
        {
            return _nonUniqueIndex.Values.SelectMany(bag => bag);
        }
    }

    /// <summary>
    /// Clears all entries from the index
    /// </summary>
    public void Clear()
    {
        if (_isUnique)
        {
            _uniqueIndex.Clear();
        }
        else
        {
            _nonUniqueIndex.Clear();
        }
    }

    /// <summary>
    /// Creates an index key from a row
    /// </summary>
    public IndexKey CreateKey(InMemoryRow row)
    {
        object?[] values = new object?[_columnNames.Count];
        for (int i = 0; i < _columnNames.Count; i++)
        {
            values[i] = row[_columnNames[i]];
        }
        return new IndexKey(values);
    }

    /// <summary>
    /// Gets the current key for a row (useful for updates)
    /// </summary>
    public IndexKey GetCurrentKey(InMemoryRow row)
    {
        return CreateKey(row);
    }

    /// <summary>
    /// Rebuilds the index from a collection of rows
    /// </summary>
    public void Rebuild(IEnumerable<InMemoryRow> rows)
    {
        Clear();
        foreach (InMemoryRow row in rows)
        {
            if (row.State != RowState.Deleted)
            {
                Add(row);
            }
        }
    }
}

/// <summary>
/// Represents a composite key for index lookups.
/// Supports single and multi-column keys.
/// </summary>
public readonly struct IndexKey : IEquatable<IndexKey>
{
    private readonly object?[] _values;
    private readonly int _hashCode;

    public IndexKey(params object?[] values)
    {
        _values = values ?? Array.Empty<object?>();
        _hashCode = ComputeHashCode(_values);
    }

    public object?[] Values => _values;

    public int ColumnCount => _values.Length;

    public object? this[int index] => _values[index];

    private static int ComputeHashCode(object?[] values)
    {
        unchecked
        {
            int hash = 17;
            foreach (object? value in values)
            {
                hash = hash * 31 + (value?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    public bool Equals(IndexKey other)
    {
        if (_values.Length != other._values.Length)
        {
            return false;
        }

        for (int i = 0; i < _values.Length; i++)
        {
            if (!Equals(_values[i], other._values[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is IndexKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public static bool operator ==(IndexKey left, IndexKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(IndexKey left, IndexKey right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"[{string.Join(", ", _values.Select(v => v?.ToString() ?? "NULL"))}]";
    }
}
