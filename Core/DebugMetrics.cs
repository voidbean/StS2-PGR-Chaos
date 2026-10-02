namespace ChaosPrototype.Core;

public sealed class DebugMetrics
{
    public int Single { get; private set; }
    public int Double { get; private set; }
    public int Triple { get; private set; }
    public int Supercompute { get; private set; }
    public int Interventions { get; private set; }

    public void Record(int consumed, bool supercompute)
    {
        if (consumed is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(consumed));
        if (consumed == 1) Single++;
        else if (consumed == 2) Double++;
        else Triple++;
        if (supercompute) Supercompute++;
    }

    public void MarkIntervention() => Interventions++;

    public string Summary => $"单消 {Single} · 双消 {Double} · 自然三消 {Triple}\n超算强化 {Supercompute}（可与自然消球重叠）\n调试干预 {Interventions} 次";
}
