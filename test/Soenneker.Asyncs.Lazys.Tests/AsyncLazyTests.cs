using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Specialized;
using Xunit;

namespace Soenneker.Asyncs.Lazys.Tests;

[Collection("Collection")]
public sealed class AsyncLazyTests
{
    [Fact]
    public async ValueTask GetTask_WithTaskFactory_ReturnsValue()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask GetTask_WithValueTaskFactory_ReturnsValue()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return new ValueTask<int>(42);
        });

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask GetTask_WithTaskFactoryToken_ReturnsValue()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(ct =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask GetTask_WithValueTaskFactoryToken_ReturnsValue()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(ct =>
        {
            callCount++;
            return new ValueTask<int>(42);
        });

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask GetTask_MultipleCalls_ReturnsSameTask()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        Task<int> task1 = lazy.GetTask();
        Task<int> task2 = lazy.GetTask();
        Task<int> task3 = lazy.GetTask();

        await Task.WhenAll(task1, task2, task3);

        // Assert
        task2.Should().BeSameAs(task1);
        task3.Should().BeSameAs(task2);
        (await task1).Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask GetTask_ConcurrentCalls_OnlyCallsFactoryOnce()
    {
        // Arrange
        var callCount = 0;
        Func<ValueTask<int>> factory = async () =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(100);
            return 42;
        };
        var lazy = new AsyncLazy<int>(factory);

        // Act
        var tasks = new Task<int>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = lazy.GetTask();
        }

        await Task.WhenAll(tasks);

        // Assert
        callCount.Should().Be(1);
        foreach (Task<int> task in tasks)
        {
            (await task).Should().Be(42);
            task.Should().BeSameAs(tasks[0]);
        }
    }

    [Fact]
    public void IsValueCreated_BeforeAccess_ReturnsFalse()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act & Assert
        lazy.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public async ValueTask IsValueCreated_AfterAccess_ReturnsTrue()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act
        _ = lazy.GetTask();
        await lazy.GetTask();

        // Assert
        lazy.IsValueCreated.Should().BeTrue();
    }

    [Fact]
    public async ValueTask IsValueCreated_AfterReset_ReturnsFalse()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));
        await lazy.GetTask();

        // Act
        lazy.Reset();

        // Assert
        lazy.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public async ValueTask Reset_AllowsFactoryToBeCalledAgain()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        await lazy.GetTask();

        // Act
        lazy.Reset();
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(2);
    }

    [Fact]
    public async ValueTask TryGetCompletedSuccessfully_BeforeCompletion_ReturnsFalse()
    {
        // Arrange
        var tcs = new TaskCompletionSource<int>();
        var lazy = new AsyncLazy<int>(() => tcs.Task);

        // Act
        _ = lazy.GetTask();
        bool success = lazy.TryGetCompletedSuccessfully(out int value);

        // Assert
        success.Should().BeFalse();
        value.Should().Be(default(int));
    }

    [Fact]
    public async ValueTask TryGetCompletedSuccessfully_AfterCompletion_ReturnsTrue()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act
        await lazy.GetTask();
        bool success = lazy.TryGetCompletedSuccessfully(out int value);

        // Assert
        success.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public async ValueTask TryGetCompletedSuccessfully_AfterException_ReturnsFalse()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromException<int>(new InvalidOperationException("Test")));

        // Act
        try
        {
            await lazy.GetTask();
        }
        catch
        {
            // Expected
        }

        bool success = lazy.TryGetCompletedSuccessfully(out int value);

        // Assert
        success.Should().BeFalse();
        value.Should().Be(default(int));
    }

    [Fact]
    public async ValueTask TryGetCompletedSuccessfully_AfterCancellation_ReturnsFalse()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var lazy = new AsyncLazy<int>(ct => Task.FromCanceled<int>(ct));

        // Act
        try
        {
            await lazy.GetTask(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        bool success = lazy.TryGetCompletedSuccessfully(out int value);

        // Assert
        success.Should().BeFalse();
        value.Should().Be(default(int));
    }

    [Fact]
    public async ValueTask GetAwaiter_CanBeAwaited()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act
        int result = await lazy;

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async ValueTask GetTask_WithException_PropagatesException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var lazy = new AsyncLazy<int>(() => Task.FromException<int>(exception));

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask();
        ExceptionAssertions<InvalidOperationException>? ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("Test exception");
    }

    [Fact]
    public async ValueTask GetTask_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var lazy = new AsyncLazy<int>(ct => Task.FromCanceled<int>(ct));

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async ValueTask GetTask_WithCancellationToken_ThrowsIfCancelledBeforeFactory()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var lazy = new AsyncLazy<int>(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(42);
        });

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_WithNullTaskFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<Task<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullValueTaskFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<ValueTask<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullTaskFactoryToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<CancellationToken, Task<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullValueTaskFactoryToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<CancellationToken, ValueTask<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async ValueTask GetTask_WithAsyncFactory_HandlesAsyncOperation()
    {
        // Arrange
        Func<ValueTask<int>> factory = async () =>
        {
            await Task.Delay(50);
            return 42;
        };
        var lazy = new AsyncLazy<int>(factory);

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async ValueTask GetTask_ValueTaskSynchronousCompletion_OptimizesCorrectly()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return new ValueTask<int>(42); // Synchronous completion
        });

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask GetTask_ValueTaskAsynchronousCompletion_HandlesCorrectly()
    {
        // Arrange
        var callCount = 0;
        Func<ValueTask<int>> factory = async () =>
        {
            callCount++;
            await Task.Delay(50);
            return 42;
        };
        var lazy = new AsyncLazy<int>(factory);

        // Act
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async ValueTask Reset_MultipleTimes_WorksCorrectly()
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        await lazy.GetTask();
        lazy.Reset();
        await lazy.GetTask();
        lazy.Reset();
        int result = await lazy.GetTask();

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(3);
    }

    [Fact]
    public async ValueTask GetTask_AfterReset_CreatesNewTask()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));
        Task<int> task1 = lazy.GetTask();
        await task1;

        // Act
        lazy.Reset();
        Task<int> task2 = lazy.GetTask();

        // Assert
        task2.Should().NotBeSameAs(task1);
        (await task2).Should().Be(42);
    }

    [Fact]
    public async ValueTask GetTask_WithStringValue_WorksCorrectly()
    {
        // Arrange
        var lazy = new AsyncLazy<string>(() => Task.FromResult("test"));

        // Act
        string result = await lazy.GetTask();

        // Assert
        result.Should().Be("test");
    }

    [Fact]
    public async ValueTask GetTask_WithNullableValue_WorksCorrectly()
    {
        // Arrange
        var lazy = new AsyncLazy<int?>(() => Task.FromResult<int?>(null));

        // Act
        int? result = await lazy.GetTask();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async ValueTask TryGetCompletedSuccessfully_WithNullableValue_WorksCorrectly()
    {
        // Arrange
        var lazy = new AsyncLazy<int?>(() => Task.FromResult<int?>(null));

        // Act
        await lazy.GetTask();
        bool success = lazy.TryGetCompletedSuccessfully(out int? value);

        // Assert
        success.Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public async ValueTask GetTask_ConcurrentCallsAfterReset_OnlyCallsFactoryOnce()
    {
        // Arrange
        var callCount = 0;
        Func<ValueTask<int>> factory = async () =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(100);
            return 42;
        };
        var lazy = new AsyncLazy<int>(factory);

        await lazy.GetTask();
        lazy.Reset();

        // Act
        var tasks = new Task<int>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = lazy.GetTask();
        }

        await Task.WhenAll(tasks);

        // Assert
        callCount.Should().Be(2); // Once before reset, once after
        foreach (Task<int> task in tasks)
        {
            (await task).Should().Be(42);
            task.Should().BeSameAs(tasks[0]);
        }
    }

    [Fact]
    public async ValueTask GetTask_WithCancellationToken_PassesTokenToFactory()
    {
        // Arrange
        CancellationToken receivedToken = default;
        var cts = new CancellationTokenSource();
        var lazy = new AsyncLazy<int>(ct =>
        {
            receivedToken = ct;
            return Task.FromResult(42);
        });

        // Act
        await lazy.GetTask(cts.Token);

        // Assert
        receivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async ValueTask GetTask_ValueTaskFactoryWithException_HandlesException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test");
        Func<ValueTask<int>> factory = () => new ValueTask<int>(Task.FromException<int>(exception));
        var lazy = new AsyncLazy<int>(factory);

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask();
        ExceptionAssertions<InvalidOperationException>? ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("Test");
    }

    [Fact]
    public async ValueTask GetTask_ValueTaskFactoryWithCancellation_HandlesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var lazy = new AsyncLazy<int>(ct => new ValueTask<int>(Task.FromCanceled<int>(ct)));

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
