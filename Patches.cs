using System;
using HarmonyLib;
using MelonLoader;
using CMS21Together.Shared;
using CMS21Together.ClientSide;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ClientSide.Data.Garage.Car;
using CMS21Together.ClientSide.Data.Garage.Tools;
using CMS21Together.ClientSide.Data.NewUI;
using CMS21Together.Shared.Data.Vanilla.Cars;

namespace CMS21TogetherHotfix
{
    // FIX 1: the client read the engine-stand angle with ReadInt() while the
    // sender writes it as Write<float> (length-prefixed). That desynced the
    // packet buffer and threw "Offset and length out of bounds", corrupting the
    // packet stream -> parts disappeared / misplaced on garage entry.
    // We replace the handler with a correct Read<float>() (same as the server).
    [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.EngineStandAnglePacket))]
    public static class EngineStandAngleReadFix
    {
        public static bool Prefix(Packet packet)
        {
            // The old (0.4.16hf3) DLL sends only the angle float on this packet;
            // newer builds also append a bool "alt". Read the float, then try the
            // bool but fall back to false when it is absent. Never throw: a throw
            // here breaks engine-stand sync (the engine vanishes on the other
            // client) and can cascade into a garage resync that reverts the car
            // to its as-bought state.
            try
            {
                float angle = packet.Read<float>();
                bool alt = false;
                try { alt = packet.Read<bool>(); }
                catch { alt = false; }
                MelonCoroutines.Start(EngineStand.IncreaseEngineStandAngle(angle, alt));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[TogetherHotfix] EngineStandAngle read skipped: " + e.Message);
            }
            return false; // never run the broken original
        }
    }

    // FIX 2: RefreshPlayers()/DeleteAllPlayer() dereferenced UICore.TMP_Window
    // before the lobby window existed -> NullReferenceException on reconnect.
    // If the window is not ready yet, skip the refresh instead of crashing.
    [HarmonyPatch(typeof(UILobby), nameof(UILobby.RefreshPlayers))]
    public static class LobbyRefreshNullGuard
    {
        public static bool Prefix()
        {
            if (UICore.TMP_Window == null)
                return false; // window not ready -> do nothing (no crash)
            return true;      // otherwise run the original refresh
        }
    }

    // FIX 3 (EXPERIMENTAL): parking sync was disabled in the base mod.
    // In ParkHook.cs the hooks on GameDataManager.SaveCarInParking /
    // LoadCarInGarage are commented out, so when a player sends a car to the
    // parking lot the CURRENT car state (installed parts, body, engine) is
    // never captured and never sent to the server. Result: the parked car
    // reverts to its as-bought state and the partner never sees it.
    //
    // All the downstream plumbing is intact and active:
    //   ClientSend.AddCarToParkPacket -> ServerHandle -> ServerSend ->
    //   ClientHandle.AddCarToParkPacket -> ParkHook.AddCarToPark, plus
    //   ServerResyncs.ResyncPark on join.
    // We only re-enable the trigger the authors left commented, faithfully
    // including their ParkHook.listen echo-guard.
    //
    // The target methods are vanilla Il2Cpp methods, so we resolve and patch
    // them MANUALLY by name from Main (see Main.cs) to avoid a compile-time
    // reference to Assembly-CSharp. This class only holds the postfix bodies.
    public static class ParkingSyncFix
    {
        // Postfix for GameDataManager.SaveCarInParking(NewCarData carData, int index)
        public static void SaveCarInParkingPostfix(object[] __args, int __1)
        {
            try
            {
                if (Client.Instance == null || !Client.Instance.isConnected)
                    return;

                // Echo guard: when we RECEIVE a parked car, ParkHook sets
                // listen=false before calling SaveCarInParking. Consume it and
                // do NOT re-broadcast (prevents a feedback loop).
                if (!ParkHook.listen)
                {
                    ParkHook.listen = true;
                    return;
                }

                // Parking slots use small indices. If the runtime hands us a
                // huge value the int argument was marshalled wrong, so skip it
                // to avoid corrupting the other player's parking.
                int index = __1;
                if (index < 0 || index > 50)
                {
                    MelonLogger.Warning($"[TogetherHotfix] parking sync: suspicious index {index}, skipping.");
                    return;
                }

                object carData = (__args != null && __args.Length > 0) ? __args[0] : null;

                // Null car == the slot is being cleared -> tell others to remove.
                if (carData == null)
                {
                    ClientSend.RemoveCarFromParkPacket(index);
                    MelonLogger.Msg($"[TogetherHotfix] parking sync: removed car from park (index {index}).");
                    return;
                }

                // Build a ModNewCarData from the vanilla NewCarData via reflection
                // (ctor: ModNewCarData(NewCarData, int placeNo = 0, int _jobID = -1))
                // so we don't need the vanilla type at compile time.
                var modCar = (ModNewCarData)Activator.CreateInstance(
                    typeof(ModNewCarData), carData, 0, -1);

                ClientSend.AddCarToParkPacket(modCar, index);
                MelonLogger.Msg($"[TogetherHotfix] parking sync: sent car to park (index {index}).");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[TogetherHotfix] SaveCarInParkingPostfix failed: " + e.Message);
            }
        }

    }

    // FIX 4 helper: swallow exceptions thrown by the base mod's
    // CarSyncHooks.SwitchCarPartHook so a missing loadedCars key (which happens
    // right after buying a car) can't crash the game. The hook is a postfix, so
    // the real part switch has already run by the time it throws.
    public static class SwitchCarPartGuard
    {
        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
                MelonLogger.Warning(
                    "[TogetherHotfix] SwitchCarPart: swallowed base-mod " +
                    __exception.GetType().Name + " to prevent crash.");
            return null;
        }
    }
}
