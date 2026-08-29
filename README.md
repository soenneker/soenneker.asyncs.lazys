[![](https://img.shields.io/nuget/v/soenneker.asyncs.lazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.lazys/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.lazys/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.lazys/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.asyncs.lazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.lazys/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.lazys/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.lazys/actions/workflows/codeql.yml)

# Soenneker.Asyncs.Lazys

A thread-safe async lazy value that invokes a factory once, shares the in-flight `Task<T>`, and caches its final result, exception, or cancellation.

Use `AsyncLazy<T>` when several callers may need an expensive value and only the first access should start its creation.

## Installation

```bash
dotnet add package Soenneker.Asyncs.Lazys
```

## Create and await a lazy value

```csharp
using Soenneker.Asyncs.Lazys;

var signingKeys = new AsyncLazy<IReadOnlyList<string>>(
    async cancellationToken =>
    {
        return await LoadSigningKeys(cancellationToken);
    });

IReadOnlyList<string> keys = await signingKeys.GetTask(cancellationToken);
```

Concurrent calls return the same task, so the factory runs once and every caller observes the same outcome.

`AsyncLazy<T>` itself is awaitable when no cancellation token needs to be supplied:

```csharp
IReadOnlyList<string> keys = await signingKeys;
```

## Factory overloads

Factories can return either `Task<T>` or `ValueTask<T>`, with or without a cancellation token:

```csharp
new AsyncLazy<T>(Func<Task<T>> factory);
new AsyncLazy<T>(Func<CancellationToken, Task<T>> factory);
new AsyncLazy<T>(Func<ValueTask<T>> factory);
new AsyncLazy<T>(Func<CancellationToken, ValueTask<T>> factory);
```

Synchronous `ValueTask<T>` completion is converted directly into a completed cached task.

## Cancellation semantics

The token supplied by the caller that starts initialization is passed to a token-aware factory. Once the cached task exists, later calls return it directly; their cancellation tokens do not cancel waiting and are not passed to the factory.

If each caller needs independently cancellable waiting while the shared work continues, apply cancellation while awaiting the returned task:

```csharp
T value = await lazy.GetTask().WaitAsync(cancellationToken);
```

A token already cancelled before the first initialization attempt prevents the factory from running and throws `OperationCanceledException` to that caller. Cancellation produced by the factory is cached like any other result until `Reset` is called.

## Exceptions and retries

Synchronous factory exceptions are captured into the cached task rather than thrown directly from `GetTask`. Asynchronous failures are naturally retained by that task. Every caller then observes the same exception.

To permit a new attempt after a failure or cancellation, explicitly reset the lazy:

```csharp
try
{
    return await lazy.GetTask(cancellationToken);
}
catch
{
    lazy.Reset();
    throw;
}
```

Coordinate reset policy at the owning-service level. If `Reset` races with an in-flight factory, it does not cancel that work; a later caller can start a second factory while callers holding the old task still await the first one.

## Inspect without waiting

`IsValueCreated` means a task has been cached. It does not mean the task completed successfully.

Use `TryGetCompletedSuccessfully` for a non-blocking successful-result check:

```csharp
if (lazy.TryGetCompletedSuccessfully(out T? value))
{
    Use(value);
}
```

It returns `false` before initialization, while work is running, and after a fault or cancellation. It does not throw a cached exception.

## API

| Member | Behavior |
| --- | --- |
| `GetTask(CancellationToken)` | Creates or returns the shared cached task. |
| `await lazy` | Awaits `GetTask()` without a token. |
| `IsValueCreated` | Indicates whether the cached task exists. |
| `TryGetCompletedSuccessfully(out T?)` | Reads an already-successful result without blocking. |
| `Reset()` | Clears the cached task so a later access invokes the factory again. |
