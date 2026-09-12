using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Asyncs.Lazys.Tests;

public sealed class LazyRegressionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Synchronous_cancellation_is_cached_even_without_a_canceled_token(bool hasToken)
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken token = hasToken ? cancellation.Token : default;
        int calls = 0;
        var lazy = new AsyncLazy<int>((Func<Task<int>>)(() =>
        {
            calls++;
            throw new OperationCanceledException(token);
        }));
        Task<int> first = lazy.GetTask();
        Task<int> second = lazy.GetTask();
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(first.IsCanceled).IsTrue();
        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(async () => await first).Throws<OperationCanceledException>();
    }
}
