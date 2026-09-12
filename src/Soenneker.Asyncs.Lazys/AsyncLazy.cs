using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Lazys.Abstract;

namespace Soenneker.Asyncs.Lazys;

public sealed class AsyncLazy<T> : IAsyncLazy<T>
{
    private readonly object _gate = new();

    private readonly Delegate _factory;

    private Task<T>? _task;

    public AsyncLazy(Func<Task<T>> factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncLazy(Func<CancellationToken, Task<T>> factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncLazy(Func<ValueTask<T>> factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncLazy(Func<CancellationToken, ValueTask<T>> factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

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
            return _factory switch
            {
                Func<ValueTask<T>> factory => factory().AsTask(),
                Func<Task<T>> factory => factory(),
                Func<CancellationToken, ValueTask<T>> factory => factory(cancellationToken).AsTask(),
                _ => ((Func<CancellationToken, Task<T>>)_factory)(cancellationToken)
            };
        }
        catch (OperationCanceledException oce)
        {
            // Preserve the token when possible.
            CancellationToken token = oce.CancellationToken.CanBeCanceled ? oce.CancellationToken : cancellationToken;
            if (token.IsCancellationRequested)
                return Task.FromCanceled<T>(token);

            var completion = new TaskCompletionSource<T>();
            completion.SetCanceled(token);
            return completion.Task;
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    public TaskAwaiter<T> GetAwaiter() => GetTask()
        .GetAwaiter();

    public void Reset() => Volatile.Write(ref _task, null);

    public bool TryGetCompletedSuccessfully(out T? value)
    {
        Task<T>? task = Volatile.Read(ref _task);

        if (task is null || !task.IsCompletedSuccessfully)
        {
            value = default;
            return false;
        }

        value = task.Result;
        return true;
    }
}
