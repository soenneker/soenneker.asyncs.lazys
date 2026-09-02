using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Specialized;

namespace Soenneker.Asyncs.Lazys.Tests;

public sealed class AsyncLazyTests
{
    [Test]
    public async ValueTask GetTask_WithTaskFactory_ReturnsValue(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask GetTask_WithValueTaskFactory_ReturnsValue(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return new ValueTask<int>(42);
        });

        // Act
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask GetTask_WithTaskFactoryToken_ReturnsValue(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(ct =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask GetTask_WithValueTaskFactoryToken_ReturnsValue(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(ct =>
        {
            callCount++;
            return new ValueTask<int>(42);
        });

        // Act
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask GetTask_MultipleCalls_ReturnsSameTask(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        Task<int> task1 = lazy.GetTask(cancellationToken: cancellationToken);
        Task<int> task2 = lazy.GetTask(cancellationToken: cancellationToken);
        Task<int> task3 = lazy.GetTask(cancellationToken: cancellationToken);

        await Task.WhenAll(task1, task2, task3);

        // Assert
        task2.Should().BeSameAs(task1);
        task3.Should().BeSameAs(task2);
        (await task1).Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask GetTask_ConcurrentCalls_OnlyCallsFactoryOnce(CancellationToken cancellationToken)
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
            tasks[i] = lazy.GetTask(cancellationToken: cancellationToken);
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

    [Test]
    public void IsValueCreated_BeforeAccess_ReturnsFalse()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act & Assert
        lazy.IsValueCreated.Should().BeFalse();
    }

    [Test]
    public async ValueTask IsValueCreated_AfterAccess_ReturnsTrue(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act
        _ = lazy.GetTask(cancellationToken: cancellationToken);
        await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        lazy.IsValueCreated.Should().BeTrue();
    }

    [Test]
    public async ValueTask IsValueCreated_AfterReset_ReturnsFalse(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));
        await lazy.GetTask(cancellationToken: cancellationToken);

        // Act
        lazy.Reset();

        // Assert
        lazy.IsValueCreated.Should().BeFalse();
    }

    [Test]
    public async ValueTask Reset_AllowsFactoryToBeCalledAgain(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        await lazy.GetTask(cancellationToken: cancellationToken);

        // Act
        lazy.Reset();
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(2);
    }

    [Test]
    public async ValueTask TryGetCompletedSuccessfully_BeforeCompletion_ReturnsFalse(CancellationToken cancellationToken)
    {
        // Arrange
        var tcs = new TaskCompletionSource<int>();
        var lazy = new AsyncLazy<int>(() => tcs.Task);

        // Act
        _ = lazy.GetTask(cancellationToken: cancellationToken);
        bool success = lazy.TryGetCompletedSuccessfully(out int value);

        // Assert
        success.Should().BeFalse();
        value.Should().Be(default(int));
    }

    [Test]
    public async ValueTask TryGetCompletedSuccessfully_AfterCompletion_ReturnsTrue(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act
        await lazy.GetTask(cancellationToken: cancellationToken);
        bool success = lazy.TryGetCompletedSuccessfully(out int value);

        // Assert
        success.Should().BeTrue();
        value.Should().Be(42);
    }

    [Test]
    public async ValueTask TryGetCompletedSuccessfully_AfterException_ReturnsFalse(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromException<int>(new InvalidOperationException("Test")));

        // Act
        try
        {
            await lazy.GetTask(cancellationToken: cancellationToken);
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

    [Test]
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

    [Test]
    public async ValueTask GetAwaiter_CanBeAwaited()
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));

        // Act
        int result = await lazy;

        // Assert
        result.Should().Be(42);
    }

    [Test]
    public async ValueTask GetTask_WithException_PropagatesException(CancellationToken cancellationToken)
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var lazy = new AsyncLazy<int>(() => Task.FromException<int>(exception));

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask(cancellationToken: cancellationToken);
        ExceptionAssertions<InvalidOperationException>? ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("Test exception");
    }

    [Test]
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

    [Test]
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

    [Test]
    public void Constructor_WithNullTaskFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<Task<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WithNullValueTaskFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<ValueTask<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WithNullTaskFactoryToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<CancellationToken, Task<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WithNullValueTaskFactoryToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<AsyncLazy<int>> act = () => new AsyncLazy<int>((Func<CancellationToken, ValueTask<int>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public async ValueTask GetTask_WithAsyncFactory_HandlesAsyncOperation(CancellationToken cancellationToken)
    {
        // Arrange
        Func<ValueTask<int>> factory = async () =>
        {
            await Task.Delay(50);
            return 42;
        };
        var lazy = new AsyncLazy<int>(factory);

        // Act
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
    }

    [Test]
    public async ValueTask GetTask_ValueTaskSynchronousCompletion_OptimizesCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return new ValueTask<int>(42); // Synchronous completion
        });

        // Act
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask GetTask_ValueTaskAsynchronousCompletion_HandlesCorrectly(CancellationToken cancellationToken)
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
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Test]
    public async ValueTask Reset_MultipleTimes_WorksCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        var callCount = 0;
        var lazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        // Act
        await lazy.GetTask(cancellationToken: cancellationToken);
        lazy.Reset();
        await lazy.GetTask(cancellationToken: cancellationToken);
        lazy.Reset();
        int result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be(42);
        callCount.Should().Be(3);
    }

    [Test]
    public async ValueTask GetTask_AfterReset_CreatesNewTask(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int>(() => Task.FromResult(42));
        Task<int> task1 = lazy.GetTask(cancellationToken: cancellationToken);
        await task1;

        // Act
        lazy.Reset();
        Task<int> task2 = lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        task2.Should().NotBeSameAs(task1);
        (await task2).Should().Be(42);
    }

    [Test]
    public async ValueTask GetTask_WithStringValue_WorksCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<string>(() => Task.FromResult("test"));

        // Act
        string result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().Be("test");
    }

    [Test]
    public async ValueTask GetTask_WithNullableValue_WorksCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int?>(() => Task.FromResult<int?>(null));

        // Act
        int? result = await lazy.GetTask(cancellationToken: cancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async ValueTask TryGetCompletedSuccessfully_WithNullableValue_WorksCorrectly(CancellationToken cancellationToken)
    {
        // Arrange
        var lazy = new AsyncLazy<int?>(() => Task.FromResult<int?>(null));

        // Act
        await lazy.GetTask(cancellationToken: cancellationToken);
        bool success = lazy.TryGetCompletedSuccessfully(out int? value);

        // Assert
        success.Should().BeTrue();
        value.Should().BeNull();
    }

    [Test]
    public async ValueTask GetTask_ConcurrentCallsAfterReset_OnlyCallsFactoryOnce(CancellationToken cancellationToken)
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

        await lazy.GetTask(cancellationToken: cancellationToken);
        lazy.Reset();

        // Act
        var tasks = new Task<int>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = lazy.GetTask(cancellationToken: cancellationToken);
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

    [Test]
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

    [Test]
    public async ValueTask GetTask_ValueTaskFactoryWithException_HandlesException(CancellationToken cancellationToken)
    {
        // Arrange
        var exception = new InvalidOperationException("Test");
        Func<ValueTask<int>> factory = () => new ValueTask<int>(Task.FromException<int>(exception));
        var lazy = new AsyncLazy<int>(factory);

        // Act & Assert
        Func<Task<int>> act = async () => await lazy.GetTask(cancellationToken: cancellationToken);
        ExceptionAssertions<InvalidOperationException>? ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("Test");
    }

    [Test]
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
