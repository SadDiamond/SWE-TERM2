using UnityEngine;

public enum GrappleMassClass
{
    Light,
    Heavy
}

public interface IGrappleMassTarget
{
    GrappleMassClass GrappleMassClass { get; }
    bool ApplyGrapplePull(Vector3 pullTargetPoint, Vector3 pullDirection, float pullSpeed, float deltaTime);
}
