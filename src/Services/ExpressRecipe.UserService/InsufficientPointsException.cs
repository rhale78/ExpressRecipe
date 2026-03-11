namespace ExpressRecipe.UserService;

/// <summary>
/// Thrown when a user tries to redeem a reward but has insufficient points balance.
/// </summary>
public sealed class InsufficientPointsException : InvalidOperationException
{
    public int CurrentBalance { get; }
    public int RequiredPoints { get; }

    public InsufficientPointsException(int currentBalance, int requiredPoints)
        : base($"Insufficient points. Current balance: {currentBalance}, required: {requiredPoints}.")
    {
        CurrentBalance = currentBalance;
        RequiredPoints = requiredPoints;
    }
}
