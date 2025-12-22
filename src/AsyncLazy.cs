using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Lazys.Abstract;

namespace Soenneker.Asyncs.Lazys;

///<inheritdoc cref="IAsyncLazy{T}"/>
public sealed class AsyncLazy<T> : IAsyncLazy<T>
{
    private readonly object _gate = new();

    private readonly Func<Task<T>>? _taskFactory;
    private readonly Func<CancellationToken, Task<T>>? _taskFactoryToken;

    private readonly Func<ValueTask<T>>? _valueTaskFactory;
    private readonly Func<CancellationToken, ValueTask<T>>? _valueTaskFactoryToken;

    private Task<T>? _task;

    public AsyncLazy(Func<Task<T>> factory) => _taskFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncLazy(Func<CancellationToken, Task<T>> factory) => _taskFactoryToken = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncLazy(Func<ValueTask<T>> factory) => _valueTaskFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncLazy(Func<CancellationToken, ValueTask<T>> factory) => _valueTaskFactoryToken = factory ?? throw new ArgumentNullException(nameof(factory));

    public bool IsValueCreated => Volatile.Read(ref _task) is not null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<T> GetTask(CancellationToken cancellationToken = default)
    {
        Task<T>? task = Volatile.Read(ref _task);
        if (task is not null)
            return task;

        return SlowGetTask(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Task<T> SlowGetTask(CancellationToken cancellationToken)
    {
        Task<T>? task = Volatile.Read(ref _task);
        if (task is not null)
            return task;

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            task = _task;
            if (task is not null)
                return task;

            task = CreateTask(cancellationToken);
            Volatile.Write(ref _task, task);
            return task;
        }
    }

    private Task<T> CreateTask(CancellationToken cancellationToken)
    {
        try
        {
            // Prefer ValueTask factories if supplied.
            if (_valueTaskFactoryToken is not null)
                return CreateFromValueTask(_valueTaskFactoryToken(cancellationToken));

            if (_valueTaskFactory is not null)
                return CreateFromValueTask(_valueTaskFactory());

            if (_taskFactoryToken is not null)
                return _taskFactoryToken(cancellationToken);

            return _taskFactory!();
        }
        catch (OperationCanceledException oce)
        {
            // Preserve the token when possible.
            return Task.FromCanceled<T>(oce.CancellationToken.CanBeCanceled ? oce.CancellationToken : cancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    /// <summary>
    /// Avoids ValueTask.AsTask() allocation when the ValueTask completed synchronously.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<T> CreateFromValueTask(ValueTask<T> valueTask)
    {
        if (valueTask.IsCompletedSuccessfully)
        {
            // No allocation besides Task.FromResult's cached/allocated Task.
            return Task.FromResult(valueTask.Result);
        }

        if (valueTask.IsCompleted)
        {
            // Completed synchronously but not successfully: capture exception/cancel without awaiting.
            try
            {
                return Task.FromResult(valueTask.Result);
            }
            catch (OperationCanceledException oce)
            {
                return Task.FromCanceled<T>(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        // Still pending: fall back to Task representation (may allocate if not already Task-backed).
        return valueTask.AsTask();
    }

    // Allows: await _authState;
    public TaskAwaiter<T> GetAwaiter() => GetTask()
        .GetAwaiter();

    public void Reset() => Volatile.Write(ref _task, null);

    public bool TryGetCompletedSuccessfully(out T? value)
    {
        Task<T>? task = Volatile.Read(ref _task);

        if (task is null || task.Status != TaskStatus.RanToCompletion)
        {
            value = default;
            return false;
        }

        value = task.Result;
        return true;
    }
}