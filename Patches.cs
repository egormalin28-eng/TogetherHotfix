using HarmonyLib;
using MelonLoader;
using CMS21Together.Shared;
using CMS21Together.ClientSide.Data.Handle;
using CMS21Together.ClientSide.Data.Garage.Tools;
using CMS21Together.ClientSide.Data.NewUI;

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
            float angle = packet.Read<float>();
            bool alt = packet.Read<bool>();
            MelonCoroutines.Start(EngineStand.IncreaseEngineStandAngle(angle, alt));
            return false; // skip the broken original
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
}
