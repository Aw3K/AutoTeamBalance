using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Text.Json.Serialization;

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

public class AutoTeamBalanceConfig : BasePluginConfig
{
    [JsonPropertyName("PlayersJoinBehaviour")] public string PlayersJoinBehaviour { get; set; } = new("");
    [JsonPropertyName("BasicPermissions")] public string BasicPermissions { get; set; } = new("");
    [JsonPropertyName("TeamCountMax")] public int? TeamCountMax { get; set; }
    [JsonPropertyName("TeamCountMaxBehaviour")] public string TeamCountMaxBehaviour { get; set; } = new("");
}

public class AutoTeamBalance : BasePlugin, IPluginConfig<AutoTeamBalanceConfig>
{
    public override string ModuleName => " AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.2.0";

    public AutoTeamBalanceConfig Config { get; set; } = new();

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
        if (!AdminManager.PlayerHasPermissions(player, Config.BasicPermissions)) {
            player!.PrintToChat(Localizer["NoPermissions"]);
            return;
        }
        command.ReplyToCommand($"[\u0004AutoTeamBalance\u0001]");
        command.ReplyToCommand($" \u0004Plugin Version\u0001: {ModuleVersion}");
        command.ReplyToCommand($" \u0004Plugin Config\u0001: {Config.BasicPermissions} \u0004| \u0001{Config.PlayersJoinBehaviour} \u0004| \u0001{Config.TeamCountMax} \u0004| \u0001{Config.TeamCountMaxBehaviour}");
        command.ReplyToCommand($" \u0004Current known TeamCounts\u0001:  {PlayersTeams(CsTeam.CounterTerrorist).Count()} \u0007CTs \u0001| {PlayersTeams(CsTeam.Terrorist).Count()} \u0007TTs");
        command.ReplyToCommand($" \u0004Current know Players after balance\u0001: ");
        foreach (var moved in MovedPlayers)
        {
            command.ReplyToCommand($" \u0004|> \u0001{moved.timeOfSwitch} \u0004[\u0001{moved.playerName}\u0004] - \u0007{moved.teams[0]} \u0004--> \u0007{moved.teams[1]} \u0004- \u0001TeamCount: \u0004[\u0001{moved.teamCountBefore![1]} \u0007CTs \u0001| {moved.teamCountBefore[0]} \u0007TTs\u0004] \u0004--> [\u0001{moved.teamCountAfter![1]} \u0007CTs \u0001| {moved.teamCountAfter[0]} \u0007TTs\u0004]>");
        }
        if (MovedPlayers.Count() == 0) { command.ReplyToCommand(" \u0007 List is empty."); }
        command.ReplyToCommand($"[\u0001/\u0004AutoTeamBalance\u0001]");
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
            if (Config.PlayersJoinBehaviour == "off"
            || player == null
            || !player.IsValid
            || player.IsHLTV
            || player.IsBot
            || AdminManager.PlayerHasPermissions(player, Config.BasicPermissions)){
                if (AdminManager.PlayerHasPermissions(player, Config.BasicPermissions) && Config.PlayersJoinBehaviour != "off") logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] ignored for on join balance, have '{Config.BasicPermissions}' permission");
                return;
            }

            if ((PlayersTeams(CsTeam.Terrorist).Count() + PlayersTeams(CsTeam.CounterTerrorist).Count()) >= Config.TeamCountMax * 2) {
                if (Config.TeamCountMaxBehaviour == "none") return;
                else if (Config.TeamCountMaxBehaviour == "spect")
                {
                    player.SwitchTeam(CsTeam.Spectator);
                    logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] moved to spectator becouse TeamCountMaxBehaviour is set to 'spect'");
                }
                else if (Config.TeamCountMaxBehaviour == "kick")
                {
                    Server.ExecuteCommand($"kickid {player.UserId} max players reached");
                    logger.LogInformation($"[EventPlayerConnectFull][{player.PlayerName}] was kicked becouse TeamCountMaxBehaviour is set to 'kick'");
                }
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
            if (gamerule.WarmupPeriod && (player.Team == CsTeam.Terrorist || player.Team == CsTeam.CounterTerrorist)) {
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
            if (Config.PlayersJoinBehaviour == "off") {
                IsQueuedMatchmaking = false;
                logger.LogInformation($"PlayersJoinBehaviour is set to off, disabling forced team switch upon join completly");
            } else if (Config.PlayersJoinBehaviour == "default") {
                var gamerule = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
                IsQueuedMatchmaking = gamerule.IsQueuedMatchmaking;
                if (!IsQueuedMatchmaking) logger.LogInformation($"Detected that teammenu is ENABLED, forced random upon join team switch: DISABLED");
                else logger.LogInformation($"Detected that teammenu is DISABLED, forced random upon join team switch: ENABLED");
            } else if (Config.PlayersJoinBehaviour == "forced")
            {
                IsQueuedMatchmaking = true;
                logger.LogInformation($"PlayersJoinBehaviour is set to forced, forced team switch enabled");
            }
            
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

    public void OnConfigParsed(AutoTeamBalanceConfig config)
    {
        logger = Logger;
        Config = config;
        if (Config.PlayersJoinBehaviour == null || Config.PlayersJoinBehaviour.Length < 1 || !(Config.PlayersJoinBehaviour == "off" || Config.PlayersJoinBehaviour == "default" || Config.PlayersJoinBehaviour == "forced"))
        {
            Config.PlayersJoinBehaviour = "default";
            logger.LogWarning($"PlayersJoinBehaviour not set in the config, defaulting to 'default'");
        }
        if (Config.TeamCountMaxBehaviour == null || Config.TeamCountMaxBehaviour.Length < 1 || !(Config.TeamCountMaxBehaviour == "none" || Config.TeamCountMaxBehaviour == "spect" || Config.TeamCountMaxBehaviour == "kick"))
        {
            Config.TeamCountMaxBehaviour = "none";
            logger.LogWarning($"TeamCountMaxBehaviour not set in the config, defaulting to 'none'");
        }
        if (Config.BasicPermissions == null || Config.BasicPermissions.Length < 1)
        {
            Config.BasicPermissions = "@css/ban";
            logger.LogWarning($"BasicPermissions not set in the config, defaulting to '@css/ban'");
        }
        if (Config.TeamCountMax < 0 || Config.TeamCountMax > 32 || !Config.TeamCountMax.HasValue)
        {
            Config.TeamCountMax = 5;
            logger.LogWarning($"TeamCountMax not set in the config/set wrong <0-32>, defaulting to 5");
        }
    }
    #endregion
}
