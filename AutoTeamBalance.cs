using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System.Data;
using System.Numerics;

namespace AutoTeamBalance;

public class MovedPlayerInfo {
    public string? playerName { get; set; }
    public ulong SteamID { get; set; }
    public string[]? teams { get; set; }
    public int[]? teamCountBefore { get; set; }
    public int[]? teamCountAfter {  get; set; }
}

public class AutoTeamBalance : BasePlugin
{
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.0.7";
    public static string[] teamNames = new string[] { "TT", "CT" };
    public List<MovedPlayerInfo> MovedPlayers = new List<MovedPlayerInfo>();

    public override void Load(bool hotReload)
    {
        MovedPlayers.Clear();
        base.Load(hotReload);
    }

    #region Commands
    [ConsoleCommand("css_atb", "Status of AutoTeamBalance Plugin.")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnATBCommand(CCSPlayerController? player, CommandInfo command) {
        if (!AdminManager.PlayerHasPermissions(player, "@css/ban")) {
            player!.PrintToChat(Localizer["NoPermissions"]);
            return;
        }
        List<string> output = new List<string>();
        output.Clear();
        output.Add("[\u0004AutoTeamBalance\u0001]");
        output.Add(" \u0004Plugin Version\u0001: " + ModuleVersion);
        output.Add(" \u0004Current known TeamCounts\u0001:  " + PlayersTeams(CsTeam.CounterTerrorist).Count() + " \u0007CTs \u0001| " + PlayersTeams(CsTeam.Terrorist).Count() + " \u0007TTs");
        output.Add(" \u0004Current know Players after balance\u0001: ");
        foreach (var moved in MovedPlayers)
        {
            output.Add(" \u0004|> \u0004[\u0001" + moved.playerName + "\u0004] - \u0007" + moved.teams[0] + " \u0004--> \u0007" + moved.teams[1] + " \u0004- \u0001TeamCount: \u0004[\u0001" + moved.teamCountBefore![1] + " \u0007CTs \u0001| " + moved.teamCountBefore[0] + " \u0007TTs\u0004] \u0004--> [\u0001" + moved.teamCountAfter![1] + " \u0007CTs \u0001| " + moved.teamCountAfter[0] + " \u0007TTs\u0004]>");
        }
        if (MovedPlayers.Count == 0) { output.Add(" \u0007 List is empty.");  }
        output.Add("[\u0001/\u0004AutoTeamBalance\u0001]");
        if (player == null) foreach (var str in output) { Server.PrintToConsole(str); }
        else foreach (var str in output) { player.PrintToChat(str); }
    }
    #endregion

    #region Events
    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info) {
        var ttPlayers = PlayersTeams(CsTeam.Terrorist);
        var ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
        while (Math.Abs(ttPlayers.Count() - ctPlayers.Count()) >= 2)
        {
            Random random = new Random();
            MovedPlayerInfo moved = new MovedPlayerInfo();
            moved.teams = new string[2] { "", "" };
            var playertt = ttPlayers[random.Next(ttPlayers.Count())];
            var playerct = ctPlayers[random.Next(ctPlayers.Count())];
            if (ttPlayers.Count() > ctPlayers.Count())
            {
                if (playertt.Team == CsTeam.Terrorist && playertt.Connected == PlayerConnectedState.PlayerConnected && playertt.IsValid)
                {
                    moved.playerName = playertt.PlayerName;
                    moved.teams[0] = teamNames[playertt.TeamNum - 2];
                    moved.teamCountBefore = new int[2] { ttPlayers.Count(), ctPlayers.Count() };
                    playertt.SwitchTeam(CsTeam.CounterTerrorist);
                }
                playertt.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + playertt.PlayerName + " [" + playertt.SteamID + "] to the team: " + playertt.Team.ToString() + " (After[tt:" + ttPlayers.Count() + " ct:" + ctPlayers.Count() + "])");
            }
            else if (ctPlayers.Count() > ttPlayers.Count())
            {
                if (playerct.Team == CsTeam.CounterTerrorist && playerct.Connected == PlayerConnectedState.PlayerConnected && playerct.IsValid)
                {
                    moved.playerName = playerct.PlayerName;
                    moved.teams[0] = teamNames[playerct.TeamNum - 2];
                    moved.teamCountBefore = new int[2] { ttPlayers.Count(), ctPlayers.Count() };
                    playerct.SwitchTeam(CsTeam.Terrorist);
                }
                playerct.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + playerct.PlayerName + " [" + playerct.SteamID + "] to the team: " + playerct.Team.ToString() + " (After[tt:" + ttPlayers.Count() + " ct:" + ctPlayers.Count() + "])");
            }
            ttPlayers = PlayersTeams(CsTeam.Terrorist);
            ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
            var player = Utilities.GetPlayerFromSteamId(moved.SteamID);
            moved.teams[1] = teamNames[player.TeamNum - 2];
            moved.teamCountAfter = new int[2] { ttPlayers.Count(), ctPlayers.Count() };
            MovedPlayers.Add(moved);
        }
        return HookResult.Continue;
    }
    #endregion

    #region functions
    public List<CCSPlayerController> PlayersTeams( CsTeam team)
    {
       return (Utilities.GetPlayers().Where(p =>
              p.IsValid
              && !p.IsHLTV
              && !p.IsBot
              && p.Connected == PlayerConnectedState.PlayerConnected
              && p.SteamID.ToString().Length == 17
              && p.Team == team)).ToList();
    }
    #endregion
}
