using UnityEngine;

public static class CybergrindRules
{
    public static float GetEnemyHealthMultiplier(int floor)
    {
        return 1f + (0.14f * Mathf.Sqrt(Mathf.Max(0, floor - 1)));
    }

    public static int GetEnemyCountBonus(int floor)
    {
        return Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0, floor - 1)) * 1.25f);
    }

    public static float CalculateWeaponDamage(
        float baseDamage,
        float baseDamageMultiplier,
        float runDamageMultiplier,
        float passiveMultiplier = 1f,
        float abilityMultiplier = 1f)
    {
        return Mathf.Max(0f, baseDamage) *
               Mathf.Max(0f, baseDamageMultiplier) *
               Mathf.Max(0f, runDamageMultiplier) *
               Mathf.Max(0f, passiveMultiplier) *
               Mathf.Max(0f, abilityMultiplier);
    }

    public static bool IsShopPurchaseLocked(bool purchaseMadeThisFloor)
    {
        return purchaseMadeThisFloor;
    }

    public static float GetTimerNormalized(float remaining, float duration)
    {
        return duration <= 0.01f ? 0f : Mathf.Clamp01(remaining / duration);
    }

    public static float TickTimer(float remaining, float deltaTime)
    {
        return Mathf.Max(0f, remaining - Mathf.Max(0f, deltaTime));
    }
}
