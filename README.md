AutoTeamBalance is cssharp plugin for balancig teams on round start. Based on team counts, plugin will move random player to another team untill team diff will be less or equal to 1.
Another pugin functionality is balancing players on join, they will be forcefully moved to team where there is less players, when counts are the same player will be randomly moved to one team if team menu is disabled (detected with gamerule "IsQueuedMatchmaking").
Players with @css/ban permissions are ignored.

Theres one command available: css_atb (in game !atb - or any other prefix set in your config), @css/ban permission needed.
Output will contains information about plugin version, current team counts that plugin uses for balance and list of players that were moved in that game (list clears on map load).

In current version, no config from file is available.
