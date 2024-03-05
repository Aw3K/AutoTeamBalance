using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
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
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.1.2";
    public static string[] teamNames = new string[] { "TT", "CT" };
    public List<MovedPlayerInfo> MovedPlayers = new List<MovedPlayerInfo>();

    public override void Load(bool hotReload)
    {
        MovedPlayers.Clear();
        RegisterListener<Listeners.OnMapStart>(func => OnMapStartHandle());
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
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (After[tt:" + ttPlayers.Count() + " ct:" + ctPlayers.Count() + "])");
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
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + " [" + player.SteamID + "] to the team: " + player.Team.ToString() + " (After[tt:" + ttPlayers.Count() + " ct:" + ctPlayers.Count() + "])");
            } else { break; }
            ttPlayers = PlayersTeams(CsTeam.Terrorist);
            ctPlayers = PlayersTeams(CsTeam.CounterTerrorist);
            moved.LastSet(new int[2] { ttPlayers.Count(), ctPlayers.Count() }, DateTime.Now.ToString("HH:mm:ss", new CultureInfo("pl_PL")));
            MovedPlayers.Add(moved);
        }
        return HookResult.Continue;
    }
    public HookResult OnplayerConnectFull(EventPlayerConnectFull @event, GameEventInfo @info) {
        var player = @event.Userid;
        if (player == null
            || !player.IsValid
            || player.IsHLTV
            || player.IsBot
            || AdminManager.PlayerHasPermissions(player, "@css/ban")
            || player.Team == CsTeam.Terrorist
            || player.Team == CsTeam.CounterTerrorist) { return HookResult.Continue; }
        if (teamDiffCount() < 0) AddTimer(2.0f, () => { player.ChangeTeam(CsTeam.Terrorist); });
        else if (teamDiffCount() > 0) AddTimer(2.0f, () => { player.ChangeTeam(CsTeam.CounterTerrorist); });
        return HookResult.Continue;
    }
    #endregion

    #region functions
    private int teamDiffCount()
    {
        var ttPlayers = PlayersTeams(CsTeam.Terrorist).Count();
        var ctPlayers = PlayersTeams(CsTeam.CounterTerrorist).Count();
        return ttPlayers - ctPlayers;
    }
    public void OnMapStartHandle() {
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
