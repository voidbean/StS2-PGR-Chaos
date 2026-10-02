namespace ChaosPrototype.Gameplay;

public sealed partial class ChaosCharacter
{
    private static string CharacterArt => $"res://ChaosPrototype/character/{UI.CharacterArtConfig.ActiveSkin}/";

    public override string CustomVisualPath => CharacterArt + "combat.tscn";
    public override string CustomCharacterSelectBg => CharacterArt + "select.tscn";
    public override string CustomCharacterSelectIconPath => CharacterArt + "portrait.png";
    public override string CustomCharacterSelectLockedIconPath => CharacterArt + "portrait.png";
    public override string CustomIconPath => CharacterArt + "icon.tscn";
    public override string CustomIconTexturePath => CharacterArt + "portrait.png";
    public override string CustomIconOutlineTexturePath => CharacterArt + "portrait.png";
    public override string CustomMapMarkerPath => CharacterArt + "portrait.png";
    public override string CustomRestSiteAnimPath => CharacterArt + "rest.tscn";
    public override string CustomMerchantAnimPath => CharacterArt + "merchant.tscn";
}
