using BaseLib.Abstracts;
using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static partial class HyperrealChecks
{
    private static readonly List<(Type Power, int Count)> WorldlineSelections = [];
    private static int AbyssChoice, AbyssPrompts;
    private static Action? AfterAbyssChoice;
    private static bool SelectWorldline(Player player, CardSelectorPrefs prefs, AbstractModel source, ref Task<IEnumerable<CardModel>> __result)
    {
        if (prefs.MinSelect != prefs.MaxSelect || prefs.Cancelable || Draws.Count == 0)
            throw new Exception("worldline choice must follow draw and require the exact available count");
        WorldlineSelections.Add((source.GetType(), prefs.MinSelect));
        __result = Task.FromResult<IEnumerable<CardModel>>(player.PlayerCombatState!.Hand.Cards.TakeLast(prefs.MinSelect).ToArray());
        return false;
    }
    private static bool DiscardWorldline(IEnumerable<CardModel> cards, ref Task __result)
    {
        foreach (var card in cards) { card.Pile!.RemoveInternal(card, true); card.Owner.PlayerCombatState!.DiscardPile.AddInternal(card, silent: true); }
        __result = Task.CompletedTask; return false;
    }
    private static bool ExhaustWorldline(CardModel card, ref Task __result)
    {
        card.Pile!.RemoveInternal(card, true); card.Owner.PlayerCombatState!.ExhaustPile.AddInternal(card, silent: true);
        __result = Task.CompletedTask; return false;
    }
    private static bool ModifyWorldline(PowerModel power, decimal offset, ref Task<int> __result)
    {
        power.SetAmount(power.Amount + (int)offset, true);
        __result = Task.FromResult(power.Amount); return false;
    }
    private static bool SelectAbyss(IReadOnlyList<CardModel> cards, bool canSkip, ref Task<CardModel?> __result)
    {
        if (canSkip || cards.Count != 2 || cards[0] is not MyriadCalamitiesChoice || cards[1] is not SeveranceChoice)
            throw new Exception("abyss requires exactly two mandatory mode options");
        AbyssPrompts++; AfterAbyssChoice?.Invoke();
        __result = Task.FromResult(AbyssChoice < 0 ? null : cards[AbyssChoice]); return false;
    }

    private static async Task RunInverseCrown(Action<bool, string> check, Player player, CombatState combat, Creature target, List<PowerModel> powers, Harmony harmony)
    {
        var state = player.PlayerCombatState!;
        AbstractModel[] models = [new Crown(), new Atheism(), new LightlessAbyss(), new CrownPower(), new AtheismPower(), new MyriadCalamitiesChoice(), new SeveranceChoice(), new ChaosCardPool()];
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        foreach (var model in models) table[ModelDb.GetId(model.GetType())] = model;
        var choices = models.OfType<AbyssModeChoice>().ToArray();
        check(choices.All(c => !c.ShouldShowInCardLibrary && c.Pool is ChaosCardPool && !typeof(ChaosCard).IsAssignableFrom(c.GetType())), "screen modes are not playable reward/debug/library cards");
        var poolModels = (System.Collections.IDictionary)AccessTools.Field(typeof(MegaCrit.Sts2.Core.Modding.ModHelper), "_moddedContentForPools").GetValue(null)!;
        var entry = poolModels[typeof(ChaosCardPool)]!;
        var registered = (List<Type>)AccessTools.Field(entry.GetType(), "modelsToAdd").GetValue(entry)!;
        check(registered.Contains(typeof(LightlessAbyss)) && choices.All(c => !registered.Contains(c.GetType())), "ultimate is registered but screen modes are excluded from reward pool");
        harmony.Unpatch(AccessTools.Method(typeof(CardSelectCmd), "FromHand"), AccessTools.Method(typeof(HyperrealChecks), nameof(ChooseHand)));
        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromHand"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(SelectWorldline)));
        harmony.Patch(AccessTools.Method(typeof(CardCmd), "Discard", [typeof(PlayerChoiceContext), typeof(IEnumerable<CardModel>)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(DiscardWorldline)));
        harmony.Patch(AccessTools.Method(typeof(CardCmd), "Exhaust"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(ExhaustWorldline)));
        harmony.Patch(AccessTools.Method(typeof(PowerCmd), "ModifyAmount"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(ModifyWorldline)));
        harmony.Unpatch(AccessTools.Method(typeof(CardSelectCmd), "FromChooseACardScreen"), AccessTools.Method(typeof(HyperrealChecks), nameof(ChooseYellow)));
        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromChooseACardScreen"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(SelectAbyss)));
        harmony.Unpatch(AccessTools.Method(typeof(AttackCommand), "Execute"), AccessTools.Method(typeof(HyperrealChecks), nameof(ExpansionAttack)));
        harmony.Patch(AccessTools.Method(typeof(AttackCommand), "Execute"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(CaptureAttack)));
        void Reset()
        {
            powers.Clear(); Draws.Clear(); Attacks.Clear(); WorldlineSelections.Clear(); HpPayments = 0; AbyssPrompts = 0; AfterAbyssChoice = null;
            foreach (var pile in state.AllPiles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card, true);
            AccessTools.Property(typeof(PlayerCombatState), "TurnNumber").SetValue(state, 1);
            AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 70);
            AccessTools.Property(typeof(Creature), "Block").SetValue(player.Creature, 20);
            AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, 100);
        }
        T Card<T>() where T : CardModel => combat.CreateCard<T>(player);
        void Supply(int count) { for (int i = 0; i < count; i++) state.DrawPile.AddInternal(Card<RedSignal>(), silent: true); }
        async Task StartTurn(bool next = true)
        {
            if (next) state.IncrementTurnNumber();
            foreach (var power in powers.ToArray()) await power.AfterPlayerTurnStart(null!, player);
        }
        Reset();
        var crown = Card<Crown>(); var atheism = Card<Atheism>(); var burst = Card<LightlessAbyss>();
        check(crown.Type == CardType.Power && atheism.Type == CardType.Power && crown.Rarity == CardRarity.Uncommon && atheism.Rarity == CardRarity.Rare, "confirmed worldline ability types and rarities");
        check(crown.EnergyCost.Canonical == 1 && atheism.EnergyCost.Canonical == 2 && burst.EnergyCost.Canonical == 0 && CustomResources<Charge>.CanonicalCost(ModelDb.Card<LightlessAbyss>()) == 4, "confirmed energy and shared charge costs");
        var crownPlus = Card<Crown>(); crownPlus.UpgradeInternal();
        var atheismPlus = Card<Atheism>(); atheismPlus.UpgradeInternal();
        check(crownPlus.DynamicVars["Cycle"].IntValue == 2 && crownPlus.EnergyCost.GetWithModifiers(CostModifiers.All) == 1 && atheismPlus.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "crown upgrades cycle only; atheism upgrades cost only");
        await Invoke(crown, "OnPlay", null, Play(crown));
        await Invoke(atheism, "OnPlay", null, Play(atheism));
        await StartTurn(false);
        check(Draws.Count == 0 && WorldlineSelections.Count == 0 && player.Creature.CurrentHp == 66 && player.Creature.Block == 20, "abilities have no immediate cycle and atheism pays four unblocked HP");
        Supply(6); await StartTurn();
        check(Draws.SequenceEqual(new[] { 1m, 1m }) && WorldlineSelections.Select(x => x.Power).SequenceEqual(new[] { typeof(CrownPower), typeof(AtheismPower) }) && state.DiscardPile.Cards.Count == 1 && state.ExhaustPile.Cards.Count == 1, "both abilities resolve in application order using distinct discard and exhaust commands");
        await StartTurn(false);
        check(Draws.Count == 2 && player.Creature.CurrentHp == 66, "one cycle per own turn with no recurring HP payment");
        await Invoke(crownPlus, "OnPlay", null, Play(crownPlus));
        await Invoke(crown, "OnPlay", null, Play(crown));
        await Invoke(atheismPlus, "OnPlay", null, Play(atheismPlus));
        check(powers.Count == 2 && powers[0] is CrownPower { Amount: 2 } && player.Creature.CurrentHp == 62 && HpPayments == 2, "repeated abilities do not stack or reorder; upgraded crown wins and atheism still pays");
        await StartTurn();
        check(Draws.TakeLast(2).SequenceEqual(new[] { 2m, 1m }) && WorldlineSelections[^2].Count == 2, "crown replacement does not postpone next turn and discards exactly two");
        Reset();
        await Invoke(atheismPlus, "OnPlay", null, Play(atheismPlus)); await Invoke(crown, "OnPlay", null, Play(crown));
        Supply(2); await StartTurn();
        check(WorldlineSelections.Select(x => x.Power).SequenceEqual(new[] { typeof(AtheismPower), typeof(CrownPower) }), "reverse application reverses cycle order");
        Reset();
        await Invoke(crownPlus, "OnPlay", null, Play(crownPlus));
        state.Hand.AddInternal(Card<BlueSignal>(), silent: true);
        await StartTurn();
        check(WorldlineSelections.Single().Count == 1 && state.Hand.Cards.Count == 0 && state.DiscardPile.Cards.Count == 1, "insufficient draw and hand require only available cards");
        await StartTurn();
        check(WorldlineSelections.Count == 1, "empty hand does not open an impossible selection");
        Reset();
        await Invoke(atheism, "OnPlay", null, Play(atheism));
        for (int i = 0; i < CardPile.MaxCardsInHand; i++) state.Hand.AddInternal(Card<RedSignal>(), silent: true);
        Supply(1); await StartTurn();
        check(state.Hand.Cards.Count == CardPile.MaxCardsInHand - 1 && state.DrawPile.Cards.Count == 1 && state.ExhaustPile.Cards.Count == 1, "full hand still pays exhaust after blocked draw");
        Reset();
        await Invoke(crown, "OnPlay", null, Play(crown)); Supply(1);
        var otherPlayer = (Player)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Player));
        await powers[0].AfterPlayerTurnStart(null!, otherPlayer);
        check(Draws.Count == 0, "another player's turn does not trigger the ability");
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 4);
        await Invoke(atheism, "OnPlay", null, Play(atheism)); await StartTurn();
        check(player.Creature.CurrentHp == 0 && powers.All(p => p is not AtheismPower) && Draws.Count == 0, "lethal activation pays HP without applying a power or continuing dead-owner cycles");
        Reset();
        check(burst.PreventAutoPlay && burst.CanonicalKeywords.Contains(CardKeyword.Retain) && burst.MaxUpgradeLevel == 0, "abyss keeps shared ultimate retain and autoplay restrictions without invented upgrade");
        await Invoke(burst, "OnPlay", null, Play(burst, target, auto: true));
        await Invoke(burst, "OnPlay", null, Play(burst));
        check(AbyssPrompts == 0 && Attacks.Count == 0, "autoplay and missing target never prompt or attack");
        for (int mode = 0; mode < 2; mode++)
        {
            AbyssChoice = mode; Attacks.Clear(); AbyssPrompts = 0;
            await Invoke(burst, "OnPlay", null, Play(burst, target));
            await Invoke(burst, "OnPlay", null, new CardPlay { Card = burst, Target = target, IsAutoPlay = false, PlayIndex = 1, PlayCount = 2, ResultPile = PileType.Discard, Resources = default });
            check(AbyssPrompts == 1 && Attacks.Count == 2 && Attacks.All(a => a == (mode == 0 ? 36m : 6m, mode == 0 ? 1 : 5, target, ValueProp.Move)), "each mode uses powered same-target attacks and replay inherits selection");
        }
        AbyssChoice = -1; Attacks.Clear();
        await Invoke(burst, "OnPlay", null, Play(burst, target));
        check(Attacks.Count == 0, "aborted choice does not reuse previous mode");
        AbyssChoice = 0;
        AfterAbyssChoice = () => AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, 0);
        await Invoke(burst, "OnPlay", null, Play(burst, target));
        check(Attacks.Count == 0, "target death while choosing cancels attack");
        Reset();
    }
}
