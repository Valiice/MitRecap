# Mit Recap

[![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Valiice/MitRecap/total?style=flat-square&color=blue)](https://github.com/Valiice/MitRecap/releases)
[![GitHub Release](https://img.shields.io/github/v/release/Valiice/MitRecap?include_prereleases&style=flat-square&color=green)](https://github.com/Valiice/MitRecap/releases/latest)
[![GitHub License](https://img.shields.io/github/license/Valiice/MitRecap?style=flat-square&color=orange)](LICENSE.md)
[![GitHub Issues](https://img.shields.io/github/issues/Valiice/MitRecap?style=flat-square)](https://github.com/Valiice/MitRecap/issues)
[![Download Latest Zip](https://img.shields.io/badge/Download-Latest%20Zip-blue?style=flat-square)](https://github.com/Valiice/MitRecap/releases/latest/download/MitRecap.zip)

See what your party pressed, and what they left unpressed.

Mit Recap records every mitigation and heal your party uses in a duty and lists them pull by pull. It looks like [Death Recap](https://github.com/Kouzukii/ffxiv-deathrecap), but covers the whole duty instead of only the moments before a death.

## Installation

1. Add my custom plugin repository: [https://raw.githubusercontent.com/Valiice/DalamudPluginRepo/master/repo.json](https://github.com/Valiice/DalamudPluginRepo)
2. Search for **Mit Recap** in the Plugin Installer.
3. Install and Enable.

Not on the official repo. Alternatively, build with `dotnet build MitRecap/MitRecap.csproj -c Release` and load `MitRecap/bin/Release/MitRecap` as a dev plugin.

## Usage

- In a duty, right-click a party member and pick **Mit Recap**.
- `/mitrecap` opens the window at any time.

Pick a player at the top, or **Everyone** for the whole party in one list. The list on the left jumps to a single pull.

## What you see

Each press is one row:

- **Time** into the duty. Hover it for the time into the pull.
- **Healed**, with overheal in grey next to it.
- **Mitigated (est.)**, an estimate of the damage that press prevented. Shields count what they absorbed.
- **Target** and their **HP** when the press landed. An area heal shows one bar per player.
- **Status effects** the press applied.

Deaths are red rows that name the hit that killed the player. Hover one for the last hits they took.

Damage buffs, stances and similar are hidden by default. Right-click an ability to hide it, and use the filter button to bring hidden ones back.

## Not pressed

Under each pull, a **Not pressed** row shows the mitigations a player had ready in that pull and did not use. It updates during the fight.

- **Could press more** lists cooldowns that were used, but less often than they were ready, with a count such as 1/3.
- **Never used**, at the top, lists what a player did not press once in the whole duty.

Mit Recap shows what happened. Whether a press was needed is for you to decide. To keep the list fair it leaves out:

- anything the player's job does not have at the level the duty syncs them to
- cooldowns that were still recharging, or only came back in the last 20 seconds of the pull
- pulls shorter than 15 seconds
- plain heals, invulnerabilities and abilities that cost gauge
- Arm's Length, unless an enemy was auto-attacking the player
- Feint, Addle and DPS party mitigation in individual pulls. Those are only listed under Never used.

## Sharing

- **Copy** puts the whole duty on your clipboard as text, with player names replaced by P1, P2 and so on. Use this for bug reports.
- **Share** copies a code with the whole duty, character names included. Someone else can **Import** it in Mit Recap, or paste it at [valiice.github.io/MitRecap](https://valiice.github.io/MitRecap/) to view it in a browser without the game. The site reads the code in the browser and uploads nothing.

The box at the top lists the duties you did since starting the game. Nothing is saved to disk, so the list is empty again after a restart.

## Limitations

- The mitigated number is an estimate. Blocks, parries, invulnerabilities and damage over time are not counted.
- With several shields on one player, the split between them is a guess, because the game only shows the total.
- Regen ticks are not listed.
- Another player's real level is not known inside a duty, only the synced one. Role actions such as Reprisal and Arm's Length survive level sync, so for those Mit Recap goes by the lowest level the job can be, and adds any the player was seen pressing.

## Privacy

Mit Recap only reads what the game already sent to your client. Nothing is requested from the server and nothing leaves your machine unless you copy it yourself.

## Credits

Table layout, colours and HP bar are adapted from [Death Recap](https://github.com/Kouzukii/ffxiv-deathrecap) by Kouzukii (MIT). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## License & Copyright

Copyright © 2026 Valiice

Mit Recap is licensed under the GNU Affero General Public License v3.0; see [LICENSE](LICENSE.md) for details. You are free to use, modify, and redistribute the source code, provided derivative works keep the same license, credit this project with a link back to [https://github.com/Valiice/MitRecap](https://github.com/Valiice/MitRecap), and retain this copyright notice.
