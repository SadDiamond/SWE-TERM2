using NUnit.Framework;

public class CybergrindRulesTests
{
    [Test]
    public void EnemyHealthMultiplier_StartsAtOneAndIncreasesWithFloor()
    {
        Assert.That(CybergrindRules.GetEnemyHealthMultiplier(1), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(CybergrindRules.GetEnemyHealthMultiplier(10), Is.GreaterThan(CybergrindRules.GetEnemyHealthMultiplier(2)));
    }

    [Test]
    public void EnemyCountBonus_StartsAtZeroAndIncreasesWithFloor()
    {
        Assert.That(CybergrindRules.GetEnemyCountBonus(1), Is.EqualTo(0));
        Assert.That(CybergrindRules.GetEnemyCountBonus(10), Is.GreaterThan(CybergrindRules.GetEnemyCountBonus(2)));
    }

    [Test]
    public void WeaponDamage_AppliesEveryMultiplier()
    {
        float damage = CybergrindRules.CalculateWeaponDamage(100f, 0.88f, 1.1f, 1.16f, 1.08f);
        Assert.That(damage, Is.EqualTo(121.27104f).Within(0.0001f));
    }

    [Test]
    public void ShopLock_ReflectsWhetherPurchaseWasMadeThisFloor()
    {
        Assert.That(CybergrindRules.IsShopPurchaseLocked(false), Is.False);
        Assert.That(CybergrindRules.IsShopPurchaseLocked(true), Is.True);
    }

    [Test]
    public void TimerNormalized_ClampsToValidHudRange()
    {
        Assert.That(CybergrindRules.GetTimerNormalized(30f, 60f), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(CybergrindRules.GetTimerNormalized(90f, 60f), Is.EqualTo(1f));
        Assert.That(CybergrindRules.GetTimerNormalized(10f, 0f), Is.EqualTo(0f));
    }

    [Test]
    public void TimerTick_StopsAtZero()
    {
        Assert.That(CybergrindRules.TickTimer(1f, 0.25f), Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(CybergrindRules.TickTimer(0.1f, 0.5f), Is.EqualTo(0f));
    }
}
