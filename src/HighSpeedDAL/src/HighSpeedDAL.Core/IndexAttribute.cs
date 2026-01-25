using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Specifies that a property should have an index
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class IndexAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the index name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Alias for Name property (for test compatibility)
        /// </summary>
        public string? IndexName => Name;

        /// <summary>
        /// Gets or sets whether the index is unique
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets the order of this column in a composite index
        /// </summary>
        public int Order { get; set; }

        public IndexAttribute()
        {
        }

        public IndexAttribute(string name)
        {
            Name = name;
        }

        public IndexAttribute(string name, bool isUnique)
        {
            Name = name;
            IsUnique = isUnique;
        }
    }
}
