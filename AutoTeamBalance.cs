using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;

namespace AutoTeamBalance;

public class AutoTeamBalance : BasePlugin
{
    public override string ModuleName => "AutoTeamBalance";
    public override string ModuleAuthor => "NyggaBytes";
    public override string ModuleVersion => "1.0.0";
    public bool isWarmup = false;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundAnnounceWarmup>(OnWarmup);
    }

    #region Events
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;

        if (
            player.IsHLTV
         || player == null
         || !player.IsValid
         || player.Connected != PlayerConnectedState.PlayerConnected
         || player.SteamID.ToString().Length != 17
         || isWarmup == true
         || @event.Weapon == "world"
        ) return HookResult.Continue;

        Server.PrintToConsole(@event.Weapon);

        var ttList = getPlayersPerTeam("Terrorist");
        var ctList = getPlayersPerTeam("CounterTerrorist");
        if (Math.Abs(ttList.Count() - ctList.Count()) >= 2)
        {
            if (ttList.Count() > ctList.Count()) {
                if (ttList.Contains(player)) { player.SwitchTeam(player.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist); }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + "[" + player.SteamID + "] to the team: " + player.Team.ToString() + " (Before tt:" + ttList.Count() + " ct:" + ctList.Count() + ")");
            }
            else if (ctList.Count() > ttList.Count()) {
                if (ctList.Contains(player)) { player.SwitchTeam(player.Team == CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist); }
                player.PrintToChat(Localizer["ForcedChangedTeam"]);
                Server.PrintToConsole("[AutoTeamBalance]: Moved " + player.PlayerName + "[" + player.SteamID + "] to the team: " + player.Team.ToString() + " (Before tt:" + ttList.Count() + " ct:" + ctList.Count() + ")");
            }
        }

        return HookResult.Continue;
    }
    private HookResult OnWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
    {
        if (@event.EventName == "round_announce_warmup") isWarmup = true;
        AddTimer((float)(Math.Round(ConVar.Find("mp_warmuptime")!.GetPrimitiveValue<float>())), () => {
            isWarmup = false;
        });
        return HookResult.Continue;
    }
    #endregion

    #region Functions
    private List<CCSPlayerController> getPlayersPerTeam(string team)
    {
        var allPlayers = Utilities.GetPlayers().Where(p =>
                    p.IsValid
                    && !p.IsHLTV
                    && !p.IsBot
                    && p.Connected == PlayerConnectedState.PlayerConnected
                    && p.SteamID.ToString().Length == 17
                    && p.Team.ToString() == team);
        return allPlayers.ToList();
    }
    #endregion
}
