using ArbitrageBot.Application.Services;
using ArbitrageBot.Domain.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArbitrageBot.Tests;

public class CircuitBreakerTests
{
    private CircuitBreaker CreateBreaker(int window = 5, int maxLosses = 3, int cooldown = 30)
    {
        var opts = Options.Create(new CircuitBreakerOptions
        {
            WindowSize = window,
            MaxLossesBeforeOpen = maxLosses,
            CooldownSeconds = cooldown
        });
        return new CircuitBreaker(opts);
    }

    [Fact]
    public void IsOpen_StartsClosed()
    {
        var cb = CreateBreaker();
        Assert.False(cb.IsOpen);
    }

    [Fact]
    public void RecordTrade_SingleLoss_DoesNotOpen()
    {
        var cb = CreateBreaker();
        cb.RecordTrade(false);
        Assert.False(cb.IsOpen);
    }

    [Fact]
    public void RecordTrade_ThreeConsecutiveLosses_OpensCircuit()
    {
        var cb = CreateBreaker(window: 5, maxLosses: 3, cooldown: 30);

        cb.RecordTrade(false); // loss 1
        cb.RecordTrade(true);  // profit
        cb.RecordTrade(false); // loss 2
        cb.RecordTrade(false); // loss 3
        cb.RecordTrade(false); // loss 4 → 3 de 5 = OPEN

        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void RecordTrade_ThreeLossesInWindow_OpensCircuit()
    {
        var cb = CreateBreaker(window: 5, maxLosses: 3, cooldown: 30);

        // Llenar window con 3 losses de 5
        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(true);
        cb.RecordTrade(true);

        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void RecordTrade_SixteenTrades_WindowSlidesCorrectly()
    {
        var cb = CreateBreaker(window: 5, maxLosses: 3, cooldown: 30);

        // Llenar con trades buenos
        for (int i = 0; i < 10; i++) cb.RecordTrade(true);
        Assert.False(cb.IsOpen);

        // Ahora 3 losses seguidos
        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(false);
        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void GetState_ReflectsCurrentState()
    {
        var cb = CreateBreaker(window: 5, maxLosses: 3, cooldown: 30);

        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(true);
        cb.RecordTrade(true);

        var state = cb.GetState();
        Assert.True(state.IsOpen);
        Assert.Equal(3, state.ConsecutiveLosses);
        Assert.Equal(5, state.RecentTradesCount);
        Assert.NotNull(state.OpenedAt);
        Assert.NotNull(state.ClosedAt);
    }
}
