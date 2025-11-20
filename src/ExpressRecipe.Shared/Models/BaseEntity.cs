namespace ExpressRecipe.Shared.Models;

/// <summary>
/// Base entity with common audit fields.
/// All domain entities should inherit from this.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// When this entity was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created this entity.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// When this entity was last updated (UTC).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User who last updated this entity.
    /// </summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Soft delete flag. Deleted entities remain in database but are filtered out.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When this entity was deleted (UTC).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
