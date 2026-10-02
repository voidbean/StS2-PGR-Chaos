using BaseLib.Config;
using Godot;

namespace ChaosPrototype.UI;

public sealed class CharacterArtConfig : ModConfig
{
    public static bool UseFusionSkin { get; set; }

    // Freeze resource paths for this process: the game's scene cache and BaseLib's
    // scene conversions are registered before play. Switching mid-run would mix skins.
    [ConfigIgnore]
    public static string ActiveSkin { get; private set; } = "normal";

    internal static void Register()
    {
        var config = new CharacterArtConfig();
        ActiveSkin = UseFusionSkin ? "fusion" : "normal";
        ModConfigRegistry.Register("ChaosPrototype", config);
    }

    public override void SetupConfigUI(Control optionContainer)
    {
        optionContainer.AddChild(new Label { Text = "卡俄斯 · 外观皮肤" });
        var choice = new OptionButton { CustomMinimumSize = new Vector2(420, 60) };
        choice.AddItem("通常形态 · 黑红裙装");
        choice.AddItem("融合形态 · 零号代行者");
        choice.Select(UseFusionSkin ? 1 : 0);
        optionContainer.AddChild(choice);
        optionContainer.AddChild(new Label
        {
            Text = "保存后重启游戏生效。两套皮肤仅改变外观，玩法与存档角色相同。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 80)
        });
        choice.ItemSelected += index =>
        {
            UseFusionSkin = index == 1;
            Changed();
            Save();
        };
    }
}
