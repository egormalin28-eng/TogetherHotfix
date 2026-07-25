using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace CMS21TogetherHotfix
{
    public class Main : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            var h = new HarmonyLib.Harmony("com.together.hotfix.enginestand-lobby");
            h.PatchAll(Assembly.GetExecutingAssembly());
            MelonLogger.Msg("[TogetherHotfix] applied: EngineStandAngle read fix + Lobby null-guard.");
        }
    }
}
