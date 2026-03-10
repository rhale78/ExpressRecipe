using ExpressRecipe.Shared.Events;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.UserService.Data;

namespace ExpressRecipe.UserService.Services;

public interface IReferralService
{
    Task<string> GetOrCreateReferralCodeAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ApplyReferralCodeAsync(Guid newUserId, string code, CancellationToken ct = default);
    Task RecordConversionAsync(Guid referredUserId, CancellationToken ct = default);
    Task<string> CreateShareLinkAsync(Guid userId, string entityType, Guid entityId, CancellationToken ct = default);
}

public class ReferralService : IReferralService
{
    private const int MaxActiveCodesPerUser = 10;
    private const int MaxConversionsPerMonth = 5;
    private const int MaxShareLinksPerDay = 20;
    private const int ShareLinkValidityDays = 30;
    private const int ReferralPoints = 500;
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    private readonly IReferralRepository _referralRepository;
    private readonly EventPublisher? _eventPublisher;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(
        IReferralRepository referralRepository,
        ILogger<ReferralService> logger,
        EventPublisher? eventPublisher = null)
    {
        _referralRepository = referralRepository;
        _logger = logger;
        _eventPublisher = eventPublisher;
    }

    public async Task<string> GetOrCreateReferralCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var existingCode = await _referralRepository.GetActiveCodeForUserAsync(userId, ct);
        if (existingCode != null)
        {
            return existingCode;
        }

        var activeCount = await _referralRepository.CountActiveCodesAsync(userId, ct);
        if (activeCount >= MaxActiveCodesPerUser)
        {
            throw new ReferralException("MaxActiveCodesReached",
                $"User already has {MaxActiveCodesPerUser} active referral codes.");
        }

        var code = await GenerateUniqueCodeAsync(ct);
        return await _referralRepository.CreateReferralCodeAsync(userId, code, ct);
    }

    public async Task<bool> ApplyReferralCodeAsync(Guid newUserId, string code, CancellationToken ct = default)
    {
        var referralCode = await _referralRepository.GetCodeByValueAsync(code, ct);
        if (referralCode == null)
        {
            return false;
        }

        // Prevent self-referral
        if (referralCode.UserId == newUserId)
        {
            return false;
        }

        await _referralRepository.ApplyCodeToUserAsync(newUserId, code, ct);
        await _referralRepository.IncrementCodeUsageAsync(referralCode.Id, ct);

        _logger.LogInformation("User {UserId} applied referral code {Code} from referrer {ReferrerId}",
            newUserId, code, referralCode.UserId);

        return true;
    }

    public async Task RecordConversionAsync(Guid referredUserId, CancellationToken ct = default)
    {
        var referredByCode = await _referralRepository.GetReferredByCodeAsync(referredUserId, ct);
        if (string.IsNullOrWhiteSpace(referredByCode))
        {
            return;
        }

        var referralCode = await _referralRepository.GetCodeByValueAsync(referredByCode, ct);
        if (referralCode == null)
        {
            return;
        }

        var referrerId = referralCode.UserId;

        // Enforce monthly cap
        var monthlyConversions = await _referralRepository.CountConversionsThisMonthAsync(referrerId, ct);
        if (monthlyConversions >= MaxConversionsPerMonth)
        {
            _logger.LogInformation(
                "Referral conversion for user {ReferrerId} capped — {Count} conversions this month",
                referrerId, monthlyConversions);
            await _referralRepository.RecordConversionAsync(referralCode.Id, referrerId, referredUserId, 0, ct);
            return;
        }

        await _referralRepository.RecordConversionAsync(referralCode.Id, referrerId, referredUserId, ReferralPoints, ct);

        if (_eventPublisher != null)
        {
            await _eventPublisher.PublishAsync(EventKeys.PointsEarned, new PointsEarnedEvent
            {
                UserId = referrerId,
                Reason = "ReferralConverted",
                Points = ReferralPoints,
                RelatedEntityId = referredUserId
            });
        }

        _logger.LogInformation("Recorded referral conversion: referrer {ReferrerId} earned {Points} points",
            referrerId, ReferralPoints);
    }

    public async Task<string> CreateShareLinkAsync(Guid userId, string entityType, Guid entityId, CancellationToken ct = default)
    {
        var linksToday = await _referralRepository.CountShareLinksCreatedTodayAsync(userId, ct);
        if (linksToday >= MaxShareLinksPerDay)
        {
            throw new ReferralException("DailyShareLimitReached",
                $"Cannot create more than {MaxShareLinksPerDay} share links per day.");
        }

        var token = GenerateShareToken();
        var expiresAt = DateTime.UtcNow.AddDays(ShareLinkValidityDays);

        await _referralRepository.CreateShareLinkAsync(userId, entityType, entityId, token, expiresAt, ct);

        _logger.LogInformation("Created share link {Token} for user {UserId}, entity {EntityType}/{EntityId}",
            token, userId, entityType, entityId);

        return token;
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateCode();
            if (!await _referralRepository.CodeExistsAsync(code, ct))
            {
                return code;
            }
        }
        throw new InvalidOperationException("Could not generate a unique referral code after 10 attempts.");
    }

    private static string GenerateCode()
    {
        var rng = new Random();
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = CodeChars[rng.Next(CodeChars.Length)];
        }
        return new string(chars);
    }

    private static string GenerateShareToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "A").Replace("/", "B").Replace("=", "").Substring(0, 22);
    }
}
