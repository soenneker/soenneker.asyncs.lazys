using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Asyncs.Lazys.Abstract;

/// <summary>
/// Thread-safe async lazy initializer that runs a factory once, shares the in-flight operation, and caches the result. Supports Task and ValueTask factories with optimized synchronous paths and optional reset.
/// </summary>
/// <typeparam name="T">The type of the value that is being lazily initialized.</typeparam>
public interface IAsyncLazy<T>
{
    /// <summary>
    /// Gets a value indicating whether the value has been created.
    /// </summary>
    /// <value>
    /// <c>true</c> if the value has been created; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property returns <c>true</c> once the factory has been invoked and the task has been created,
    /// regardless of whether the task has completed successfully, faulted, or been canceled.
    /// </remarks>
    bool IsValueCreated { get; }

    /// <summary>
    /// Gets the task that represents the asynchronous initialization of the value.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task{T}"/> that represents the asynchronous initialization of the value.
    /// The same task instance is returned for all subsequent calls, ensuring thread-safe sharing of the in-flight operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// On the first call, the factory is invoked and the resulting task is cached. Subsequent calls return the same cached task,
    /// ensuring that the factory is only executed once and all callers share the same in-flight operation.
    /// </para>
    /// <para>
    /// If a cancellation token is provided and the factory supports cancellation, it will be passed to the factory.
    /// If the operation is already in progress, the cancellation token is checked before returning the cached task.
    /// </para>
    /// </remarks>
    Task<T> GetTask(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an awaiter used to await the asynchronous initialization of the value.
    /// </summary>
    /// <returns>
    /// A <see cref="TaskAwaiter{T}"/> instance that can be used to await the completion of the lazy initialization.
    /// </returns>
    /// <remarks>
    /// This method enables the <c>await</c> keyword to be used directly on instances of <see cref="IAsyncLazy{T}"/>,
    /// allowing for a more natural async/await syntax.
    /// </remarks>
    TaskAwaiter<T> GetAwaiter();

    /// <summary>
    /// Resets the lazy initializer, allowing the factory to be invoked again on the next access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After calling <see cref="Reset"/>, the cached task is cleared. The next call to <see cref="GetTask"/> or
    /// <see cref="GetAwaiter"/> will invoke the factory again, creating a new task.
    /// </para>
    /// <para>
    /// If the factory is currently executing, this method does not cancel the in-flight operation.
    /// The reset only affects future calls to get the value.
    /// </para>
    /// </remarks>
    void Reset();

    /// <summary>
    /// Attempts to get the value if it has completed successfully.
    /// </summary>
    /// <param name="value">When this method returns, contains the value if the task completed successfully; otherwise, the default value for type <typeparamref name="T"/>.</param>
    /// <returns>
    /// <c>true</c> if the value was successfully retrieved (the task completed successfully); otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method returns <c>false</c> if:
    /// <list type="bullet">
    /// <item><description>The factory has not been invoked yet (<see cref="IsValueCreated"/> is <c>false</c>).</description></item>
    /// <item><description>The task is still in progress.</description></item>
    /// <item><description>The task faulted or was canceled.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This method does not throw exceptions. It is a non-blocking way to check if the value is available
    /// without awaiting the task.
    /// </para>
    /// </remarks>
    bool TryGetCompletedSuccessfully(out T? value);
}
