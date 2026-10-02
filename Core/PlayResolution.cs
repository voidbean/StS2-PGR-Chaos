namespace ChaosPrototype.Core;

public readonly record struct Resolution(int Strength, bool ConsumePair, bool ConsumeSupercompute);

// One outer play may execute OnPlay repeatedly. Costs belong to that outer play,
// while the enhanced effect is retained for its replay iterations.
public sealed class PlayResolution
{
    private bool _resolved;
    private int _strength;
    public Resolution Resolve(int naturalCount, bool supercompute)
    {
        if (_resolved) return new(_strength, false, false);
        _resolved = true;
        _strength = supercompute ? 3 : naturalCount;
        return new(_strength, naturalCount > 1, supercompute);
    }
}
