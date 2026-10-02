namespace ChaosPrototype.Core;

public static class ResonanceRules
{
    public static bool IsTriple(int strength) => strength == 3;
    public static bool PerfectBlock(bool attack, int blocked, int unblocked) => attack && blocked > 0 && unblocked == 0;
    public static bool DeadlineDue(int appliedTurn, int currentTurn) => currentTurn > appliedTurn && (currentTurn - appliedTurn) % 2 == 1;
}
