namespace ChaosPrototype.Core;

public sealed class RavenTurnRules
{
    private int _turn = -1;
    public int Triples { get; private set; }
    public bool ConnectionReady { get; private set; }
    public void EnterTurn(int turn)
    {
        if (_turn == turn) return;
        _turn = turn;
        Triples = 0;
        ConnectionReady = false;
    }
    public void ArmConnection() => ConnectionReady = true;
    public int RecordTriple()
    {
        Triples++;
        int block = ConnectionReady ? 7 : 0;
        ConnectionReady = false;
        return block;
    }
    public int IceBonus => Math.Min(Triples, 2) * 6;

    public static T[] MoveToRight<T>(IReadOnlyList<T> hand, T selected) where T : class =>
        hand.Any(c => ReferenceEquals(c, selected)) ? hand.Where(c => !ReferenceEquals(c, selected)).Append(selected).ToArray() : hand.ToArray();

    public static T[] Gather<T>(IReadOnlyList<T> hand, SignalColor selected, Func<T, SignalColor> color) =>
        hand.Where(c => color(c) != selected).Concat(hand.Where(c => color(c) == selected)).ToArray();
}
