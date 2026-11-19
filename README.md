# Mit Recap

A Dalamud plugin that shows when your party pressed their mitigations and heals during a duty.
It looks like [Death Recap](https://github.com/Kouzukii/ffxiv-deathrecap), but covers the whole duty and not only the moments before a death.

## Use

- In a duty, right-click a party member and pick **Mit Recap**.
- Or type `/mitrecap` at any time. The log of the last duty stays until you enter the next one.

The window lists every mitigation and heal the player used, split by pull, with the time into the pull, the amount healed (with overheal shown apart in grey), the target, the target's HP at that moment and the statuses applied. Deaths show as red rows. Pick **Everyone** to see the whole party in one list.

## What counts

Any action that heals, shields or applies a status and deals no damage. Damage buffs, stances and similar are hidden by default. Right-click an ability in the table to hide it, and use the filter button to bring hidden ones back.

## Mitigated (est.)

For known mitigations the table also shows an estimate of the damage the press prevented. For every hit taken while the mitigation was up, the plugin works out how hard the hit would have landed without any mitigation. Mitigations in this game multiply (20% and 10% together are 28%, not 30%), so when several are active the damage saved by a hit is shared between them by strength.

For a shield the number is the part of each hit the shield absorbed. The game only shows the total shield on a player, so with several shields up the split between them is a guess.

It is an estimate. Blocks, parries, invulnerabilities and damage over time are not counted, and a mitigation the plugin does not know gets no number at all.

Some abilities heal their user with every hit landed while they are up, such as Raw Intuition, Bloodwhetting, Nascent Flash, Bloodbath and Living Dead. Those heals are added to the press, next to its mitigation. Attacks that also heal the party, such as Assize and Pneuma, get a row of their own.

## Not pressed

At the top of the whole duty view, a "Never used" row lists the cooldowns a player did not press a single time in the duty. It is left out when the duty has only one pull, where it would repeat that pull's own row.

Under each pull, a "Not pressed" row shows the icons of the cooldowns a player had ready during that pull and did not use. During a fight it updates live: an icon disappears the moment the cooldown is pressed. It only lists what the player's job has at the level the duty syncs them to. Role actions such as Reprisal and Arm's Length are the exception, because level sync does not take them away: for those it uses the lowest level the player can really be on that job (80 for a Viper, 30 for any job with a soul crystal). A role action above that which the player was seen pressing in the duty is added too, since the press proves they have it. It leaves out a cooldown that was still recharging from an earlier pull, or that only came back in the last 20 seconds. Pulls shorter than 15 seconds are not judged. In a long pull, a cooldown that was pressed but could have been pressed more often is listed in grey with a count, such as 1/3. A DPS is only asked to have used Feint, Addle and their party mitigation at some point in the duty, not in every pull. After a wipe everything counts as ready again, as it is in the game. Invulnerabilities, abilities paid for with a gauge and plain heals are not listed, since holding a heal is often the right call. Arm's Length only counts in a pull where an enemy auto-attacked the player.

## Reporting a problem

The copy button in the toolbar puts the whole duty on your clipboard as text, ready to paste into an issue. Player names are replaced by P1, P2 and so on.

## Sharing a duty

The share button copies a share code for the duty you are looking at. Someone else with Mit Recap copies that code and presses the import button, and the duty shows up in their duty list, marked as imported, exactly as you saw it.

A share code contains character names, so send it to people directly and do not post it in public. For a public bug report, use the copy button instead.

## History

The duty box at the top lists the duties you did since starting the game, newest first. Nothing is saved to disk, so the list is empty again after a restart.

Regen ticks are not listed.

## Credits

Table layout, colours and HP bar are adapted from Death Recap by Kouzukii (MIT).
