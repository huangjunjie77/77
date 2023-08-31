using System.Collections.Generic;
using AmongUs.GameOptions;
using COG.Config.Impl;
using COG.Role;
using COG.Role.Impl;
using COG.Rpc;
using COG.States;
using COG.UI.CustomWinner;
using COG.Utils;
using Il2CppSystem;
using Il2CppSystem.Collections;
using UnityEngine;

namespace COG.Listener.Impl;

public class GameListener : IListener
{
    private static readonly List<IListener> RoleListeners = new();

    private static bool HasStartedRoom { get; set; }
    // private static bool _forceStarted;

    public void OnCoBegin()
    {
        // _forceStarted = false;
        GameStates.InGame = true;
        Main.Logger.LogInfo("Game started!");

        foreach (var playerRole in GameUtils.Data)
            RoleManager.Instance.SetRole(playerRole.Player, playerRole.Role.BaseRoleType);
    }

    public void OnRPCReceived(byte callId, MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;
        var knownRpc = (KnownRpc)callId;
        if (knownRpc != KnownRpc.ShareRoles) return;
        
        // already for roles

        // GameUtils.Data = data;
    }

    public void AfterPlayerFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsLobby && AmongUsClient.Instance.AmHost)
        {
            GameOptionsManager.Instance.currentNormalGameOptions.RoleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
            GameOptionsManager.Instance.currentNormalGameOptions.RoleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
            GameOptionsManager.Instance.currentNormalGameOptions.RoleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            GameOptionsManager.Instance.currentNormalGameOptions.RoleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
        }
    }

    private void SelectRoles()
    {
        GameUtils.Data.Clear(); // 首先清除 防止干扰

        if (!AmongUsClient.Instance.AmHost) return; // 不是房主停止分配

        // 开始分配职业
        var players = PlayerUtils.GetAllPlayers().ToListCustom().Disarrange(); // 打乱玩家顺序

        var rolesToAdd = new List<Role.Role>(); // 新建集合，用来存储可用的职业 

        // 获取最大可以启用的内鬼数量
        var maxImpostorsNum = GameUtils.GetImpostorsNum();

        // 新建一个获取器
        var getter = Role.RoleManager.GetManager().NewGetter();

        // 获取已经获取的内鬼职业数量
        var equalsImpostorsNum = 0;

        // 开始获取可以分配的职业
        while (getter.HasNext())
        {
            var next = getter.GetNext();
            if (next == null) continue;
            if (rolesToAdd.Count >= players.Count) break;
            if (equalsImpostorsNum >= maxImpostorsNum && next.CampType == CampType.Impostor) continue;
            if (next.CampType == CampType.Impostor) equalsImpostorsNum++;
            rolesToAdd.Add(next);
        }

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            Role.Role role;
            try
            {
                role = rolesToAdd[i];
            }
            catch
            {
                role = Role.RoleManager.GetManager().GetTypeRoleInstance<Unknown>();
            }

            GameUtils.Data.Add(new PlayerRole(player, role));
        }

        // 打印职业分配信息
        foreach (var playerRole in GameUtils.Data)
        {
            Main.Logger.LogInfo($"{playerRole.Player.name}({playerRole.Player.Data.FriendCode})" +
                                $" => {playerRole.Role.GetType().Name}");
        }

        foreach (var playerRole in GameUtils.Data) RoleListeners.Add(playerRole.Role.GetListener(playerRole.Player));
    }

    public void OnSelectRoles()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        foreach (var playerRole in GameUtils.Data)
        {
            playerRole.Player.SetRole(playerRole.Role.BaseRoleType);
        }
    }

    public void OnGameStart(GameStartManager manager)
    {
        if (HasStartedRoom)
            GameUtils.ForceClearGameData();
        else
            HasStartedRoom = true;
        // 改变按钮颜色
        manager.MakePublicButton.color = Palette.DisabledClear;
        manager.privatePublicText.color = Palette.DisabledClear;

        PlayerUtils.Players.Clear();
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player != null)
                PlayerUtils.Players.Add(player);
    }

    public bool OnMakePublic(GameStartManager manager)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        GameUtils.SendGameMessage(LanguageConfig.Instance.MakePublicMessage);
        // 禁止设置为公开
        return false;
    }

    public bool OnSetUpRoleText(IntroCutscene intro, ref IEnumerator roles)
    {
        Main.Logger.LogInfo("Setup role text for players...");

        var myRole = GameUtils.GetLocalPlayerRole();
        if (myRole == null) return true;

        var list = new List<IEnumerator>();

        void SetupRoles()
        {
            if (GameOptionsManager.Instance.currentGameMode != GameModes.Normal) return;
            intro.RoleText.text = myRole.Name;
            intro.RoleText.color = myRole.Color;
            intro.RoleBlurbText.text = myRole.Description;
            intro.RoleBlurbText.color = myRole.Color;
            intro.YouAreText.color = myRole.Color;

            intro.YouAreText.gameObject.SetActive(true);
            intro.RoleText.gameObject.SetActive(true);
            intro.RoleBlurbText.gameObject.SetActive(true);

            SoundManager.Instance.PlaySound(PlayerControl.LocalPlayer.Data.Role.IntroSound, false);

            if (intro.ourCrewmate == null)
            {
                intro.ourCrewmate = intro.CreatePlayer(0, 1, PlayerControl.LocalPlayer.Data, false);
                intro.ourCrewmate.gameObject.SetActive(false);
            }

            intro.ourCrewmate.gameObject.SetActive(true);
            var transform = intro.ourCrewmate.transform;
            transform.localPosition = new Vector3(0f, -1.05f, -18f);
            transform.localScale = new Vector3(1f, 1f, 1f);
            intro.ourCrewmate.ToggleName(false);
        }

        list.Add(Effects.Action((Action)(System.Action?)SetupRoles));
        list.Add(Effects.Wait(2.5f));

        void Action()
        {
            intro.YouAreText.gameObject.SetActive(false);
            intro.RoleText.gameObject.SetActive(false);
            intro.RoleBlurbText.gameObject.SetActive(false);
            intro.ourCrewmate.gameObject.SetActive(false);
        }

        list.Add(Effects.Action((Action)(System.Action?)Action));

        roles = Effects.Sequence(list.ToArray());
        return false;
    }

    public void OnSetUpTeamText(IntroCutscene intro,
        ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        var role = GameUtils.GetLocalPlayerRole();
        var player = PlayerControl.LocalPlayer;

        var camp = role?.CampType;
        if (camp is CampType.Neutral or CampType.Unknown)
        {
            var soloTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            soloTeam.Add(player);
            teamToDisplay = soloTeam;
        }
    }

    public void AfterSetUpTeamText(IntroCutscene intro)
    {
        var role = GameUtils.GetLocalPlayerRole();

        if (role == null) return;
        var camp = role.CampType;

        intro.BackgroundBar.material.color = camp.GetColor();
        intro.TeamTitle.text = camp.GetName();
        intro.TeamTitle.color = camp.GetColor();

        intro.ImpostorText.text = camp.GetDescription();
    }

    public void OnGameEndSetEverythingUp(EndGameManager manager)
    {
        // 取消已经注册的Listener
        foreach (var roleListener in RoleListeners) ListenerManager.GetManager().UnregisterListener(roleListener);
        RoleListeners.Clear();
        _sharedRoles = false;
    }

    public bool OnCheckGameEnd()
    {
        return !GlobalCustomOption.DebugMode.GetBool() && CustomWinnerManager.CheckEndForCustomWinners();
    }

    public bool OnPlayerVent(Vent vent, GameData.PlayerInfo playerInfo, ref bool canUse, ref bool couldUse,
        ref float cooldown)
    {
        foreach (var playerRole in GameUtils.Data)
        {
            if (!playerRole.Player.Data.IsSamePlayer(playerInfo)) continue;
            var ventAble = playerRole.Role.CanVent;
            canUse = ventAble;
            couldUse = ventAble;
            cooldown = float.MaxValue;
            return false;
        }

        return true;
    }

    public void OnKeyboardPass()
    {
        // 有BUG 暂时不用
        /*
        if (GameStates.InGame || !GameStates.IsLobby || GameStates.IsMeeting || GameStates.IsVoting || GameStates.IsInTask) return;
        if (GameStartManager.Instance.startState != GameStartManager.StartingStates.Countdown) return;
        if (_forceStarted) return;
        if ((!Input.GetKey(KeyCode.RightShift) && !Input.GetKey(KeyCode.LeftShift)) || _forceStarted) return;
        _forceStarted = true;
        GameStartManager.Instance.countDownTimer = 1f;
        */
    }

    public void OnHudUpdate(HudManager manager)
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null) return;
        Role.Role? role;
        try
        {
            role = player.GetRoleInstance();
        }
        catch
        {
            return;
        }

        if (role == null) return;

        if (role.CanKill)
        {
            manager.KillButton.ToggleVisible(true);
            player.Data.Role.CanUseKillButton = true;
            manager.KillButton.gameObject.SetActive(true);
        }
        else
        {
            manager.KillButton.SetDisabled();
            manager.KillButton.ToggleVisible(false);
            manager.KillButton.gameObject.SetActive(false);
        }

        if (role.CanVent)
        {
            manager.ImpostorVentButton.ToggleVisible(true);
            player.Data.Role.CanVent = true;
            manager.ImpostorVentButton.gameObject.SetActive(true);
        }
        else
        {
            manager.ImpostorVentButton.SetDisabled();
            manager.ImpostorVentButton.ToggleVisible(false);
            manager.ImpostorVentButton.gameObject.SetActive(false);
        }

        if (role.CanSabotage)
        {
            manager.SabotageButton.ToggleVisible(true);
            manager.SabotageButton.gameObject.SetActive(true);
        }
        else
        {
            manager.SabotageButton.SetDisabled();
            manager.SabotageButton.ToggleVisible(false);
            manager.SabotageButton.gameObject.SetActive(false);
        }
    }

    private static void ShareRoles()
    {
        var writer = RpcUtils.StartRpcImmediately(PlayerControl.LocalPlayer, (byte)KnownRpc.ShareRoles);
        
        // ready for share roles
        writer.Write(GameUtils.Data.Count);
        foreach (var playerRole in GameUtils.Data)
        {
            writer.WritePacked(playerRole.Player.PlayerId);
            writer.WritePacked(playerRole.Role.Id);
        }
        
        writer.Finish();
    }

    private static bool _sharedRoles;

    public void OnGameStartManagerUpdate(GameStartManager manager)
    {
        if (manager.startState != GameStartManager.StartingStates.Countdown) return;
        if (manager.countDownTimer <= 0.5f && !_sharedRoles)
        {
            Main.Logger.LogInfo("Select roles for players...");
            SelectRoles();
            Main.Logger.LogInfo("Share roles for players...");
            ShareRoles();
            _sharedRoles = true;
        }
    }
}