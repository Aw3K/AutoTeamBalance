using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;

namespace AutoTeamBalance;

public class AutoTeamBalance : BasePlugin
{
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.0.3";

    public int[] TeamCount = new int[4];
    public override void Load(bool hotReload)
    {
        Array.Clear(TeamCount, 0, 4);
    }

    #region Events

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;

        if (player.IsHLTV
         || player == null
         || player.IsBot
         || !player.IsValid
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
            int[] TeamCountBefore = (int[])TeamCount.Clone();
            if (TeamCount[2] > TeamCount[3]) {
                if (player.Team == CsTeam.Terrorist) { player.SwitchTeam(CsTeam.CounterTerrorist); }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (Before[tt:" + TeamCountBefore[2] + " ct:" + TeamCountBefore[3] + "] After[tt:" + TeamCount[2] + " ct:" + TeamCount[3] + "])");
            }
            else if (TeamCount[3] > TeamCount[2]) {
                if (player.Team == CsTeam.CounterTerrorist) { player.SwitchTeam(CsTeam.Terrorist); }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (Before[tt:" + TeamCountBefore[2] + " ct:" + TeamCountBefore[3] + "] After[tt:" + TeamCount[2] + " ct:" + TeamCount[3] + "])");
            }
        }
        return HookResult.Continue;
    }
    #endregion
}
