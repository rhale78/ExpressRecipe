namespace ExpressRecipe.Messaging.Saga.Abstractions;

/// <summary>The lifecycle status of a saga instance.</summary>
public enum SagaStatus
{
    /// <summary>The saga has been created but not yet started.</summary>
    Pending,
    /// <summary>The saga is actively running (one or more steps in flight).</summary>
    Running,
    /// <summary>All steps completed successfully.</summary>
    Completed,
    /// <summary>The saga has failed and will not proceed.</summary>
    Failed,
    /// <summary>The saga was compensated (rolled back).</summary>
    Compensated
}
