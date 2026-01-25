using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Cache strategy options
    /// </summary>
    public enum CacheStrategy
    {
        None,
        Memory,
        Distributed,
        TwoLayer
    }

    /// <summary>
    /// Configures caching for an entity
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CacheAttribute : Attribute
    {
        /// <summary>
        /// Gets the cache strategy
        /// </summary>
        public CacheStrategy Strategy { get; }

        /// <summary>
        /// Gets or sets the maximum cache size
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Gets or sets the expiration time in seconds
        /// </summary>
        public int ExpirationSeconds { get; set; }

        /// <summary>
        /// Gets or sets whether to preload the cache on startup
        /// </summary>
        public bool PreloadOnStartup { get; set; }

        /// <summary>
        /// Gets or sets the L2 to L1 promotion interval in seconds (for TwoLayer strategy)
        /// </summary>
        public int PromotionIntervalSeconds { get; set; } = 5;

        public CacheAttribute(CacheStrategy strategy)
        {
            Strategy = strategy;
        }
    }
}
