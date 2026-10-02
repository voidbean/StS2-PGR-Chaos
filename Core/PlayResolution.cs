namespace ChaosPrototype.Core;

public readonly record struct Resolution(bool Enhanced, bool ConsumePair, bool ConsumeSupercompute);

// One outer play may execute OnPlay repeatedly. Costs belong to that outer play,
// while the enhanced effect is retained for its replay iterations.
public sealed class PlayResolution
{
    private bool _resolved;
    private bool _enhanced;
    public Resolution Resolve(bool natural, bool supercompute)
    {
        if (_resolved) return new(_enhanced, false, false);
        _resolved = true;
        _enhanced = natural || supercompute;
        return new(_enhanced, natural, supercompute);
    }
}
