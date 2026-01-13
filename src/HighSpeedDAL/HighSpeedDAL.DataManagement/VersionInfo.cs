using System;

namespace HighSpeedDAL.DataManagement.Versioning
{
    /// <summary>
    /// Contains information about an entity's version state.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// Gets or sets the current version value as a byte array (for RowVersion strategy).
        /// </summary>
        public byte[]? RowVersionValue { get; set; }

        /// <summary>
        /// Gets or sets the current version value as a DateTime (for Timestamp strategy).
        /// </summary>
        public DateTime? TimestampValue { get; set; }

        /// <summary>
        /// Gets or sets the current version value as an integer (for Integer strategy).
        /// </summary>
        public int? IntegerValue { get; set; }

        /// <summary>
        /// Gets or sets the current version value as a GUID (for Guid strategy).
        /// </summary>
        public Guid? GuidValue { get; set; }

        /// <summary>
        /// Gets or sets the versioning strategy used.
        /// </summary>
        public VersionStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the name of the property that holds the version value.
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the database column that holds the version value.
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when this version was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the user who created this version (if tracked).
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets a value indicating whether this version info contains a valid version value.
        /// </summary>
        public bool HasValue
        {
            get
            {
                return Strategy switch
                {
                    VersionStrategy.RowVersion => RowVersionValue != null && RowVersionValue.Length > 0,
                    VersionStrategy.Timestamp => TimestampValue.HasValue,
                    VersionStrategy.Integer => IntegerValue.HasValue,
                    VersionStrategy.Guid => GuidValue.HasValue,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Gets the version value as an object for comparison purposes.
        /// </summary>
        /// <returns>The version value as an object.</returns>
        public object? GetVersionValue()
        {
            return Strategy switch
            {
                VersionStrategy.RowVersion => RowVersionValue,
                VersionStrategy.Timestamp => TimestampValue,
                VersionStrategy.Integer => IntegerValue,
                VersionStrategy.Guid => GuidValue,
                _ => null
            };
        }

        /// <summary>
        /// Compares this version with another to determine if they are equal.
        /// </summary>
        /// <param name="other">The other version to compare with.</param>
        /// <returns>True if versions are equal, false otherwise.</returns>
        public bool EqualsVersion(VersionInfo? other)
        {
            if (other == null || Strategy != other.Strategy)
            {
                return false;
            }

            return Strategy switch
            {
                VersionStrategy.RowVersion => ByteArrayEquals(RowVersionValue, other.RowVersionValue),
                VersionStrategy.Timestamp => TimestampValue == other.TimestampValue,
                VersionStrategy.Integer => IntegerValue == other.IntegerValue,
                VersionStrategy.Guid => GuidValue == other.GuidValue,
                _ => false
            };
        }

        /// <summary>
        /// Compares two byte arrays for equality.
        /// </summary>
        /// <param name="a">First byte array.</param>
        /// <param name="b">Second byte array.</param>
        /// <returns>True if arrays are equal, false otherwise.</returns>
        private bool ByteArrayEquals(byte[]? a, byte[]? b)
        {
            if (a == null && b == null)
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
