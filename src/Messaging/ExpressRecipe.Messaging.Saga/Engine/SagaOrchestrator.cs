using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.BatchWriter;
using ExpressRecipe.Messaging.Saga.Builder;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Saga.Engine;

/// <summary>
/// Orchestrates instances of a specific saga workflow using the bit-flag DAG model.
/// Subscribes to step result messages via <see cref="IMessageBus"/>, advances state
/// using bitwise mask operations, and persists updates via <see cref="SagaBatchWriter{TState}"/>.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaOrchestrator<TState> : ISagaOrchestrator<TState>, IAsyncDisposable
    where TState : class, ISagaState
{
    private readonly SagaWorkflowDefinition<TState> _workflow;
    private readonly ISagaBatchRepository<TState> _repository;
    private readonly IMessageBus _bus;
    private readonly SagaBatchWriter<TState> _batchWriter;
    private readonly ILogger<SagaOrchestrator<TState>> _logger;
    private int _initialized;

    /// <summary>
    /// Initializes the orchestrator with the compiled workflow and required dependencies.
    /// </summary>
    public SagaOrchestrator(
        SagaWorkflowDefinition<TState> workflow,
        ISagaBatchRepository<TState> repository,
        IMessageBus bus,
        SagaBatchWriter<TState> batchWriter,
        ILogger<SagaOrchestrator<TState>> logger)
    {
        _workflow = workflow;
        _repository = repository;
        _bus = bus;
        _batchWriter = batchWriter;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes all result-message handlers for this workflow's steps.
    /// Must be called once during application startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0) return;
        foreach (var step in _workflow.Steps)
        {
            if (step.ResultType is null) continue;

            // Use a static generic helper to call SubscribeAsync<TResult> cleanly
            var subscribeHelper = typeof(SagaOrchestrator<TState>)
                .GetMethod(nameof(SubscribeStepResultAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(step.ResultType);

            var task = (Task)subscribeHelper.Invoke(null, [_bus, this, step, cancellationToken])!;
            await task.ConfigureAwait(false);

            _logger.LogInformation(
                "Saga '{Workflow}': subscribed to result type '{ResultType}' for step '{Step}'",
                _workflow.WorkflowName, step.ResultType.Name, step.Name);
        }
    }

    // Static generic helper avoids complex delegate construction via reflection
    private static async Task SubscribeStepResultAsync<TResult>(
        IMessageBus bus,
        SagaOrchestrator<TState> orchestrator,
        SagaStepDefinition<TState> step,
        CancellationToken cancellationToken)
        where TResult : IMessage
    {
        await bus.SubscribeAsync<TResult>(
            async (msg, ctx, ct) => await orchestrator.HandleResultMessageAsync(step, msg, ctx, ct),
            new SubscribeOptions { RoutingMode = RoutingMode.CompetingConsumer },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StartAsync(TState initialState, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(initialState.CorrelationId))
            throw new ArgumentException("CorrelationId must be set on the initial state.", nameof(initialState));

        initialState.Status = SagaStatus.Running;
        initialState.StartedAt = DateTimeOffset.UtcNow;
        initialState.CurrentMask = 0;

        await _repository.SaveAsync(initialState, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Saga '{Workflow}' started: CorrelationId={CorrelationId}",
            _workflow.WorkflowName, initialState.CorrelationId);

        await DispatchEligibleStepsAsync(initialState, cancellationToken).ConfigureAwait(false);
    }

    // ── Internal step dispatch ────────────────────────────────────────────────

    private async Task DispatchEligibleStepsAsync(TState state, CancellationToken cancellationToken)
    {
        var eligible = _workflow.GetEligibleSteps(state.CurrentMask).ToList();
        foreach (var step in eligible)
        {
            if (step.CommandFactory is null)
            {
                _logger.LogDebug(
                    "Saga '{Workflow}' step '{Step}': no command factory, treating as instant-complete",
                    _workflow.WorkflowName, step.Name);

                // Step with no command is auto-completed
                await HandleStepCompleteAsync(state.CorrelationId, step, null, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var (cmd, cmdType) = step.CommandFactory(state);

                // Use a static generic helper for SendAsync to avoid reflection on the method directly
                var sendHelper = typeof(SagaOrchestrator<TState>)
                    .GetMethod(nameof(SendCommandAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(cmdType);

                var sendOptions = new SendOptions { CorrelationId = state.CorrelationId };
                // Use the explicitly configured destination if set; fall back to command type name
                var destination = step.CommandDestination ?? cmdType.Name.ToLowerInvariant();

                var sendTask = (Task)sendHelper.Invoke(null, [_bus, cmd, destination, sendOptions, cancellationToken])!;
                await sendTask.ConfigureAwait(false);

                _logger.LogDebug(
                    "Saga '{Workflow}' step '{Step}': sent command '{Command}' for CorrelationId={CorrelationId}",
                    _workflow.WorkflowName, step.Name, cmdType.Name, state.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Saga '{Workflow}' step '{Step}': error dispatching command for CorrelationId={CorrelationId}",
                    _workflow.WorkflowName, step.Name, state.CorrelationId);

                await HandleStepFailedAsync(state, step, ex, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task SendCommandAsync<TCommand>(
        IMessageBus bus,
        TCommand command,
        string destination,
        SendOptions options,
        CancellationToken cancellationToken)
        where TCommand : IMessage
    {
        await bus.SendAsync(command, destination, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleStepCompleteAsync(
        string correlationId, SagaStepDefinition<TState> step, object? resultMessage, CancellationToken cancellationToken)
    {
        // Load current state
        var state = await _repository.LoadAsync(correlationId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            _logger.LogWarning(
                "Saga '{Workflow}' step '{Step}': state not found for CorrelationId={CorrelationId}",
                _workflow.WorkflowName, step.Name, correlationId);
            return;
        }

        // Run optional result handler (may mutate state)
        if (step.ResultHandler is not null && resultMessage is not null)
        {
            state = await step.ResultHandler(state, resultMessage, cancellationToken).ConfigureAwait(false);
        }

        // Set the step's bit in CurrentMask
        state.CurrentMask |= step.Bit;

        // Determine if workflow is complete
        bool isComplete = _workflow.IsComplete(state.CurrentMask);
        if (isComplete)
        {
            state.Status = SagaStatus.Completed;
            state.CompletedAt = DateTimeOffset.UtcNow;

            await _batchWriter.EnqueueAsync(correlationId, step.Bit, SagaStatus.Completed, state.CompletedAt, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Saga '{Workflow}' COMPLETED: CorrelationId={CorrelationId}",
                _workflow.WorkflowName, correlationId);

            if (_workflow.OnCompleted is not null)
            {
                try { await _workflow.OnCompleted(state, _bus, cancellationToken).ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogError(ex, "Saga '{Workflow}': error in OnCompleted callback", _workflow.WorkflowName); }
            }
        }
        else
        {
            await _batchWriter.EnqueueMaskUpdateAsync(correlationId, step.Bit, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Saga '{Workflow}' step '{Step}' completed: CorrelationId={CorrelationId}, Mask=0x{Mask:X}",
                _workflow.WorkflowName, step.Name, correlationId, state.CurrentMask);

            // Dispatch any newly eligible steps
            await DispatchEligibleStepsAsync(state, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleStepFailedAsync(
        TState state, SagaStepDefinition<TState> step, Exception ex, CancellationToken cancellationToken)
    {
        state.Status = SagaStatus.Failed;

        await _batchWriter.EnqueueStatusUpdateAsync(state.CorrelationId, SagaStatus.Failed, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (step.CompensationHandler is not null)
        {
            try { await step.CompensationHandler(state, ex, cancellationToken).ConfigureAwait(false); }
            catch (Exception cex) { _logger.LogError(cex, "Saga '{Workflow}' step '{Step}': error in compensation handler", _workflow.WorkflowName, step.Name); }
        }

        if (_workflow.OnFailed is not null)
        {
            try { await _workflow.OnFailed(state, ex, _bus, cancellationToken).ConfigureAwait(false); }
            catch (Exception fex) { _logger.LogError(fex, "Saga '{Workflow}': error in OnFailed callback", _workflow.WorkflowName); }
        }
    }

    internal async Task HandleResultMessageAsync<TResult>(
        SagaStepDefinition<TState> step, TResult result, MessageContext context, CancellationToken cancellationToken)
        where TResult : IMessage
    {
        var correlationId = context.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogWarning(
                "Saga '{Workflow}' step '{Step}': received result with no CorrelationId, skipping",
                _workflow.WorkflowName, step.Name);
            return;
        }

        await HandleStepCompleteAsync(correlationId, step, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _batchWriter.DisposeAsync().ConfigureAwait(false);
    }
}
