using BaseLib.Abstracts;

namespace ChaosPrototype.Gameplay;

public sealed class Charge() : BasicCustomResource("ChaosPrototypeCharge", -1)
{
    public override bool ApplySharedModification => false;
    // The prototype's panel displays both resources without custom Godot scripts.
    public override void RegisterResourceVisuals<T>() { }
}

public sealed class Supercompute() : BasicCustomResource("ChaosPrototypeSupercompute", -1)
{
    public override bool ApplySharedModification => false;
    public override void RegisterResourceVisuals<T>() { }
}
