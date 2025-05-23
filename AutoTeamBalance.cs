﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
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
        if (playerAfter != null) { this.teams![1] = teamNames[playerAfter.TeamNum - 2]; }
        this.teamCountAfter = (int[]?)teamCountAfter.Clone();
        this.timeOfSwitch = time;
    }
}

public class SwitchedWithCMDPlayer {
    public string? adminName { get; set; }
    public string? playerName { get; set; }
    public string? timeOfSwitch { get; set; }
    public string? teamName { get; set; }

    public SwitchedWithCMDPlayer(string? adminName, string? playerName, string? timeOfSwitch, string? teamName)
    {
        this.adminName = adminName;
        this.playerName = playerName;
        this.timeOfSwitch = timeOfSwitch;
        this.teamName = teamName;
    }
}

public class AutoTeamBalanceConfig : BasePluginConfig
{
    [JsonPropertyName("GameMode")] public string GameMode { get; set; } = new("");
    [JsonPropertyName("MaxDifference")] public int? MaxDifference { get; set; }
    [JsonPropertyName("PlayersJoinBehaviour")] public string PlayersJoinBehaviour { get; set; } = new("");
    [JsonPropertyName("BasicPermissions")] public string BasicPermissions { get; set; } = new("");
    [JsonPropertyName("IgnorePlayerWithBP")] public string IgnorePlayerWithBP { get; set; } = new("");
    [JsonPropertyName("TeamCountMax")] public int? TeamCountMax { get; set; }
    [JsonPropertyName("EnableScramble")] public bool EnableScramble { get; set; } = true;
    [JsonPropertyName("EnableSwitching")] public bool EnableSwitching { get; set; } = true;
    [JsonPropertyName("TagBasedBalance")] public bool TagBasedBalance { get; set; } = false;

    [JsonPropertyName("TeamCountMaxBehaviour")] public string TeamCountMaxBehaviour { get; set; } = new("");
}

public class AutoTeamBalance : BasePlugin, IPluginConfig<AutoTeamBalanceConfig>
{
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.2.8";

    public AutoTeamBalanceConfig Config { get; set; } = new();

    public static string[] teamNames = new string[] { "TT", "CT" };
    public bool IsQueuedMatchmaking = false;
    public Random rand = new Random();
    public required ILogger logger;
    public List<MovedPlayerInfo> MovedPlayers = new List<MovedPlayerInfo>();
    public List<CCSPlayerController> playersToMove = new List<CCSPlayerController>();
    public List<SwitchedWithCMDPlayer> SwitchedPlayers = new List<SwitchedWithCMDPlayer>();

    public override void Load(bool hotReload)
    {
        MovedPlayers.Clear();
        playersToMove.Clear();
        SwitchedPlayers.Clear();
        logger = Logger;
        logger.LogInformation($"Plugin version: {ModuleVersion}");
        RegisterListener<Listeners.OnMapStart>(OnMapStartHandle);
    }

    #region Commands
    [ConsoleCommand("css_atbsw", "Switch players teams")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnATBSwitchCommand(CCSPlayerController? player, CommandInfo command) {
        if (player == null) return;
        if (!AdminManager.PlayerHasPermissions(player, Config.BasicPermissions))
        {
            player!.PrintToChat(Localizer["NoPermissions"]);
            return;
        }
        if (!Config.EnableSwitching)
        {
            player!.PrintToChat("[\u0004AutoTeamBalance\u0001] \u0007Team switching disabled in config.");
            return;
        }
        CenterHtmlMenu menu = new("Team Switch", this);
        CenterHtmlMenu menuMoveOne = new("Move One Player", this);
        CenterHtmlMenu menuSwitchOne = new("Select First Player", this);
        CenterHtmlMenu menuSwitchTwo = new("Select Secound Player", this);

        menu.AddMenuOption("Move One", (player, option) => {
            MenuManager.CloseActiveMenu(player);
            foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && (p.Team == CsTeam.Terrorist || p.Team == CsTeam.CounterTerrorist)))
            {
                menuMoveOne.AddMenuOption(p.PlayerName + "[" + ((p.Team == CsTeam.Terrorist) ? "TT" : "CT") + "]", (MOplayer, MOoption) => {
                    if (playersToMove.Contains(p))
                    {
                        MenuManager.CloseActiveMenu(player);
                        player.PrintToChat($"[\u0004AutoTeamBalance\u0001] Player {p.PlayerName} already marked for TeamSwitch on round end.");
                    }
                    else
                    {
                        playersToMove.Add(p);
                        SwitchedPlayers.Add(new SwitchedWithCMDPlayer(formatPlayerName(player.PlayerName, 13), formatPlayerName(p.PlayerName, 13), DateTime.Now.ToString("HH:mm:ss", new CultureInfo("pl_PL")), $"{teamNames[p.TeamNum - 2]} \u0004--> \u0007 " + ((teamNames[p.TeamNum - 2] == "CT") ? "TT" : "CT")));
                        MenuManager.CloseActiveMenu(player);
                        player.PrintToChat($"[\u0004AutoTeamBalance\u0001] Marked {p.PlayerName} for TeamSwitch on round end.");
                    }
                });
            }
            menuMoveOne.Open(player);
        });
        menu.AddMenuOption("Switch", (player, option) => {
            MenuManager.CloseActiveMenu(player);
            foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.Team == CsTeam.Terrorist))
            {
                menuSwitchOne.AddMenuOption(p.PlayerName + "[" + ((p.Team == CsTeam.Terrorist) ? "TT" : "CT") + "]", (SOplayer, SOoption) => {
                    if (playersToMove.Contains(p))
                    {
                        MenuManager.CloseActiveMenu(player);
                        player.PrintToChat($"[\u0004AutoTeamBalance\u0001] Player {p.PlayerName} already marked for TeamSwitch on round end.");
                    }
                    else
                    {
                        playersToMove.Add(p);
                        SwitchedPlayers.Add(new SwitchedWithCMDPlayer(formatPlayerName(player.PlayerName, 13), formatPlayerName(p.PlayerName, 13), DateTime.Now.ToString("HH:mm:ss", new CultureInfo("pl_PL")), $"{teamNames[p.TeamNum - 2]} \u0004--> \u0007 " + ((teamNames[p.TeamNum - 2] == "CT") ? "TT" : "CT")));
                        MenuManager.CloseActiveMenu(player);
                        player.PrintToChat($"[\u0004AutoTeamBalance\u0001] Marked {p.PlayerName} for TeamSwitch on round end.");
                        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.Team == CsTeam.CounterTerrorist))
                        {
                            menuSwitchTwo.AddMenuOption(p.PlayerName + "[" + ((p.Team == CsTeam.Terrorist) ? "TT" : "CT") + "]", (STplayer, SToption) =>
                            {
                                if (playersToMove.Contains(p))
                                {
                                    MenuManager.CloseActiveMenu(player);
                                    player.PrintToChat($"[\u0004AutoTeamBalance\u0001] Player {p.PlayerName} already marked for TeamSwitch on round end.");
                                }
                                else
                                {
                                    playersToMove.Add(p);
                                    SwitchedPlayers.Add(new SwitchedWithCMDPlayer(formatPlayerName(player.PlayerName, 13), formatPlayerName(p.PlayerName, 13), DateTime.Now.ToString("HH:mm:ss", new CultureInfo("pl_PL")), $"{teamNames[p.TeamNum - 2]} \u0004--> \u0007 " + ((teamNames[p.TeamNum - 2] == "CT") ? "TT" : "CT")));
                                    MenuManager.CloseActiveMenu(player);
                                    player.PrintToChat($"[\u0004AutoTeamBalance\u0001] Marked {p.PlayerName} for TeamSwitch on round end.");
                                }
                            });
                        }
                        menuSwitchTwo.Open(player);
                    }
                });
            }
            menuSwitchOne.Open(player);
        });
        menu.Open(player);
    }

    [ConsoleCommand("css_atb", "Status of AutoTeamBalance Plugin.")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnATBCommand(CCSPlayerController? player, CommandInfo command) {
        if (!AdminManager.PlayerHasPermissions(player, Config.BasicPermissions)) {
            player!.PrintToChat(Localizer["NoPermissions"]);
            return;
        }
        command.ReplyToCommand($"[\u0004AutoTeamBalance\u0001]");
        command.ReplyToCommand($" \u0004Other commands\u0001: atbsw (switches players) | atbsc (scrambles teams)");
        command.ReplyToCommand($" \u0004Plugin Version\u0001: {ModuleVersion}");
        command.ReplyToCommand($" \u0004Plugin Config\u0001: {Config.BasicPermissions} \u0004| \u0001{Config.GameMode} \u0004| \u0001{Config.PlayersJoinBehaviour} \u0004| \u0001{Config.TeamCountMax} \u0004| \u0001{Config.TeamCountMaxBehaviour} \u0004| \u0001{Config.EnableScramble} \u0004| \u0001{Config.EnableSwitching} \u0004| \u0001{Config.TagBasedBalance}");
        command.ReplyToCommand($" \u0004Current known TeamCounts\u0001:  {PlayersTeams(CsTeam.CounterTerrorist).Count()} \u0007CTs \u0001| {PlayersTeams(CsTeam.Terrorist).Count()} \u0007TTs");
        command.ReplyToCommand($" \u0004Current know Players after balance\u0001: ");
        foreach (var moved in MovedPlayers)
        {
            command.ReplyToCommand($" \u0004|> \u0001{moved.timeOfSwitch} \u0004[\u0001{moved.playerName}\u0004] - \u0007{moved.teams![0]} \u0004--> \u0007{moved.teams[1]} \u0004- \u0001TeamCount: \u0004[\u0001{moved.teamCountBefore![1]} \u0007CTs \u0001| {moved.teamCountBefore[0]} \u0007TTs\u0004] \u0004--> [\u0001{moved.teamCountAfter![1]} \u0007CTs \u0001| {moved.teamCountAfter[0]} \u0007TTs\u0004]>");
        }
        if (MovedPlayers.Count() == 0) { command.ReplyToCommand(" \u0007 List is empty."); }
        command.ReplyToCommand($" \u0004Switched Players by Admins\u0001: ");
        foreach (var switched in SwitchedPlayers)
        {
            command.ReplyToCommand($" \u0004|> \u0001{switched.timeOfSwitch} \u0004[\u0001{switched.playerName}\u0004] - \u0007{switched.teamName} \u0004- BY:[\u0007{switched.adminName}\u0004]>");
        }
        if (SwitchedPlayers.Count() == 0) { command.ReplyToCommand(" \u0007 List is empty."); }
        command.ReplyToCommand($"[\u0001/\u0004AutoTeamBalance\u0001]");
    }
    [ConsoleCommand("css_atbsc", "Scramble teams.")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnATBScrambleCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!AdminManager.PlayerHasPermissions(player, Config.BasicPermissions))
        {
            player!.PrintToChat(Localizer["NoPermissions"]);
            return;
        }
        if (!Config.EnableScramble)
        {
            player!.PrintToChat("[\u0004AutoTeamBalance\u0001] \u0007Team scramble disabled in config.");
            return;
        }
        var result = ScrambleTeams();
        if (result < 4) command.ReplyToCommand(Localizer["CantScramble"]);
        else Server.PrintToChatAll(Localizer["TeamsScrambled"]);
    }
    #endregion

    #region Events
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info) {
        if (playersToMove.Count() > 0) {
            foreach (var player in playersToMove) {
                if (player != null && player.IsValid) {
                    if (player.Team == CsTeam.Terrorist) player.SwitchTeam(CsTeam.CounterTerrorist);
                    else if (player.Team == CsTeam.CounterTerrorist) player.SwitchTeam(CsTeam.Terrorist);
                    player.PrintToChat(Localizer["ForcedChangedTeam"]);
                    logger.LogInformation($"Moving {player.PlayerName} with css_atbsw command to: {player.Team.ToString()}");
                }
            }
            playersToMove.Clear();
        }
        if (Config.GameMode != "default") return HookResult.Continue;
        var ttPlayers = PlayersTeams(CsTeam.Terrorist);
        var ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
        while (Math.Abs(ttPlayers.Count() - ctPlayers.Count()) >= Config.MaxDifference)
        {
            MovedPlayerInfo moved = new MovedPlayerInfo();
            if (ttPlayers.Count() > ctPlayers.Count())
            {
                var player = getPlayerForSwitch(ttPlayers);
                if (player == null) {
                    logger.LogWarning($"Couldn't move player [NULL] (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
                    return HookResult.Continue;
                }
                if (player.Team == CsTeam.Terrorist && player.Connected == PlayerConnectedState.PlayerConnected && player.IsValid)
                {
                    moved.FirstSet(player.PlayerName, player.SteamID, teamNames[player.TeamNum - 2], new int[2] { ttPlayers.Count(), ctPlayers.Count() });
                    player.SwitchTeam(CsTeam.CounterTerrorist);
                    player.PrintToChat(Localizer["ForcedChangedTeam"]);
                    logger.LogInformation($"Moved {player.PlayerName} [{player.SteamID}] to the team: {player.Team.ToString()} (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
                } else logger.LogWarning($"Couldn't move {player.PlayerName} [{player.SteamID}] (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
            }
            else if (ctPlayers.Count() > ttPlayers.Count())
            {
                var player = getPlayerForSwitch(ctPlayers);
                if (player == null)
                {
                    logger.LogWarning($"Couldn't move player [NULL] (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
                    return HookResult.Continue;
                }
                if (player.Team == CsTeam.CounterTerrorist && player.Connected == PlayerConnectedState.PlayerConnected && player.IsValid)
                {
                    moved.FirstSet(player.PlayerName, player.SteamID, teamNames[player.TeamNum - 2], new int[2] { ttPlayers.Count(), ctPlayers.Count() });
                    player.SwitchTeam(CsTeam.Terrorist);
                    player.PrintToChat(Localizer["ForcedChangedTeam"]);
                    logger.LogInformation($"Moved {player.PlayerName} [{player.SteamID}] to the team: {player.Team.ToString()} (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
                } else logger.LogWarning($"Couldn't move {player.PlayerName} [{player.SteamID}] (After[tt:{ttPlayers.Count()}|ct:{ctPlayers.Count()}])");
            } else { break; }
            ttPlayers = PlayersTeams(CsTeam.Terrorist);
            ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
            moved.LastSet(new int[2] { ttPlayers.Count(), ctPlayers.Count() }, DateTime.Now.ToString("HH:mm:ss", new CultureInfo("pl_PL")));
            MovedPlayers.Add(moved);
        }
        return HookResult.Continue;
    }
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo @info) {
        if (Config.GameMode != "gungame") return HookResult.Continue;
        var player = @event.Userid;
        if (player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsBot) {
            var ttPlayers = PlayersTeams(CsTeam.Terrorist);
            var ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
            if (player.Team == CsTeam.Terrorist && (ttPlayers.Count() - ctPlayers.Count()) >= Config.MaxDifference)
            {
                player.SwitchTeam(CsTeam.CounterTerrorist);
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
            }
            else if (player.Team == CsTeam.CounterTerrorist && (ctPlayers.Count() - ttPlayers.Count()) >= Config.MaxDifference)
            {
                player.SwitchTeam(CsTeam.Terrorist);
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
            }
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
            || player.IsBot){
                return;
            }

            if(Config.IgnorePlayerWithBP == "true" && AdminManager.PlayerHasPermissions(player, Config.BasicPermissions))
            {
                logger.LogInformation($"[EventPlayerConnectFull][{player!.PlayerName}] ignored for on join balance, have '{Config.BasicPermissions}' permission");
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
    public static string formatPlayerName(string input, int maxLength)
    {
        const char replacementChar = '□';
        bool firstHighCharFound = false;
        char[] modifiedChars = input.Select(c =>
        {
            if ((int)c > 4096)
            {
                if (!firstHighCharFound)
                {
                    firstHighCharFound = true;
                    return c;
                }
                return replacementChar;
            }
            return c;
        }).ToArray();
        return new string(modifiedChars).Substring(0, Math.Min(modifiedChars.Length, maxLength));
    }
    private int ScrambleTeams()
    {
        List<CCSPlayerController> tmp = new List<CCSPlayerController>();
        tmp.Clear();
        tmp.AddRange(PlayersTeams(CsTeam.CounterTerrorist));
        tmp.AddRange(PlayersTeams(CsTeam.Terrorist));
        var playersScrambled = tmp.Count();
        if (playersScrambled < 4) return tmp.Count();
        logger.LogInformation($"[AutoTeamBalance] Scramble");
        while (tmp.Count() > 0)
        {
            var selected = tmp[rand.Next(0, tmp.Count())];
            if (selected != null && selected.IsValid)
            {
                CsTeam team = (CsTeam)((tmp.Count() % 2) + 2);
                selected.SwitchTeam(team);
                logger.LogInformation($" {selected.PlayerName} {selected.UserId} -> {team.ToString()}");
                tmp.Remove(selected);
            }
        }
        logger.LogInformation($"[/AutoTeamBalance]");
        return playersScrambled;
    }
    private CCSPlayerController? getPlayerForSwitch(List<CCSPlayerController> list) {
        if (Config.TagBasedBalance)
        {
            Dictionary<string, int> clansCount = new Dictionary<string, int>();
            foreach (var player in list)
            {
                if (player.IsValid && player.Clan != null && player.Clan.Length > 0)
                {
                    if (clansCount.ContainsKey(player.Clan)) clansCount[player.Clan]++;
                    else clansCount[player.Clan] = 1;
                }
            }
            if (clansCount.Count() > 0 && clansCount.Values.Any(value => value >= 2) && clansCount.Values.Where(value => value >= 2).Sum() != list.Count())
            {
                foreach (var player in list)
                {
                    if (player.IsValid)
                    {
                        if (player.Clan == null || player.Clan.Length < 3) return player;
                        if (player.Clan != null && player.Clan.Length > 0 && clansCount[player.Clan] < 2) return player;
                    }
                }
            }
            else
            {
                Random random = new Random();
                return list[random.Next(list.Count())];
            }
        }
        else {
            Random random = new Random();
            return list[random.Next(list.Count())];
        }
        return null;
    }
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
        SwitchedPlayers.Clear();
        playersToMove.Clear();
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
        if (Config.GameMode == null || Config.GameMode.Length < 1 || !(Config.GameMode == "default" || Config.GameMode == "gungame"))
        {
            Config.GameMode = "default";
            logger.LogWarning($"GameMode not set in the config, defaulting to 'default'");
        }
        if (Config.MaxDifference < 2 || !Config.MaxDifference.HasValue)
        {
            Config.MaxDifference = 2;
            logger.LogWarning($"MaxDifference not set in the config/set wrong <2-oo>, defaulting to 2");
        }
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
        if (Config.IgnorePlayerWithBP == null || Config.IgnorePlayerWithBP.Length < 1 || !(Config.IgnorePlayerWithBP == "true" || Config.IgnorePlayerWithBP == "false"))
        {
            Config.IgnorePlayerWithBP = "true";
            logger.LogWarning($"IgnorePlayerWithBP not set in the config, defaulting to 'true'");
        }
        if (Config.TeamCountMax < 0 || Config.TeamCountMax > 32 || !Config.TeamCountMax.HasValue)
        {
            Config.TeamCountMax = 5;
            logger.LogWarning($"TeamCountMax not set in the config/set wrong <0-32>, defaulting to 5");
        }
    }
    #endregion
}
