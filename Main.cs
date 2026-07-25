using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace CMS21TogetherHotfix
{
    // Small companion hotfix for CMS21-Together.
    // Does NOT replace CMS21-Together.dll or TogetherFixes.dll.
    // It only adds two runtime Harmony patches:
    //   1) Fixes the EngineStandAnglePacket read mismatch (parts desync / garage-entry bug).
    //   2) Guards UILobby.RefreshPlayers against a null lobby window (reconnect crash).
    public class Main : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            var h = new Harmony("com.together.hotfix.enginestand-lobby");
            h.PatchAll(Assembly.GetExecutingAssembly());
            MelonLogger.Msg("[TogetherHotfix] applied: EngineStandAngle read fix + Lobby null-guard.");
        }
    }
}
