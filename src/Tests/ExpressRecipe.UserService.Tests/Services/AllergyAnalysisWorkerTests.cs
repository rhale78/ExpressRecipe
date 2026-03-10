using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.UserService.Tests.Services;

public class AllergyAnalysisWorkerTests
{
    private readonly Mock<IAllergyIncidentRepository> _repoMock;
    private readonly Mock<IAllergyDifferentialAnalyzer> _analyzerMock;
    private readonly AllergyAnalysisQueue _queue;
    private readonly ServiceProvider _serviceProvider;

    public AllergyAnalysisWorkerTests()
    {
        _repoMock     = new Mock<IAllergyIncidentRepository>();
        _analyzerMock = new Mock<IAllergyDifferentialAnalyzer>();
        _queue        = new AllergyAnalysisQueue();

        ServiceCollection services = new();
        services.AddScoped<IAllergyIncidentRepository>(_ => _repoMock.Object);
        services.AddScoped<IAllergyDifferentialAnalyzer>(_ => _analyzerMock.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    private AllergyAnalysisWorker CreateWorker()
        => new AllergyAnalysisWorker(
            _queue,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AllergyAnalysisWorker>.Instance);

    [Fact]
    public async Task StartAsync_PendingIncidents_AllRequeued()
    {
        // Arrange — 3 unanalyzed incidents sitting in the DB
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        Guid id3 = Guid.NewGuid();

        _repoMock.Setup(r => r.GetUnanalyzedIncidentIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { id1, id2, id3 });

        // Each incident has one member
        _repoMock.Setup(r => r.GetIncidentByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, ct) => Task.FromResult<AllergyIncidentEngineDto?>(
                new AllergyIncidentEngineDto
                {
                    Id          = id,
                    HouseholdId = Guid.NewGuid(),
                    Members     = new List<IncidentMemberDto>
                    {
                        new() { MemberId = Guid.NewGuid(), MemberName = "TestUser" }
                    }
                }));

        AllergyAnalysisWorker worker = CreateWorker();

        using CancellationTokenSource cts = new();
        await worker.StartAsync(cts.Token);

        // Give the worker time to drain the re-queued incidents
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        // Assert — analyzer called 3 times (once per incident)
        _analyzerMock.Verify(
            a => a.RunForMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _repoMock.Verify(
            r => r.MarkAnalysisRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessIncident_AnalyzerThrows_WorkerContinues_IncidentRemainsUnanalyzed()
    {
        // Arrange
        Guid failId    = Guid.NewGuid();
        Guid successId = Guid.NewGuid();

        _repoMock.Setup(r => r.GetUnanalyzedIncidentIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        _repoMock.Setup(r => r.GetIncidentByIdAsync(failId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AllergyIncidentEngineDto
            {
                Id          = failId,
                HouseholdId = Guid.NewGuid(),
                Members     = new List<IncidentMemberDto>
                    { new() { MemberId = Guid.NewGuid(), MemberName = "Bob" } }
            });

        _repoMock.Setup(r => r.GetIncidentByIdAsync(successId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AllergyIncidentEngineDto
            {
                Id          = successId,
                HouseholdId = Guid.NewGuid(),
                Members     = new List<IncidentMemberDto>
                    { new() { MemberId = Guid.NewGuid(), MemberName = "Carol" } }
            });

        _analyzerMock
            .Setup(a => a.RunForMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), "Bob",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated failure"));

        _analyzerMock
            .Setup(a => a.RunForMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), "Carol",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        AllergyAnalysisWorker worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);

        // Enqueue failing incident, then successful one
        _queue.Enqueue(failId);
        _queue.Enqueue(successId);

        await Task.Delay(500);  // let worker drain
        await worker.StopAsync(CancellationToken.None);

        // failId: MarkAnalysisRunAsync NOT called (exception was swallowed)
        _repoMock.Verify(r => r.MarkAnalysisRunAsync(failId, It.IsAny<CancellationToken>()),
            Times.Never);

        // successId: MarkAnalysisRunAsync called
        _repoMock.Verify(r => r.MarkAnalysisRunAsync(successId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Enqueue_DequeueRoundTrip_YieldsCorrectId()
    {
        Guid expected = Guid.NewGuid();
        _queue.Enqueue(expected);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(1));
        Guid actual = await _queue.ReadAllAsync(cts.Token).FirstAsync(cts.Token);

        actual.Should().Be(expected);
    }
}
