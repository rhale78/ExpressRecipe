using ExpressRecipe.UserService.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.UserService.Services;

public sealed class AllergyAnalysisWorker : BackgroundService
{
    private readonly IAllergyAnalysisQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AllergyAnalysisWorker> _logger;

    public AllergyAnalysisWorker(
        IAllergyAnalysisQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AllergyAnalysisWorker> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Drain any incidents that were not analysed before the last shutdown.
        await ReprocessPendingAsync(stoppingToken);

        await foreach (Guid incidentId in _queue.ReadAllAsync(stoppingToken))
        {
            await ProcessIncidentAsync(incidentId, stoppingToken);
        }
    }

    private async Task ReprocessPendingAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IAllergyIncidentRepository repo =
            scope.ServiceProvider.GetRequiredService<IAllergyIncidentRepository>();

        List<Guid> pending = await repo.GetUnanalyzedIncidentIdsAsync(ct);
        foreach (Guid id in pending) { _queue.Enqueue(id); }
        _logger.LogInformation("Requeued {Count} pending allergy incidents", pending.Count);
    }

    private async Task ProcessIncidentAsync(Guid incidentId, CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IAllergyIncidentRepository repo =
            scope.ServiceProvider.GetRequiredService<IAllergyIncidentRepository>();
        IAllergyDifferentialAnalyzer analyzer =
            scope.ServiceProvider.GetRequiredService<IAllergyDifferentialAnalyzer>();

        try
        {
            AllergyIncidentEngineDto? incident = await repo.GetIncidentForEngineAsync(incidentId, ct);
            if (incident is null) { return; }

            // Run analysis per unique (MemberId, MemberName) combination
            HashSet<(Guid? MemberId, string MemberName)> members = new();
            foreach (IncidentMemberDto m in incident.Members)
            {
                members.Add((m.MemberId, m.MemberName));
            }

            foreach ((Guid? memberId, string memberName) in members)
            {
                await analyzer.RunForMemberAsync(incident.HouseholdId, memberId, memberName, ct);
            }

            await repo.MarkAnalysisRunAsync(incidentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Allergy analysis failed for incident {IncidentId}", incidentId);
            // Do not rethrow — worker keeps running; incident remains AnalysisRun=0 for retry.
        }
    }
}
