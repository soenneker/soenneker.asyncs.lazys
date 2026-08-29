[![](https://img.shields.io/nuget/v/soenneker.asyncs.lazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.lazys/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.lazys/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.lazys/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.asyncs.lazys.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.asyncs.lazys/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.asyncs.lazys/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.asyncs.lazys/actions/workflows/codeql.yml)

# Soenneker.Asyncs.Lazys

Thread-safe async lazy initializer that runs a factory once, shares the in-flight operation, and caches the result. Supports Task and ValueTask factories with optimized synchronous paths and optional reset.

## Install

```bash
dotnet add package Soenneker.Asyncs.Lazys
```

## Quick start

```csharp
using Soenneker.Asyncs.Lazys.Abstract;

IAsyncLazy<T> asyncLazy = /* resolve from DI */;
var result = await asyncLazy.GetTask(default);
```

Gets the task that represents the asynchronous initialization of the value.

## What you get

- `IAsyncLazy<T>` — Thread-safe async lazy initializer that runs a factory once, shares the in-flight operation, and caches the result. Supports Task and ValueTask factories with optimized synchronous paths and optional reset.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `IAsyncLazy<T>.IsValueCreated` | Gets a value indicating whether the value has been created. | This property returns `true` once the factory has been invoked and the task has been created, regardless of whether the task has completed successfully, faulted, or been canceled. |
| `IAsyncLazy<T>.GetTask(cancellationToken)` | Gets the task that represents the asynchronous initialization of the value. | A `Task{T}` that represents the asynchronous initialization of the value. The same task instance is returned for all subsequent calls, ensuring thread-safe sharing of the in-flight operation. |
| `IAsyncLazy<T>.GetAwaiter()` | Gets an awaiter used to await the asynchronous initialization of the value. | A `TaskAwaiter{T}` instance that can be used to await the completion of the lazy initialization. |
| `IAsyncLazy<T>.Reset()` | Resets the lazy initializer, allowing the factory to be invoked again on the next access. | After calling `Reset`, the cached task is cleared. The next call to `GetTask` or `GetAwaiter` will invoke the factory again, creating a new task. If the factory is currently executing, this method does not cancel the in-flight operation. The reset only affects future calls to get the value. |
| `IAsyncLazy<T>.TryGetCompletedSuccessfully(value)` | Attempts to get the value if it has completed successfully. | `true` if the value was successfully retrieved (the task completed successfully); otherwise, `false`. |

## Important behavior

- `IAsyncLazy<T>.IsValueCreated`: This property returns `true` once the factory has been invoked and the task has been created, regardless of whether the task has completed successfully, faulted, or been canceled.
- `IAsyncLazy<T>.GetTask(cancellationToken)`: On the first call, the factory is invoked and the resulting task is cached. Subsequent calls return the same cached task, ensuring that the factory is only executed once and all callers share the same in-flight operation. If a cancellation token is provided and the factory supports cancellation, it will be passed to the factory. If the operation is already in progress, the cancellation token is checked before returning the cached task.
- `IAsyncLazy<T>.GetAwaiter()`: This method enables the `await` keyword to be used directly on instances of `IAsyncLazy{T}`, allowing for a more natural async/await syntax.
- `IAsyncLazy<T>.Reset()`: After calling `Reset`, the cached task is cleared. The next call to `GetTask` or `GetAwaiter` will invoke the factory again, creating a new task. If the factory is currently executing, this method does not cancel the in-flight operation. The reset only affects future calls to get the value.
- `IAsyncLazy<T>.TryGetCompletedSuccessfully(value)`: This method returns `false` if: The factory has not been invoked yet (`IsValueCreated` is `false`). The task is still in progress. The task faulted or was canceled. This method does not throw exceptions. It is a non-blocking way to check if the value is available without awaiting the task.

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
