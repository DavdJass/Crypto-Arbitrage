using ArbitrageBot.Application.Services;
using ArbitrageBot.Domain.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArbitrageBot.Tests;

public class CircuitBreakerTests
{
    private CircuitBreaker CreateBreaker(int maxLosses = 3, int cooldown = 30)
    {
        var opts = Options.Create(new CircuitBreakerOptions
        {
            WindowSize = 5,
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
        var cb = CreateBreaker(maxLosses: 3, cooldown: 30);

        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(false);

        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void RecordTrade_LossesBrokenByProfit_DoesNotOpen()
    {
        var cb = CreateBreaker(maxLosses: 3, cooldown: 30);

        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(true);
        cb.RecordTrade(false);
        cb.RecordTrade(false);

        Assert.False(cb.IsOpen);
    }

    [Fact]
    public void RecordTrade_ProfitResetsConsecutiveStreak()
    {
        var cb = CreateBreaker(maxLosses: 3, cooldown: 30);

        for (int i = 0; i < 10; i++) cb.RecordTrade(true);
        Assert.False(cb.IsOpen);

        cb.RecordTrade(false);
        cb.RecordTrade(false);
        cb.RecordTrade(false);
        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void GetState_ReflectsConsecutiveLosses()
    {
        var cb = CreateBreaker(maxLosses: 3, cooldown: 30);

        cb.RecordTrade(false);
        cb.RecordTrade(false);

        var state = cb.GetState();
        Assert.False(state.IsOpen);
        Assert.Equal(2, state.LossCountInWindow);
        Assert.Equal(3, state.MaxLossesBeforeOpen);
    }
}
