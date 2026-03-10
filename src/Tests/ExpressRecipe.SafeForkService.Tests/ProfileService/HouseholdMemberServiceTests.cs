using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;
using ExpressRecipe.ProfileService.Data;
using ExpressRecipe.ProfileService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.SafeForkService.Tests.ProfileService;

public class HouseholdMemberServiceTests
{
    private readonly Mock<IHouseholdMemberRepository> _repoMock;
    private readonly Mock<IProfileEventPublisher> _publisherMock;
    private readonly Mock<ILogger<HouseholdMemberService>> _loggerMock;

    public HouseholdMemberServiceTests()
    {
        _repoMock = new Mock<IHouseholdMemberRepository>();
        _publisherMock = new Mock<IProfileEventPublisher>();
        _loggerMock = new Mock<ILogger<HouseholdMemberService>>();
    }

    private HouseholdMemberService CreateService()
    {
        return new HouseholdMemberService(
            _repoMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task UpdateMember_ChildSetHasUserAccountTrue_ThrowsArgumentException()
    {
        // Arrange
        HouseholdMemberService service = CreateService();
        Guid householdId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto
            {
                Id = memberId,
                HouseholdId = householdId,
                MemberType = "Child",
                DisplayName = "Little One",
                HasUserAccount = false
            });

        UpdateMemberRequest request = new UpdateMemberRequest
        {
            DisplayName = "Little One",
            HasUserAccount = true
        };

        // Act
        Func<Task> act = async () => await service.UpdateMemberAsync(
            householdId, memberId, request, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*COPPA*");
    }

    [Fact]
    public async Task UpdateMember_InfantSetHasUserAccountTrue_ThrowsArgumentException()
    {
        // Arrange
        HouseholdMemberService service = CreateService();
        Guid householdId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto
            {
                Id = memberId,
                HouseholdId = householdId,
                MemberType = "Infant",
                DisplayName = "Baby",
                HasUserAccount = false
            });

        UpdateMemberRequest request = new UpdateMemberRequest
        {
            DisplayName = "Baby",
            HasUserAccount = true
        };

        // Act
        Func<Task> act = async () => await service.UpdateMemberAsync(
            householdId, memberId, request, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*COPPA*");
    }

    [Fact]
    public async Task UpdateMember_TeenActivatesAccount_PublishesFamilyAdminNotification()
    {
        // Arrange
        HouseholdMemberService service = CreateService();
        Guid householdId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto
            {
                Id = memberId,
                HouseholdId = householdId,
                MemberType = "Teen",
                DisplayName = "Alex",
                HasUserAccount = false
            });

        _repoMock
            .Setup(r => r.UpdateMemberAsync(memberId, It.IsAny<UpdateMemberRequest>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _publisherMock
            .Setup(p => p.PublishFamilyAdminNotificationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        UpdateMemberRequest request = new UpdateMemberRequest
        {
            DisplayName = "Alex",
            HasUserAccount = true
        };

        // Act
        bool result = await service.UpdateMemberAsync(
            householdId, memberId, request, null, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _publisherMock.Verify(
            p => p.PublishFamilyAdminNotificationAsync(
                householdId,
                memberId,
                "TeenAccountActivated",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveMember_CrossHouseholdGuest_UpdatesTypeToRecurringGuest()
    {
        // Arrange
        HouseholdMemberService service = CreateService();
        Guid householdId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto
            {
                Id = memberId,
                HouseholdId = householdId,
                MemberType = "CrossHouseholdGuest",
                DisplayName = "Guest Friend",
                SourceHouseholdId = Guid.NewGuid()
            });

        _repoMock
            .Setup(r => r.UpdateMemberTypeAsync(memberId, "RecurringGuest", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repoMock
            .Setup(r => r.SoftDeleteMemberAsync(memberId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _publisherMock
            .Setup(p => p.PublishMemberRemovedAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        bool result = await service.RemoveMemberAsync(householdId, memberId, null, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _repoMock.Verify(
            r => r.UpdateMemberTypeAsync(memberId, "RecurringGuest", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
