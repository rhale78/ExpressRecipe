namespace ExpressRecipe.UserService;

/// <summary>
/// Represents a business rule violation in the referral system.
/// </summary>
public sealed class ReferralException : InvalidOperationException
{
    public string Code { get; }

    public ReferralException(string code, string message) : base(message)
    {
        Code = code;
    }
}
