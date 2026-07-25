using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace CMS21TogetherHotfix
{
    // Small companion hotfix for CMS21-Together.
    // Does NOT replace CMS21-Together.dll or TogetherFixes.dll.
    // It adds runtime Harmony patches:
    //   1) Fixes the EngineStandAnglePacket read mismatch (parts desync / garage-entry bug).
    //   2) Guards UILobby.RefreshPlayers against a null lobby window (reconnect crash).
    //   3) (EXPERIMENTAL) Re-enables parking sync so a car sent to the parking
    //      lot keeps its state and is synced to the other player.
    public class Main : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            var h = new HarmonyLib.Harmony("com.together.hotfix.enginestand-lobby");

            // FIX 1 + FIX 2 (attribute patches). Kept isolated so nothing below
            // can break them.
            h.PatchAll(Assembly.GetExecutingAssembly());
            MelonLogger.Msg("[TogetherHotfix] applied: EngineStandAngle read fix + Lobby null-guard.");

            // FIX 3 (EXPERIMENTAL) - manual patch, fully guarded.
            TryEnableParkingSync(h);
        }

        private void TryEnableParkingSync(HarmonyLib.Harmony h)
        {
            try
            {
                // GameDataManager is a vanilla Il2Cpp type. Resolve it by name so
                // we don't need a compile-time reference to Assembly-CSharp.
                Type gdm = AccessTools.TypeByName("GameDataManager");
                if (gdm == null)
                {
                    MelonLogger.Warning("[TogetherHotfix] parking sync: GameDataManager type not found; skipping (fixes 1&2 still active).");
                    return;
                }

                MethodInfo save = AccessTools.Method(gdm, "SaveCarInParking");
                if (save != null)
                {
                    h.Patch(save, postfix: new HarmonyMethod(
                        typeof(ParkingSyncFix), nameof(ParkingSyncFix.SaveCarInParkingPostfix)));
                    MelonLogger.Msg("[TogetherHotfix] parking sync: hooked GameDataManager.SaveCarInParking.");
                }
                else
                {
                    MelonLogger.Warning("[TogetherHotfix] parking sync: SaveCarInParking not found; skipping.");
                }

                MethodInfo load = AccessTools.Method(gdm, "LoadCarInGarage");
                if (load != null)
                {
                    h.Patch(load, postfix: new HarmonyMethod(
                        typeof(ParkingSyncFix), nameof(ParkingSyncFix.LoadCarInGaragePostfix)));
                    MelonLogger.Msg("[TogetherHotfix] parking sync: hooked GameDataManager.LoadCarInGarage.");
                }
                else
                {
                    MelonLogger.Warning("[TogetherHotfix] parking sync: LoadCarInGarage not found (remove-from-park skipped).");
                }
            }
            catch (Exception e)
            {
                // Never let the experimental fix take down the working patches.
                MelonLogger.Warning("[TogetherHotfix] parking sync setup failed (fixes 1&2 still active): " + e.Message);
            }
        }
    }
}
