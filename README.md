AutoTeamBalance is cssharp plugin for balancig teams on round start. Based on team counts, plugin will move random player to another team untill team diff will be less or equal to 1.
Another pugin functionality is balancing players on join, you can configure its behaviour, more on that under Configuration.
Players with configured permission are ignored.

Configuration: (Available as of v1.1.9, Team counts/behaviour from v1.2.0)
```json
{ //cssharp folder/configs/plugins/AutoTeamBalance/AutoTeamBalance.json
  "PlayersJoinBehaviour": "default", <- Behaviour of a plugin when player connects to a server
  "BasicPermissions": "@css/ban", <- Permission needed for using (css_/!)atb command
  "TeamCountMax": 5, <- max players that can be in team, so plugin won't exceed it
  "TeamCountMaxBehaviour": "none", <- behaviour of plugin when teams are at max capacity
  "ConfigVersion": 1
}
```
PlayersJoinBehaviour can have 3 states:
- off <- completly disables this feature
- default <- players will be forcefully moved to team where there is less players, when counts are the same player will be randomly moved to one team if team menu is disabled (detected with gamerule "IsQueuedMatchmaking")
- forced <- same as default, but wont check gamerule/team menu, will be on

TeamCountMaxBehaviour can have 3 states:
- none <- does nothing with a player (default)
- spect <- moves player to spectators
- kick <- kicks player

Theres one command available: css_atb (in game !atb - or any other prefix set in your config), configured permission needed.
Output will contains information about plugin version, configs, current team counts that plugin uses for balance and list of players that were moved in that game (list clears on map load).
