using BaseLib.Abstracts;
using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static partial class HyperrealChecks
{
    private static int HpPayments;
    private static bool PayHp(Creature target, decimal amount, ValueProp props, ref Task<IEnumerable<DamageResult>> __result)
    {
        if (props != (ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move)) throw new Exception("burnout HP payment must bypass block and attack buffs");
        HpPayments++;
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, target.CurrentHp - (int)amount);
        __result = Task.FromResult<IEnumerable<DamageResult>>([]);
        return false;
    }
    private static bool GainEnergy(decimal amount, Player player, ref Task __result)
    {
        player.PlayerCombatState!.Energy += (int)amount;
        __result = Task.CompletedTask;
        return false;
    }
    private static async Task RunOathflame(Action<bool, string> check, Player player, CombatState combat, Creature target, List<PowerModel> powers, Harmony harmony)
    {
        var state = player.PlayerCombatState!;
        foreach (var pile in state.AllPiles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card, true);
        powers.Clear(); Generated.Clear(); Destinations.Clear(); Attacks.Clear(); Collapses.Clear();
        ChaosCard[] cards = [new MoltenQuench(), new CoordinatedSlash(), new EclipseDawn(), new BlazingFeathers(), new SoaringContinuation(), new FlowingThunder(), new OmegaCore(), new CoreBurnout()];
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        foreach (var model in cards.Cast<AbstractModel>().Concat([new OathflameFlightPower(), new MoltenChasePower(), new ContinuationCleanupPower(), new OmegaCorePower()])) table[ModelDb.GetId(model.GetType())] = model;
        check(cards.Count(c => c.Rarity == CardRarity.Token) == 2 && cards.Count(c => c.Rarity == CardRarity.Rare) == 5, "oathflame six reward cards and two tokens");
        check(cards.All(c => c.MaxUpgradeLevel == (c.Rarity == CardRarity.Token ? 0 : 1)), "oathflame permanent cards upgrade and tokens stay unchanged");
        var upgraded = cards[0].ToMutable(); upgraded.UpgradeInternal();
        check(upgraded.EnergyCost.Canonical == 2 && upgraded.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "quench upgrade reduces energy to one");
        var upgradedCore = cards[6].ToMutable(); upgradedCore.UpgradeInternal();
        check(upgradedCore.Keywords.Contains(CardKeyword.Retain) && !cards[6].CanonicalKeywords.Contains(CardKeyword.Retain) && CustomResources<Charge>.CanonicalCost(cards[6]) == 3, "core upgrade adds retain without changing charge");
        check(cards.Where(c => c is BurstCard or OmegaCore or CoreBurnout).All(c => c.PreventAutoPlay), "all charged bursts and core lifecycle refuse autoplay");
        var resourceField = AccessTools.Property(typeof(CustomResources<Charge>), "Resource").GetValue(null)!;
        AccessTools.Method(resourceField.GetType(), "Set").Invoke(resourceField, [state, new Charge { Owner = player }]);
        harmony.Patch(AccessTools.Method(typeof(PlayerCmd), "GainEnergy"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(GainEnergy)));
        harmony.Patch(AccessTools.Method(typeof(CreatureCmd), "Damage", [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(PayHp)));
        T Card<T>() where T : CardModel { var card = combat.CreateCard<T>(player); state.PlayPile.AddInternal(card, silent: true); return card; }
        var quench = Card<MoltenQuench>();
        await Invoke(quench, "OnPlay", null, Play(quench));
        var flight = player.Creature.GetPower<OathflameFlightPower>()!;
        var chase = player.Creature.GetPower<MoltenChasePower>()!;
        check(flight != null && chase.Remaining == 2 && CustomResources<Supercompute>.Get(state).Amount == 1, "quench grants flight supercompute and two chases");
        check(flight!.ModifyDamageMultiplicative(player.Creature, 20, ValueProp.Move, target, null) == 0.5m && flight.ModifyDamageMultiplicative(player.Creature, 10, ValueProp.Unpowered | ValueProp.Unblockable, null, null) == 1m, "flight halves attacks but not HP loss");
        await Invoke(chase, "AfterTriple", new object?[] { null });
        await Invoke(quench, "OnPlay", null, Play(quench));
        check(chase.Remaining == 2 && powers.OfType<OathflameFlightPower>().Count() == 1, "repeat quench refreshes two chases without stacking flight");
        var eclipse = Card<EclipseDawn>();
        await Invoke(eclipse, "OnPlay", null, Play(eclipse));
        check(Attacks.Last().Damage == 32 && player.Creature.GetPower<OathflameFlightPower>() == null && chase.Remaining == 2, "flying eclipse deals 32 lands and preserves chases");
        await Invoke(eclipse, "OnPlay", null, Play(eclipse));
        check(Attacks.Last().Damage == 24, "grounded eclipse deals 24");
        var slash = Card<CoordinatedSlash>(); await Invoke(slash, "OnPlay", null, Play(slash));
        check(Attacks.Last().Damage == 18 && player.Creature.GetPower<OathflameFlightPower>() != null, "coordinated slash attacks then flies");
        flight = player.Creature.GetPower<OathflameFlightPower>()!;
        await flight.BeforeSideTurnStart(null!, CombatSide.Enemy, [target], combat);
        check(player.Creature.GetPower<OathflameFlightPower>() != null, "flight survives enemy turn start");
        await flight.BeforeSideTurnStart(null!, CombatSide.Player, [player.Creature], combat);
        check(player.Creature.GetPower<OathflameFlightPower>() == null, "flight expires at owner's next turn start");
        var blue = Card<FlowingThunder>();
        await Invoke(blue, "Effect", null, Play(blue), 1);
        check(CustomResources<Charge>.Get(state).Amount == 0, "ordinary blue grants no charge");
        await Invoke(blue, "Effect", null, Play(blue), 3);
        check(CustomResources<Charge>.Get(state).Amount == 2, "triple blue grants two charge");
        var yellow = Card<BlazingFeathers>();
        await Invoke(yellow, "Effect", null, Play(yellow, target), 1);
        check(Attacks.Last().Damage == 6 && Generated.Count == 0, "normal yellow no continuation");
        await Invoke(yellow, "Effect", null, Play(yellow, target), 3);
        check(Attacks.Last().Damage == 10 && Generated.Single() is SoaringContinuation, "triple yellow creates continuation");
        var continuation = Generated.Single();
        await Invoke(continuation, "OnPlay", null, Play(continuation, target));
        check(Attacks.Last().Damage == 4 && CustomResources<Charge>.Get(state).Amount == 4, "continuation gives four damage two charge without flight requirement");
        state.Hand.RemoveInternal(continuation, true); state.ExhaustPile.AddInternal(continuation, silent: true);
        while (state.Hand.Cards.Count < CardPile.MaxCardsInHand) state.Hand.AddInternal(combat.CreateCard<RedSignal>(player), silent: true);
        await Invoke(yellow, "Effect", null, Play(yellow, target), 3);
        var overflow = Generated.Last();
        check(overflow.Pile == state.DiscardPile, "full-hand continuation requests normal discard fallback");
        var cleanup = player.Creature.GetPower<ContinuationCleanupPower>()!;
        await cleanup.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]);
        check(overflow.Pile == null && continuation.Pile == state.ExhaustPile, "continuation cleanup removes unused discard but preserves spent exhaust");
        Collapses.Clear();
        await Invoke(chase, "AfterTriple", new object?[] { null }); await Invoke(chase, "AfterTriple", new object?[] { null }); await Invoke(chase, "AfterTriple", new object?[] { null });
        check(chase.Remaining == 0 && Collapses.Count == 2 && Collapses.All(c => c == (4m, ValueProp.Unpowered)), "chase caps at two ability hits");
        await chase.AfterSideTurnEnd(null!, CombatSide.Player, [player.Creature]);
        check(player.Creature.GetPower<MoltenChasePower>() == null, "chases expire at turn end");
        var core = Card<OmegaCore>();
        Generated.Clear(); Destinations.Clear();
        await Invoke(core, "OnPlay", null, Play(core, auto: true));
        check(Generated.Count == 0, "core refuses auto startup");
        await Invoke(core, "OnPlay", null, Play(core)); await Invoke(core, "OnPlay", null, Play(core));
        check(Generated.Count == 1 && Generated[0] is CoreBurnout && Destinations.Single() == (PileType.Draw, CardPilePosition.Random), "core creates one burnout at random draw-pile position across repeats");
        var supply = player.Creature.GetPower<OmegaCorePower>()!;
        int energy = state.Energy; int charge = CustomResources<Charge>.Get(state).Amount;
        await supply.BeforeHandDraw(player, null!, combat);
        check(state.Energy == energy && CustomResources<Charge>.Get(state).Amount == charge, "core grants no startup resources");
        AccessTools.Property(typeof(PlayerCombatState), "TurnNumber").SetValue(state, 2);
        await supply.BeforeHandDraw(player, null!, combat); await supply.BeforeHandDraw(player, null!, combat);
        check(state.Energy == energy + 1 && CustomResources<Charge>.Get(state).Amount == charge + 1, "core supplies exactly once on next turn");
        powers.Remove(supply);
        await Invoke(core, "OnPlay", null, Play(core));
        check(Generated.Count == 1 && player.Creature.GetPower<OmegaCorePower>() != null, "externally removed core can restart without replacing burnout");
        var burnout = Card<CoreBurnout>();
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 10);
        await Invoke(burnout, "OnPlay", null, Play(burnout));
        check(HpPayments == 0, "burnout blocked at ten HP");
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 11);
        await Invoke(burnout, "OnPlay", null, Play(burnout, auto: true));
        check(HpPayments == 0, "burnout refuses autoplay");
        Attacks.Clear();
        await Invoke(burnout, "OnPlay", null, Play(burnout));
        await Invoke(burnout, "OnPlay", null, Play(burnout));
        await Invoke(Card<CoreBurnout>(), "OnPlay", null, Play(burnout));
        await Invoke(core, "OnPlay", null, Play(core));
        check(HpPayments == 1 && player.Creature.CurrentHp == 1 && Attacks.Single().Damage == 40 && player.Creature.GetPower<OmegaCorePower>() == null, "burnout pays once deals forty and permanently blocks replay copy and restart");
    }
}
