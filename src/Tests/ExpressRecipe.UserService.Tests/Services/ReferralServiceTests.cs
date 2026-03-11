using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpressRecipe.UserService.Tests.Services;

public class ReferralServiceTests
{
    private readonly Mock<IReferralRepository> _mockRepository;
    private readonly Mock<ILogger<ReferralService>> _mockLogger;
    private readonly ReferralService _service;
    private readonly Guid _userId;

    public ReferralServiceTests()
    {
        _mockRepository = new Mock<IReferralRepository>();
        _mockLogger = new Mock<ILogger<ReferralService>>();
        _service = new ReferralService(_mockRepository.Object, _mockLogger.Object);
        _userId = Guid.NewGuid();
    }

    // ─── COM1 spec: 11th active code → 422 MaxActiveCodesReached ────────────

    [Fact]
    public async Task GetOrCreateReferralCode_UserHas10Codes_ThrowsMaxActiveCodesReached()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetActiveCodeForUserAsync(_userId, default))
            .ReturnsAsync((string?)null);

        _mockRepository
            .Setup(r => r.CountActiveCodesAsync(_userId, default))
            .ReturnsAsync(10);

        // Act
        var act = async () => await _service.GetOrCreateReferralCodeAsync(_userId);

        // Assert
        var ex = await act.Should().ThrowAsync<ReferralException>();
        ex.Which.Code.Should().Be("MaxActiveCodesReached");
    }

    [Fact]
    public async Task GetOrCreateReferralCode_UserHasExistingCode_ReturnsExistingCode()
    {
        // Arrange
        const string existingCode = "TESTCODE";
        _mockRepository
            .Setup(r => r.GetActiveCodeForUserAsync(_userId, default))
            .ReturnsAsync(existingCode);

        // Act
        var code = await _service.GetOrCreateReferralCodeAsync(_userId);

        // Assert
        code.Should().Be(existingCode);
        _mockRepository.Verify(r => r.CreateReferralCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateReferralCode_NoExistingCode_CreatesNew()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetActiveCodeForUserAsync(_userId, default))
            .ReturnsAsync((string?)null);

        _mockRepository
            .Setup(r => r.CountActiveCodesAsync(_userId, default))
            .ReturnsAsync(0);

        _mockRepository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        _mockRepository
            .Setup(r => r.CreateReferralCodeAsync(_userId, It.IsAny<string>(), default))
            .ReturnsAsync("NEWCODE1");

        // Act
        var code = await _service.GetOrCreateReferralCodeAsync(_userId);

        // Assert
        code.Should().NotBeNullOrEmpty();
        _mockRepository.Verify(r => r.CreateReferralCodeAsync(_userId, It.IsAny<string>(), default), Times.Once);
    }

    // ─── COM1 spec: 6th conversion in same calendar month → points NOT awarded ─

    [Fact]
    public async Task RecordConversion_6thConversionThisMonth_PointsNotAwarded()
    {
        // Arrange
        var referredUserId = Guid.NewGuid();
        var referrerId = Guid.NewGuid();
        const string code = "REF12345";

        _mockRepository
            .Setup(r => r.GetReferredByCodeAsync(referredUserId, default))
            .ReturnsAsync(code);

        _mockRepository
            .Setup(r => r.GetCodeByValueAsync(code, default))
            .ReturnsAsync(new ReferralCodeDto
            {
                Id = Guid.NewGuid(),
                UserId = referrerId,
                Code = code,
                IsActive = true,
                UsageCount = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            });

        // 5 conversions already this month (max is 5, so 6th would be capped)
        _mockRepository
            .Setup(r => r.CountConversionsThisMonthAsync(referrerId, default))
            .ReturnsAsync(5);

        // Act
        await _service.RecordConversionAsync(referredUserId);

        // Assert — conversion recorded with 0 points
        _mockRepository.Verify(
            r => r.RecordConversionAsync(
                It.IsAny<Guid>(), referrerId, referredUserId, 0, default),
            Times.Once);
    }

    [Fact]
    public async Task RecordConversion_WithinLimit_Awards500Points()
    {
        // Arrange
        var referredUserId = Guid.NewGuid();
        var referrerId = Guid.NewGuid();
        const string code = "REF12345";

        _mockRepository
            .Setup(r => r.GetReferredByCodeAsync(referredUserId, default))
            .ReturnsAsync(code);

        _mockRepository
            .Setup(r => r.GetCodeByValueAsync(code, default))
            .ReturnsAsync(new ReferralCodeDto
            {
                Id = Guid.NewGuid(),
                UserId = referrerId,
                Code = code,
                IsActive = true,
                UsageCount = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

        _mockRepository
            .Setup(r => r.CountConversionsThisMonthAsync(referrerId, default))
            .ReturnsAsync(2);

        // Act
        await _service.RecordConversionAsync(referredUserId);

        // Assert — conversion recorded with 500 points
        _mockRepository.Verify(
            r => r.RecordConversionAsync(
                It.IsAny<Guid>(), referrerId, referredUserId, 500, default),
            Times.Once);
    }

    // ─── COM1 spec: 21st daily share link → 422 DailyShareLimitReached ──────

    [Fact]
    public async Task CreateShareLink_21stLinkToday_ThrowsDailyShareLimitReached()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.CountShareLinksCreatedTodayAsync(_userId, default))
            .ReturnsAsync(20);

        // Act
        var act = async () => await _service.CreateShareLinkAsync(_userId, "Recipe", Guid.NewGuid());

        // Assert
        var ex = await act.Should().ThrowAsync<ReferralException>();
        ex.Which.Code.Should().Be("DailyShareLimitReached");
    }

    [Fact]
    public async Task CreateShareLink_WithinLimit_CreatesLink()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.CountShareLinksCreatedTodayAsync(_userId, default))
            .ReturnsAsync(5);

        _mockRepository
            .Setup(r => r.CreateShareLinkAsync(_userId, "Recipe", entityId, It.IsAny<string>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync("TOKEN12345678901234567");

        // Act
        var token = await _service.CreateShareLinkAsync(_userId, "Recipe", entityId);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ApplyReferralCode_SelfReferral_ReturnsFalse()
    {
        // Arrange
        const string code = "MYCODE12";
        _mockRepository
            .Setup(r => r.GetCodeByValueAsync(code, default))
            .ReturnsAsync(new ReferralCodeDto
            {
                Id = Guid.NewGuid(),
                UserId = _userId,  // same user
                Code = code,
                IsActive = true,
                UsageCount = 0,
                CreatedAt = DateTime.UtcNow
            });

        // Act
        var result = await _service.ApplyReferralCodeAsync(_userId, code);

        // Assert
        result.Should().BeFalse();
    }
}
