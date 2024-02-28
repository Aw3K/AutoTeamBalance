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

public class AutoTeamBalance : BasePlugin
{
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.0.6";

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
        output.Add(" \u0004Current known TeamCounts\u0001:  " + PlayersTeams(CsTeam.Terrorist).Count() + " \u0007TTs \u0001| " + PlayersTeams(CsTeam.CounterTerrorist).Count() + " \u0007CTs");
        if (player == null) foreach (var str in output) { Server.PrintToConsole(str); }
        else foreach (var str in output) { player.PrintToChat(str); }
    }
    #endregion

    #region Events
    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info) {
        var ttPlayers = PlayersTeams(CsTeam.Terrorist);
        var ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
        if (Math.Abs(ttPlayers.Count() - ctPlayers.Count()) >= 2)
        {
            Random random = new Random();
            if (ttPlayers.Count() > ctPlayers.Count())
            {
                var player = ttPlayers[random.Next(ttPlayers.Count() - 1)];
                if (player.Team == CsTeam.Terrorist && player.Connected == PlayerConnectedState.PlayerConnected && player.IsValid) { player.SwitchTeam(CsTeam.CounterTerrorist); }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (After[tt:" + ttPlayers.Count() + " ct:" + ctPlayers.Count() + "])");
            }
            else if (ctPlayers.Count() > ttPlayers.Count())
            {
                var player = ttPlayers[random.Next(ctPlayers.Count() - 1)];
                if (player.Team == CsTeam.CounterTerrorist && player.Connected == PlayerConnectedState.PlayerConnected && player.IsValid) { player.SwitchTeam(CsTeam.Terrorist); }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (After[tt:" + ttPlayers.Count() + " ct:" + ctPlayers.Count() + "])");
            }
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
