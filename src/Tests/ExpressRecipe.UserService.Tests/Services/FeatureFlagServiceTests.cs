using ExpressRecipe.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Moq;

namespace ExpressRecipe.UserService.Tests.Services;

public class FeatureFlagServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Pass-through HybridCache that always invokes the factory (no caching).
    /// Allows unit tests to control repository responses without cache interference.
    /// </summary>
    private sealed class PassThroughHybridCache : HybridCache
    {
        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key, TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => factory(state, cancellationToken);

        public override ValueTask SetAsync<T>(string key, T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => default;

        public override ValueTask RemoveAsync(string key,
            CancellationToken cancellationToken = default)
            => default;

        public override ValueTask RemoveByTagAsync(string tag,
            CancellationToken cancellationToken = default)
            => default;
    }

    private readonly Mock<IFeatureFlagRepository> _repo;
    private readonly PassThroughHybridCache _cache;
    private readonly Mock<ILocalModeConfig> _localMode;
    private readonly FeatureFlagService _svc;

    private const string FeatureKey = "meal-planning";

    public FeatureFlagServiceTests()
    {
        _repo      = new Mock<IFeatureFlagRepository>();
        _cache     = new PassThroughHybridCache();
        _localMode = new Mock<ILocalModeConfig>();
        _localMode.Setup(m => m.IsLocalMode).Returns(false);
        _svc = new FeatureFlagService(_repo.Object, _cache, _localMode.Object);
    }

    private static FeatureFlagDto EnabledFlag(string? requiresTier = null, int rollout = 100) =>
        new() { FeatureKey = FeatureKey, IsEnabled = true, RolloutPercent = rollout, RequiresTier = requiresTier };

    private static FeatureFlagDto DisabledFlag() =>
        new() { FeatureKey = FeatureKey, IsEnabled = false, RolloutPercent = 100 };

    // ── Local mode ────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsEnabledAsync_LocalModeTrue_AlwaysReturnsTrue()
    {
        _localMode.Setup(m => m.IsLocalMode).Returns(true);
        // Even with no flag configured the result should be true
        _repo.Setup(r => r.GetFlagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlagDto?)null);

        bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), "Free");

        result.Should().BeTrue();
        _repo.Verify(r => r.GetFlagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsGloballyEnabledAsync_LocalModeTrue_AlwaysReturnsTrue()
    {
        _localMode.Setup(m => m.IsLocalMode).Returns(true);
        _repo.Setup(r => r.GetFlagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(DisabledFlag());

        bool result = await _svc.IsGloballyEnabledAsync(FeatureKey);

        result.Should().BeTrue();
    }

    // ── Global flag (no user override) ───────────────────────────────────────

    [Fact]
    public async Task IsEnabledAsync_GlobalFlagDisabled_ReturnsFalse()
    {
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(DisabledFlag());
        _repo.Setup(r => r.GetUserOverrideAsync(It.IsAny<Guid>(), FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), "Free");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_FlagNotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((FeatureFlagDto?)null);
        _repo.Setup(r => r.GetUserOverrideAsync(It.IsAny<Guid>(), FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), "Free");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_GlobalFlagEnabled_NoRestrictions_ReturnsTrue()
    {
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(EnabledFlag());
        _repo.Setup(r => r.GetUserOverrideAsync(It.IsAny<Guid>(), FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), "Free");

        result.Should().BeTrue();
    }

    // ── User override wins over global flag ───────────────────────────────────

    [Fact]
    public async Task IsEnabledAsync_UserOverrideOn_GlobalFlagOff_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(DisabledFlag());
        _repo.Setup(r => r.GetUserOverrideAsync(userId, FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserFeatureOverrideDto
             { UserId = userId, FeatureKey = FeatureKey, IsEnabled = true });

        bool result = await _svc.IsEnabledAsync(FeatureKey, userId, "Free");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_UserOverrideOff_GlobalFlagOn_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(EnabledFlag());
        _repo.Setup(r => r.GetUserOverrideAsync(userId, FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new UserFeatureOverrideDto
             { UserId = userId, FeatureKey = FeatureKey, IsEnabled = false });

        bool result = await _svc.IsEnabledAsync(FeatureKey, userId, "Free");

        result.Should().BeFalse();
    }

    // ── Tier requirement ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Plus",    "Free",    false)]
    [InlineData("Plus",    "AdFree",  false)]
    [InlineData("Plus",    "Plus",    true)]
    [InlineData("Plus",    "Premium", true)]
    [InlineData("Premium", "Plus",    false)]
    [InlineData("Premium", "Premium", true)]
    [InlineData("AdFree",  "Free",    false)]
    [InlineData("AdFree",  "AdFree",  true)]
    public async Task IsEnabledAsync_TierRequirement(string requiredTier, string userTier, bool expected)
    {
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(EnabledFlag(requiresTier: requiredTier));
        _repo.Setup(r => r.GetUserOverrideAsync(It.IsAny<Guid>(), FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), userTier);

        result.Should().Be(expected);
    }

    // ── Rollout percent ───────────────────────────────────────────────────────

    [Fact]
    public async Task IsEnabledAsync_RolloutPercent0_ReturnsFalseForAllUsers()
    {
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(EnabledFlag(rollout: 0));
        _repo.Setup(r => r.GetUserOverrideAsync(It.IsAny<Guid>(), FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        // Check many random users — all should be false
        for (int _ = 0; _ < 20; _++)
        {
            bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), "Free");
            result.Should().BeFalse(because: "rollout=0 means no one is included");
        }
    }

    [Fact]
    public async Task IsEnabledAsync_RolloutPercent100_ReturnsTrueForAllUsers()
    {
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(EnabledFlag(rollout: 100));
        _repo.Setup(r => r.GetUserOverrideAsync(It.IsAny<Guid>(), FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        for (int _ = 0; _ < 20; _++)
        {
            bool result = await _svc.IsEnabledAsync(FeatureKey, Guid.NewGuid(), "Free");
            result.Should().BeTrue(because: "rollout=100 means everyone is included");
        }
    }

    [Fact]
    public async Task IsEnabledAsync_RolloutPercent50_SameUserGetsSameResult()
    {
        Guid fixedUser = Guid.NewGuid();
        _repo.Setup(r => r.GetFlagAsync(FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync(EnabledFlag(rollout: 50));
        _repo.Setup(r => r.GetUserOverrideAsync(fixedUser, FeatureKey, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserFeatureOverrideDto?)null);

        bool first  = await _svc.IsEnabledAsync(FeatureKey, fixedUser, "Free");
        bool second = await _svc.IsEnabledAsync(FeatureKey, fixedUser, "Free");
        bool third  = await _svc.IsEnabledAsync(FeatureKey, fixedUser, "Free");

        second.Should().Be(first,  because: "rollout hash is deterministic per user");
        third.Should().Be(first,   because: "rollout hash is deterministic per user");
    }

    // ── GetAllFlagsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllFlagsAsync_DelegatesToRepository()
    {
        List<FeatureFlagDto> flags =
        [
            new() { FeatureKey = "feature-a", IsEnabled = true,  RolloutPercent = 100 },
            new() { FeatureKey = "feature-b", IsEnabled = false, RolloutPercent = 0 }
        ];
        _repo.Setup(r => r.GetAllFlagsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(flags);

        List<FeatureFlagDto> result = await _svc.GetAllFlagsAsync();

        result.Should().HaveCount(2);
        result[0].FeatureKey.Should().Be("feature-a");
    }
}
