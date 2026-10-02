using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ChaosPrototype.Gameplay;

internal static class RavenRuntime
{
    private static readonly Dictionary<Player, RavenTurnRules> States = [];
    internal static RavenTurnRules Get(Player player)
    {
        if (!States.TryGetValue(player, out var state)) States[player] = state = new();
        state.EnterTurn(player.PlayerCombatState!.TurnNumber);
        return state;
    }
    internal static void Reset() => States.Clear();
}
