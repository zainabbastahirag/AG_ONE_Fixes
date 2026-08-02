using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;

namespace AgOneSafe.Infrastructure.Execution;

/// <summary>
/// Handles controls that only a human can verify or fix - portal-only toggles, organisational
/// process, procurement. It returns the documented steps instead of pretending to act, which keeps
/// these controls visible in the roadmap rather than silently passing.
/// </summary>
public sealed class ManualExecutor : IControlExecutor
{
    private readonly IPayloadRenderer _renderer;

    public ManualExecutor(IPayloadRenderer renderer) => _renderer = renderer;

    public ExecutorType ExecutorType => ExecutorType.Manual;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ExecutionResult> ExecuteAsync(ExecutionRequest request)
    {
        var instructions = _renderer.Render(request.Action, request.Action.Payload, request.Parameters);

        if (string.IsNullOrWhiteSpace(instructions))
        {
            instructions = request.Control.AuditProcedure;
        }

        return Task.FromResult(new ExecutionResult
        {
            Succeeded = true,
            RequiresManualAction = true,
            Json = JsonSerializer.Serialize(new
            {
                manual = true,
                control = request.Control.VendorReference,
                instructions
            }),
            Output = instructions,
            ExecutedCommand = $"Manual procedure for {request.Control.VendorReference}"
        });
    }
}
