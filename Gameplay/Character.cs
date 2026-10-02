using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace ChaosPrototype.Gameplay;

public sealed partial class ChaosCharacter : PlaceholderCharacterModel
{
    public override Color NameColor => new("9bd4f5");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;
    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<RedSignal>(), ModelDb.Card<RedSignal>(), ModelDb.Card<RedSignal>(),
        ModelDb.Card<YellowSignal>(), ModelDb.Card<YellowSignal>(), ModelDb.Card<YellowSignal>(),
        ModelDb.Card<BlueSignal>(), ModelDb.Card<BlueSignal>(), ModelDb.Card<BlueSignal>(),
        ModelDb.Card<PerfectDodge>(), ModelDb.Card<Ultimate>()];
    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<SignalCore>()];
    public override CardPoolModel CardPool => ModelDb.CardPool<ChaosCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<ChaosRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<ChaosPotionPool>();
    public override List<(string, string)> Localization => new CharacterLoc(
        "卡俄斯（原型）", "卡俄斯", "排列红黄蓝信号球，完成相邻三消，积攒充能释放大招。\n静态立绘原型，数值仍在测试。仅用于单人测试。",
        "他", "他", "他的", "他的", "信号", "轮到你了。", "继续前进。", "信号恢复。", "补充资源。",
        "信号球", "相邻同色三张可三消。");
}
public sealed class ChaosCardPool : CustomCardPoolModel
{
    public override string Title => "ChaosPrototype";
    public override bool SeenByDefault => true;
    // Custom ability cards now supply the shop Power slot.
    protected override CardModel[] GenerateAllCards() => [];
    public override Color DeckEntryCardColor => new("9bd4f5");
    public override bool IsColorless => false;
    public override string BigEnergyIconPath => Main.Art("charge");
    public override string TextEnergyIconPath => Main.Art("charge");
}
public sealed class ChaosRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => new("9bd4f5");
    public override string BigEnergyIconPath => Main.Art("charge");
    public override string TextEnergyIconPath => Main.Art("charge");
}
public sealed class ChaosPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => new("9bd4f5");
    public override string BigEnergyIconPath => Main.Art("charge");
    public override string TextEnergyIconPath => Main.Art("charge");
}
[Pool(typeof(ChaosRelicPool))]
public sealed class SignalCore : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => Main.Art("core");
    protected override string PackedIconOutlinePath => Main.Art("core");
    protected override string BigIconPath => Main.Art("core");
    public override List<(string, string)> Localization => new RelicLoc("信号核心", "手动打出相邻同色信号球：优先三消，否则双消；弃置其余球，强化主牌。四连以上选包含主牌的最靠左三张。非信号球靠左，信号球靠右，球的顺序不变。", "原型组件，外观待定。");
}
