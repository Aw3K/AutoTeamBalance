AutoTeamBalance is cssharp plugin for balancig teams on round start. Based on team counts, plugin will move random player to another team untill team diff will be less or equal to 1.
Another pugin functionality is balancing players on join, you can configure its behaviour, more on that under Configuration.
Players with configured permission are ignored.

Configuration: (Available as of v1.1.9, Team counts/behaviour from v1.2.0, GameMode/MaxDifference from v1.2.4, EnableScramble/EnableSwitching/TagBasedBalance from v1.2.8)
```json
{ //cssharp folder/configs/plugins/AutoTeamBalance/AutoTeamBalance.json
  "GameMode": "default", <- Type of a game server is currently running
  "MaxDifference": 2, <- Minimum team difference for auto team balance to work (can't be less than 2, will crash server in default GameMode)
  "PlayersJoinBehaviour": "default", <- Behaviour of a plugin when player connects to a server
  "BasicPermissions": "@css/ban", <- Permission needed for using plugin commands
  "IgnorePlayerWithBP": "true", <- Should plugin ignore players with BasicPermissions for balance when they connect
  "TeamCountMax": 5, <- max players that can be in team, so plugin won't exceed it
  "TeamCountMaxBehaviour": "none", <- behaviour of plugin when teams are at max capacity
  "EnableScramble": true, <- Enable/Disable css_scramble command
  "EnableSwitching": true, <- Enable/Disable css_atbsw command
  "TagBasedBalance": false, <- Enable/Disable Tag based team balance
  "ConfigVersion": 1
}
```
GameMode can have 2 states:
- default <- use it for round based game modes
- gungame <- as name suggests, use on game modes where rounds don't end, balance will happen on individual player deaths

PlayersJoinBehaviour can have 3 states:
- off <- completly disables this feature
- default <- players will be forcefully moved to team where there is less players, when counts are the same player will be randomly moved to one team if team menu is disabled (detected with gamerule "IsQueuedMatchmaking")
- forced <- same as default, but wont check gamerule/team menu, will be on

TeamCountMaxBehaviour can have 3 states:
- none <- does nothing with a player (default)
- spect <- moves player to spectators
- kick <- kicks player

TagBasedBalance:
If any of your plugins modifies TAGS on the server (like for teams/vips features etc) and TagBasedBalance is set to true, then plugin won't split players with the same tag when balancing unless it's impossible without doing so.

Theres three commands available: 
- css_atb
Output will contains information about plugin version, configs, current team counts that plugin uses for balance and list of players that were moved/forcefully moved in that game (list clears on map load).

- css_scramble
Players in teams will be randomly scrambled, needs minimum of 4 players total.

- css_atbsw
Forcefully switches players between teams, can switch one or two at the same time, happens BEFORE automatic balance on round start.
