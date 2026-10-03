using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

static partial class HyperrealChecks
{
    private static readonly List<decimal> EnergyGains = [];
    private static int EverydayRoll;
    private static int EverydayRolls;
    private static RunRngSet EverydayRng = new("everyday-card-test");
    private static bool SavedEverydayRng(ref RunRngSet __result) { __result = EverydayRng; return false; }
    private static bool RollEveryday(int maxExclusive, ref int __result)
    {
        if (maxExclusive != 10) throw new Exception("Expected ten equiprobable outcomes");
        EverydayRolls++; __result = EverydayRoll; return false;
    }
    private static bool GainEverydayEnergy(decimal amount, ref Task __result)
    {
        EnergyGains.Add(amount); __result = Task.CompletedTask; return false;
    }

    private static async Task RunEveryday(Action<bool, string> check, Player player, CombatState combat, Creature target, List<PowerModel> powers, Harmony harmony)
    {
        ChaosCard[] cards = [new LuciasCooking(), new SeventyPercent(), new AllOutTogether()];
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        foreach (var model in cards.Cast<AbstractModel>().Append(new Dazed())) table[ModelDb.GetId(model.GetType())] = model;
        check(cards.All(c => c is not SignalCard && c.EnergyCost.Canonical == 1 && c.MaxUpgradeLevel == 0), "three one-cost non-signal rewards without upgrades");
        check(cards.Select(c => c.Rarity).SequenceEqual(new[] { CardRarity.Uncommon, CardRarity.Common, CardRarity.Uncommon }), "everyday rarity distribution");
        check(cards[0].CanonicalKeywords.Contains(CardKeyword.Exhaust) && cards.Skip(1).All(c => !c.CanonicalKeywords.Any()), "only cooking exhausts");
        harmony.Unpatch(AccessTools.Method(typeof(PlayerCmd), "GainEnergy"), AccessTools.Method(typeof(HyperrealChecks), nameof(GainEnergy)));
        harmony.Patch(AccessTools.Method(typeof(PlayerCmd), "GainEnergy"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(GainEverydayEnergy)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(NullRunState), "Rng"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(SavedEverydayRng)));
        var rngMethod = AccessTools.Method(typeof(Rng), "NextInt", [typeof(int)]);
        harmony.Patch(rngMethod, prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(RollEveryday)));
        var state = player.PlayerCombatState!;
        void Reset()
        {
            powers.Clear(); Blocks.Clear(); Draws.Clear(); Generated.Clear(); Destinations.Clear(); EnergyGains.Clear(); EverydayRolls = 0;
            foreach (var pile in state.AllPiles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card, true);
        }
        T Card<T>() where T : CardModel => combat.CreateCard<T>(player);
        Reset();
        var meal = Card<LuciasCooking>();
        await Invoke(meal, "OnPlay", null, Play(meal));
        check(EnergyGains.SequenceEqual(new[] { 2m }) && Generated.Single() is Dazed && Destinations.Single().Pile == PileType.Discard && state.Hand.Cards.Count == 0, "cooking grants energy and delays one native dazed into discard");
        check(Generated[0].CanonicalKeywords.Contains(CardKeyword.Ethereal), "cooking uses native ethereal dazed rather than a permanent curse");
        int drawBranches = 0;
        for (int roll = 0; roll < 10; roll++)
        {
            Reset(); EverydayRoll = roll;
            for (int i = 0; i < 3; i++) state.DrawPile.AddInternal(Card<RedSignal>(), silent: true);
            var chance = Card<SeventyPercent>();
            await Invoke(chance, "OnPlay", null, Play(chance));
            bool draws = Draws.Count == 1;
            if (draws) drawBranches++;
            check(EverydayRolls == 1 && (draws ? Blocks.SequenceEqual(new[] { 7m }) && Draws.Single() == 2 && state.Hand.Cards.Count == 2 : Blocks.SequenceEqual(new[] { 7m, 7m }) && state.Hand.Cards.Count == 0), "each roll gives exactly one random branch after base block");
        }
        check(drawBranches == 7, "exactly seventy percent of possible outcomes draw");
        Reset(); EverydayRoll = 0;
        for (int i = 0; i < CardPile.MaxCardsInHand; i++) state.Hand.AddInternal(Card<RedSignal>(), silent: true);
        var full = Card<SeventyPercent>(); await Invoke(full, "OnPlay", null, Play(full));
        check(Draws.Single() == 2 && Blocks.Count == 1 && state.Hand.Cards.Count == CardPile.MaxCardsInHand, "full hand does not reroll or substitute the consolation block");
        harmony.Unpatch(rngMethod, AccessTools.Method(typeof(HyperrealChecks), nameof(RollEveryday)));
        var outcomes = new List<bool[]>();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            EverydayRng = new RunRngSet("everyday-card-test");
            var sequence = new List<bool>();
            for (int i = 0; i < 20; i++)
            {
                Reset(); var chance = Card<SeventyPercent>();
                await Invoke(chance, "OnPlay", null, Play(chance)); sequence.Add(Draws.Count > 0);
            }
            outcomes.Add(sequence.ToArray());
            check(EverydayRng.CombatCardSelection.Counter == 20, "one saved RNG draw per resolution");
        }
        check(outcomes[0].SequenceEqual(outcomes[1]), "same seed and actions reproduce chance outcomes");
        Reset();
        var other = new Creature(null!, 100, 100) { CombatState = combat };
        var dead = new Creature(null!, 0, 100) { CombatState = combat };
        foreach (var enemy in new[] { target, other }) ((List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(enemy)!).Clear();
        ExpansionTargets = [target, other, dead];
        var support = Card<AllOutTogether>(); await Invoke(support, "OnPlay", null, Play(support));
        check(new[] { target, other }.All(c => c.GetPower<VulnerablePower>()?.Amount == 2) && dead.GetPower<VulnerablePower>() == null && player.Creature.GetPower<VulnerablePower>() == null, "support applies two native vulnerable to each live enemy only");
        Reset();
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 0);
        foreach (var card in new ChaosCard[] { Card<LuciasCooking>(), Card<SeventyPercent>(), Card<AllOutTogether>() }) await Invoke(card, "OnPlay", null, Play(card));
        check(EnergyGains.Count == 0 && Generated.Count == 0 && Blocks.Count == 0 && new[] { target, other }.All(c => c.Powers.Count() == 1), "dead owner cannot gain resources or apply more support");
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 70);
        ExpansionTargets = [];
    }
}
