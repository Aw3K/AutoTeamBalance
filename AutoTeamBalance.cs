using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System.Numerics;

namespace AutoTeamBalance;

public class AutoTeamBalance : BasePlugin
{
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.0.5";

    private int[] TeamCount = new int[4];
    private List<CCSPlayerController> balancePending = new List<CCSPlayerController>();
    public override void Load(bool hotReload)
    {
        Array.Clear(TeamCount, 0, 4);
        balancePending.Clear();
        var playersList = Utilities.GetPlayers().Where(p =>
                p.IsValid
                && !p.IsHLTV
                && p.Connected == PlayerConnectedState.PlayerConnected
                && p.SteamID.ToString().Length == 17);
        foreach (var player in playersList) {
            if (player.Team == CsTeam.Terrorist) TeamCount[2]++;
            if (player.Team == CsTeam.CounterTerrorist) TeamCount[3]++;
        }
    }

    #region Events

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundPreStart(EventRoundEnd @event, GameEventInfo info) {
        foreach (var kvp in balancePending) {
            var player = Utilities.GetPlayerFromSteamId(kvp.SteamID)!;
            if (!player.IsHLTV
            && player != null
            && !player.IsBot
            && player.IsValid
            && player.Connected == PlayerConnectedState.PlayerConnected ){
                int[] TeamCountBefore = (int[])TeamCount.Clone();
                if (Math.Abs(TeamCount[2] - TeamCount[3]) >= 2) {
                    if (TeamCount[2] > TeamCount[3])
                    {
                        if (player.Team == CsTeam.Terrorist) { player.SwitchTeam(CsTeam.CounterTerrorist); }
                        player.PrintToChat(Localizer["ForcedChangedTeam"]);
                        Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (Before[tt:" + TeamCountBefore[2] + " ct:" + TeamCountBefore[3] + "] After[tt:" + TeamCount[2] + " ct:" + TeamCount[3] + "])");
                    }
                    else if (TeamCount[3] > TeamCount[2])
                    {
                        if (player.Team == CsTeam.CounterTerrorist) { player.SwitchTeam(CsTeam.Terrorist); }
                        player.PrintToChat(Localizer["ForcedChangedTeam"]);
                        Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (Before[tt:" + TeamCountBefore[2] + " ct:" + TeamCountBefore[3] + "] After[tt:" + TeamCount[2] + " ct:" + TeamCount[3] + "])");
                    }
                }
            }
        }
        balancePending.Clear();
        return HookResult.Continue;
    }
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player.IsHLTV
        || player == null
        || player.IsBot
        || !player.IsValid
        || player.Connected != PlayerConnectedState.PlayerConnected
        ) return HookResult.Continue;

        int newTeam = @event.Team;
        int oldTeam = @event.Oldteam;
        bool disconnect = @event.Disconnect;

        if (newTeam != oldTeam && disconnect == false) {
            if (oldTeam >= 2 && TeamCount[oldTeam] >= 1) {
                TeamCount[oldTeam]--;
            }
            if (newTeam >= 2) {
                TeamCount[newTeam]++;
            }
        }

        //Server.PrintToConsole("[ATBTest] " + newTeam + " " + oldTeam + " " + disconnect);
        //Server.PrintToConsole("[ATBTest] tt " + TeamCount[2] + " ct " + TeamCount[3]);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info) {
        CCSPlayerController player = @event.Userid;

        if (player.IsHLTV
         || player == null
         || player.IsBot
         || !player.IsValid
        ) return HookResult.Continue;

        TeamCount[@event.Userid.TeamNum]--;
        if (TeamCount[@event.Userid.TeamNum] < 0) TeamCount[@event.Userid.TeamNum] = 0;
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;
        var gameRule = new CCSGameRules(@info.Handle);

        if (
            player.IsHLTV
         || player == null
         || !player.IsValid
         || player.Connected != PlayerConnectedState.PlayerConnected
         || player.SteamID.ToString().Length != 17
         || @event.Weapon == "world"
         || gameRule.WarmupPeriod
        ) return HookResult.Continue;

        if (Math.Abs(TeamCount[2] - TeamCount[3]) >= 2)
        {
            if (TeamCount[2] > TeamCount[3]) { balancePending.Add(player); }
            else if (TeamCount[3] > TeamCount[2]) { balancePending.Add(player); }
        }
        return HookResult.Continue;
    }
    #endregion
}
