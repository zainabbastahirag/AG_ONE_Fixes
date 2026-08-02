using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;

namespace AgOneSafe.Infrastructure.Execution;

/// <summary>
/// Maps an action's declared executor onto a registered runtime. Adding support for a new runtime
/// is one DI registration - the catalog, the scan engine and the UI need no change.
/// </summary>
public sealed class ExecutorRegistry : IExecutorRegistry
{
    private readonly Dictionary<ExecutorType, IControlExecutor> _executors;

    public ExecutorRegistry(IEnumerable<IControlExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.ExecutorType);
    }

    public IReadOnlyCollection<IControlExecutor> All => _executors.Values;

    public IControlExecutor Resolve(ExecutorType executorType)
    {
        if (_executors.TryGetValue(executorType, out var executor))
        {
            return executor;
        }

        throw new InvalidOperationException(
            $"No executor is registered for {executorType}. Register an IControlExecutor with that ExecutorType.");
    }
}
