namespace ChaosPrototype.Core;

public enum SignalColor { None, Red, Yellow, Blue }

public static class SignalRules
{
    // Prototype tie-break: leftmost length-three window containing the played instance.
    public static T[] Auxiliary<T>(IReadOnlyList<T> hand, T played, Func<T, SignalColor> color) where T : class
    {
        int index = -1;
        for (int i = 0; i < hand.Count; i++)
            if (ReferenceEquals(hand[i], played)) { index = i; break; }
        if (index < 0 || color(played) == SignalColor.None) return [];
        for (int start = Math.Max(0, index - 2); start <= index && start + 2 < hand.Count; start++)
            if (Enumerable.Range(start, 3).All(i => color(hand[i]) == color(played)))
                return Enumerable.Range(start, 3).Where(i => i != index).Select(i => hand[i]).ToArray();
        for (int start = Math.Max(0, index - 1); start <= index && start + 1 < hand.Count; start++)
            if (color(hand[start]) == color(played) && color(hand[start + 1]) == color(played))
                return [hand[start == index ? start + 1 : start]];
        return [];
    }

    // Append within each section; never sort signals by color.
    public static int HandInsertIndex<T>(IReadOnlyList<T> hand, T incoming, Func<T, SignalColor> color) =>
        color(incoming) != SignalColor.None ? hand.Count : hand.TakeWhile(c => color(c) == SignalColor.None).Count();

    public static bool UnchangedAfterRemoval<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, T played) where T : class
    {
        var expected = before.Where(c => !ReferenceEquals(c, played)).ToArray();
        return expected.Length == after.Count && expected.Where((c, i) => !ReferenceEquals(c, after[i])).Any() == false;
    }
}
