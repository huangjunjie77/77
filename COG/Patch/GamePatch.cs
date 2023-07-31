using COG.Listener;
using System.Linq;

namespace COG.Patch;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
class CoBeginPatch
{
    public static void Prefix()
    {
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnCoBegin();
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
class EndGamePatch
{
    public static void Prefix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
    {
        var list = ListenerManager.GetManager().GetListeners().ToList();
        foreach (var listener in list)
        {
            listener.OnGameEnd(__instance, ref endGameResult);
        }
    }
    
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
    {
        var list = ListenerManager.GetManager().GetListeners().ToList();
        foreach (var listener in list)
        {
            listener.AfterGameEnd(__instance, ref endGameResult);
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
class GameStartManagerStartPatch
{
    public static void Postfix(GameStartManager __instance)
    {
        HostSartPatch.timer = 600f;

        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnGameStart(__instance);
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
internal class MakePublicPatch
{
    public static bool Prefix(GameStartManager __instance)
    {
        bool returnAble = false;
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            if (!listener.OnMakePublic(__instance) && !returnAble)
            {
                returnAble = true;
            }
        }

        if (returnAble) return false;

        return true;
    }
}

[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
class SelectRolesPatch
{
    public static void Prefix()
    {
        var listeners = ListenerManager.GetManager().GetListeners().ToList();
        foreach (var listener in listeners)
        {
            listener.OnSelectRoles();
        }
    }
}

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
class SetEverythingUpPatch
{
    public static void Postfix(EndGameManager __instance)
    {
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnGameEndSetEverythingUp(__instance);
        }
    }
}
[HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
class KeyboardPatch
{
    public static void Postfix()
    {
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            listener.OnKeyboardPass();
        }
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
public static class PlayerVentPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Vent __instance,
        [HarmonyArgument(0)] GameData.PlayerInfo playerInfo,
        [HarmonyArgument(1)] ref bool canUse,
        [HarmonyArgument(2)] ref bool couldUse,
        ref float __result)
    {
        var returnAble = true;
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            if (!listener.OnPlayerVent(__instance, playerInfo, ref canUse, ref couldUse, ref __result))
            {
                returnAble = false;
            }
        }
        return returnAble;
    }
}

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
class GameEndChecker
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        var returnAble = true;
        foreach (var unused in ListenerManager.GetManager().GetListeners().Where(listener => !listener.OnCheckGameEnd()))
        {
            returnAble = false;
        }

        return returnAble;
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
class CheckTaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        var returnAble = true;
        foreach (var listener in ListenerManager.GetManager().GetListeners())
        {
            if (!listener.OnCheckTaskCompletion(ref __result)) returnAble = false;
        }

        return returnAble;
    }
}

[HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.ShowSabotageMap))]
class SabotageMapOpen
{
    private static bool Prefix(MapBehaviour __instance)
    {
        var returnAble = true;
        foreach (var unused in ListenerManager.GetManager().GetListeners().Where(listener => !listener.OnShowSabotageMap(__instance)))
        {
            returnAble = false;
        }

        return returnAble;
    }
}