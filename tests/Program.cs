using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using ChaosPrototype.Core;
using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using System.Reflection;

int checks = 0;
void Check(bool pass, string label) { checks++; if (!pass) throw new Exception(label); }
for (int n = 1; n <= 8; n++)
{
    for (int code = 0; code < 1 << (2 * n); code++)
    {
        var hand = Enumerable.Range(0, n).Select(i => new Token((SignalColor)((code >> (2 * i)) & 3))).ToArray();
        for (int i = 0; i < n; i++)
        {
            var pair = SignalRules.Auxiliary(hand, hand[i], c => c.Color);
            var start = Enumerable.Range(0, Math.Max(0, n - 2)).FirstOrDefault(s => s <= i && i <= s + 2 && hand[s].Color != SignalColor.None && hand[s].Color == hand[s+1].Color && hand[s].Color == hand[s+2].Color, -1);
            var expected = start < 0 ? [] : Enumerable.Range(start, 3).Where(k => k != i).Select(k => hand[k]).ToArray();
            Check(pair.Length == expected.Length && pair.Zip(expected).All(p => ReferenceEquals(p.First, p.Second)), "actual mod matching oracle");
        }
    }
}
var r = new Token(SignalColor.Red); var a = new Token(SignalColor.Red); var b = new Token(SignalColor.Red);
Check(SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { r, b }, a), "snapshot validates removal");
Check(!SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { b, r }, a), "snapshot rejects reorder");
Check(!SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { r }, a), "snapshot rejects missing auxiliary");
Check(!SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { r, b, new Token(SignalColor.None) }, a), "snapshot rejects draw");
Check(SignalRules.Auxiliary(new[] { r, a, b }, new Token(SignalColor.Red), c => c.Color).Length == 0, "same-color foreign instance rejected");
foreach (bool natural in new[] { false, true })
foreach (bool super in new[] { false, true })
{
    var play = new PlayResolution();
    var first = play.Resolve(natural, super);
    Check(first == new Resolution(natural || super, natural, super), "natural/super overlap truth table");
    var replay = play.Resolve(true, true);
    Check(replay == new Resolution(natural || super, false, false), "replay never consumes new pair/super");
}
var resource = new Charge(); resource.ModifyAmount(4); resource.StartOfTurnReset(null!, null!);
Check(resource.Amount == 4, "charge retention"); resource.PrepForCombat<Charge>(null!);
Check(resource.Amount == 0 && !resource.ApplySharedModification, "charge reset/shared-free exclusion");
var assembly = typeof(ChaosCharacter).Assembly;
Check(assembly.GetTypes().Count(t => !t.IsAbstract && typeof(ChaosCard).IsAssignableFrom(t)) == 5, "five card types");
Check(assembly.GetTypes().Count(t => !t.IsAbstract && typeof(SignalCard).IsAssignableFrom(t)) == 3, "three signal types");
var red = new RedSignal(); var yellow = new YellowSignal(); var blue = new BlueSignal();
var superCard = new SupercomputeCard(); var ultimate = new Ultimate();
Check(red.DynamicVars["Damage"].BaseValue == 6 && red.DynamicVars["TripleDamage"].BaseValue == 12, "red values");
Check(yellow.DynamicVars["Block"].BaseValue == 5 && yellow.DynamicVars["TripleBlock"].BaseValue == 10, "yellow values");
Check(blue.DynamicVars["Damage"].BaseValue == 3 && blue.DynamicVars["TripleDamage"].BaseValue == 6, "blue values");
Check(ultimate.CanonicalKeywords.Contains(CardKeyword.Retain) && CustomResources<Charge>.CanonicalCost(ultimate) == 3, "ultimate retain/cost");
Check(new ChaosCharacter().StartingHp == 70 && new SignalCore().Localization.Count == 3, "character and relic constructors");
// Populate only this test process's model table to exercise the real StartingDeck getter.
var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
var character = new ChaosCharacter(); var relic = new SignalCore();
foreach (var model in new AbstractModel[] { red, yellow, blue, superCard, ultimate, character, relic })
{
    var id = ModelDb.GetId(model.GetType());
    table[id] = model;
}
var deck = character.StartingDeck.ToArray();
Check(deck.Length == 11 && deck.Count(c => c is RedSignal) == 3 && deck.Count(c => c is YellowSignal) == 3 && deck.Count(c => c is BlueSignal) == 3, "11-card starting deck composition");
Check(deck.Count(c => c is Ultimate) == 1 && deck.Count(c => c is SupercomputeCard) == 1 && character.StartingRelics.Single() is SignalCore, "starter utility and relic");
// Apply real Harmony patches to the actual installed assembly without executing a combat.
// This catches bad overloads, target methods and injected parameter signatures.
var harmony = new Harmony("ChaosPrototype");
try
{
    ChaosPrototype.Main.Initialize();
    Check(Harmony.GetPatchInfo(typeof(CardPileCmd).GetMethod(nameof(CardPileCmd.AddDuringManualCardPlay))!)!.Owners.Contains("ChaosPrototype"), "capture patch installed");
    Check(Harmony.GetPatchInfo(typeof(CardCmd).GetMethod(nameof(CardCmd.AutoPlay))!)!.Owners.Contains("ChaosPrototype"), "autoplay patch installed");
    Check(Harmony.GetPatchInfo(typeof(CardModel).GetMethod(nameof(CardModel.TryManualPlay))!)!.Owners.Contains("ChaosPrototype"), "queue guard installed");
    Console.WriteLine("PASS actual Harmony patch installation (not in-game execution)");
}
finally { harmony.UnpatchAll("ChaosPrototype"); }
Console.WriteLine($"PASS {checks} checks against actual prototype code");
Console.WriteLine("Not covered: live Godot combat, card previews/animations, save/load, multiplayer.");
record Token(SignalColor Color);
