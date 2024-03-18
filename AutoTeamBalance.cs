using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;

namespace AutoTeamBalance;

public class MovedPlayerInfo {
    public static string[] teamNames = new string[] { "TT", "CT" };
    public string? playerName { get; set; }
    public ulong SteamID { get; set; }
    public string[]? teams { get; set; }
    public int[]? teamCountBefore { get; set; }
    public int[]? teamCountAfter {  get; set; }
    public string? timeOfSwitch;
    public void FirstSet(string PlayerName, ulong SteamID, string team, int[] teamCountBefore) {
        this.teams = new string[2] { "", "" };
        this.playerName = PlayerName;
        this.SteamID = SteamID;
        this.teams[0] = team;
        this.teamCountBefore = (int[]?)teamCountBefore.Clone();
    }
    public void LastSet(int[] teamCountAfter, string time) {
        var playerAfter = Utilities.GetPlayerFromSteamId(this.SteamID);
        if (playerAfter != null) { this.teams[1] = teamNames[playerAfter.TeamNum - 2]; }
        this.teamCountAfter = (int[]?)teamCountAfter.Clone();
        this.timeOfSwitch = time;
    }
}

public class AutoTeamBalance : BasePlugin
{
    public override string ModuleName => " AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.1.8";
    public static string[] teamNames = new string[] { "TT", "CT" };
    public bool IsQueuedMatchmaking = false;
    public Random rand = new Random();
    public required ILogger logger;
    public List<MovedPlayerInfo> MovedPlayers = new List<MovedPlayerInfo>();

    public override void Load(bool hotReload)
    {
        MovedPlayers.Clear();
        logger = Logger;
        logger.LogInformation($"Plugin version: {ModuleVersion}");
        RegisterListener<Listeners.OnMapStart>(OnMapStartHandle);
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
            output.Add(" \u0004|> \u0001" + moved.timeOfSwitch + " \u0004[\u0001" + moved.playerName + "\u0004] - \u0007" + moved.teams[0] + " \u0004--> \u0007" + moved.teams[1] + " \u0004- \u0001TeamCount: \u0004[\u0001" + moved.teamCountBefore![1] + " \u0007CTs \u0001| " + moved.teamCountBefore[0] + " \u0007TTs\u0004] \u0004--> [\u0001" + moved.teamCountAfter![1] + " \u0007CTs \u0001| " + moved.teamCountAfter[0] + " \u0007TTs\u0004]>");
        }
        if (MovedPlayers.Count() == 0) { output.Add(" \u0007 List is empty."); }
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
            if (ttPlayers.Count() > ctPlayers.Count())
            {
                var player = ttPlayers[random.Next(ttPlayers.Count())];
                if (player.Team == CsTeam.Terrorist && player.Connected == PlayerConnectedState.PlayerConnected && player.IsValid)
                {
                    moved.FirstSet(player.PlayerName, player.SteamID, teamNames[player.TeamNum - 2], new int[2] { ttPlayers.Count(), ctPlayers.Count() });
                    player.SwitchTeam(CsTeam.CounterTerrorist);
                }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                logger.LogInformation($"Moved {player.PlayerName} [{player.SteamID}] to the team: {player.Team.ToString()} (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
            }
            else if (ctPlayers.Count() > ttPlayers.Count())
            {
                var player = ctPlayers[random.Next(ctPlayers.Count())];
                if (player.Team == CsTeam.CounterTerrorist && player.Connected == PlayerConnectedState.PlayerConnected && player.IsValid)
                {
                    moved.FirstSet(player.PlayerName, player.SteamID, teamNames[player.TeamNum - 2], new int[2] { ttPlayers.Count(), ctPlayers.Count() });
                    player.SwitchTeam(CsTeam.Terrorist);
                }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                logger.LogInformation($"Moved {player.PlayerName} [{player.SteamID}] to the team: {player.Team.ToString()} (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
            } else { break; }
            ttPlayers = PlayersTeams(CsTeam.Terrorist);
            ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
            moved.LastSet(new int[2] { ttPlayers.Count(), ctPlayers.Count() }, DateTime.Now.ToString("HH:mm:ss", new CultureInfo("pl_PL")));
            MovedPlayers.Add(moved);
        }
        return HookResult.Continue;
    }
    [GameEventHandler(HookMode.Post)]
    public HookResult OnplayerConnectFull(EventPlayerConnectFull @event, GameEventInfo @info) {
        var player = @event.Userid;
        AddTimer(1.0f, () =>
        {
            if (player == null
            || !player.IsValid
            || player.IsHLTV
            || player.IsBot
            || AdminManager.PlayerHasPermissions(player, "@css/ban")
            ) {
                if (AdminManager.PlayerHasPermissions(player, "@css/ban")) logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] ignored for on join balance, have @css/ban permission");
                return;
            }
            var tDF = teamDiffCount(player);
            if (tDF < 0)
            {
                if (player.Team != CsTeam.Terrorist)
                {
                    player.ChangeTeam(CsTeam.Terrorist);
                    logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] moved to Terrorists, teamDiffCount: {tDF}");
                } else logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] couldn't be moved to Terrorists becouse is already in it, teamDiffCount: {tDF}");
            }
            else if (tDF > 0)
            {
                if (player.Team != CsTeam.CounterTerrorist)
                {
                    player.ChangeTeam(CsTeam.CounterTerrorist);
                    logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] moved to CounterTerrorists, teamDiffCount: {tDF}");
                }
                else logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] couldn't be moved to CounterTerrorists becouse is already in it, teamDiffCount: {tDF}");
            }
            else if (IsQueuedMatchmaking && tDF == 0 && player.Team != CsTeam.Terrorist && player.Team != CsTeam.CounterTerrorist) {
                CsTeam move = (CsTeam)rand.Next(2, 4);
                player.ChangeTeam(move);
                logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] was randomly moved to {move.ToString()}, teamDiffCount: {tDF}");
            }
            var gamerule = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
            if (gamerule.WarmupPeriod) {
                player.Respawn();
                logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] was respawned becouse WarmupPeriod");
            }
        });
        return HookResult.Continue;
    }
    #endregion

    #region functions
    private int teamDiffCount(CCSPlayerController player)
    {
        var ttPlayers = PlayersTeams(CsTeam.Terrorist).Count();
        var ctPlayers = PlayersTeams(CsTeam.CounterTerrorist).Count();
        if (player.Team == CsTeam.Terrorist) ttPlayers--;
        else if (player.Team == CsTeam.CounterTerrorist) ctPlayers--;
        return ttPlayers - ctPlayers;
    }
    public void OnMapStartHandle(string mapName) {
        AddTimer(1.0f, () =>
        {
            var gamerule = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
            IsQueuedMatchmaking = gamerule.IsQueuedMatchmaking;
            if (!IsQueuedMatchmaking) logger.LogInformation($"Detected that teammenu is ENABLED, forced random upon join team switch: DISABLED");
            else logger.LogInformation($"Detected that teammenu is DISABLED, forced random upon join team switch: ENABLED");
        });
        MovedPlayers.Clear();
    }
    public List<CCSPlayerController> PlayersTeams(CsTeam team)
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
