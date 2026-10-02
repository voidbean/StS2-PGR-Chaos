using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace ChaosPrototype;

[ModInitializer(nameof(Initialize))]
public static class Main
{
    public static void Initialize()
    {
        UI.CharacterArtConfig.Register();
        new Harmony("ChaosPrototype").PatchAll(Assembly.GetExecutingAssembly());
        UI.PreviewPanel.Register();
    }
    public static string Art(string name) => $"res://ChaosPrototype/{name}.svg";
}
