# Smart Steward

![Smart Steward: the Party Steward window, its Deal all button ringed in gold - food, horses, troops, prisoners handled in one click](Screenshots/cover.jpg)

**Automates your party's logistics. Ride into a town and the steward works out what you need — food, horses, troops, prisoners, loot — and does it all with one click (or by itself, if you let it).**

A mod for Mount & Blade II: Bannerlord.

Made by two of us: the code and all technical work by Claude (Opus 5.5), an AI; the idea, feedback and
playtesting by [Trax](https://github.com/TraxData313), Bible believer and engineer in AI/ML/Python/Applied Maths.

It takes the logistics chores off your hands. Ride into a town, village or castle and your **Party Steward** shows,
in one table, what the party needs. Nudge any row (click ±1, Shift ±5, Ctrl all), then **Deal all**, or **Deal**
just one line.

- **Food**: enough for everyone, varied for morale, surplus sold
- **Horses**: pack animals, a horse for every footman, the war horses you want kept; noble horses sold, lame ones replaced
- **Troops**: recruit what the notables offer, dismiss the lowest tiers; hire wanderers and mercenaries from the tavern
- **Prisoners**: ransomed, or donated to your kingdom's dungeons for influence (castles too)
- **Loot**: armour and weapons sold in bulk groups (off until you turn it on)
- **Goals**: type your own number for any food or horse row; it holds in every town. What your quests need is kept
- **Full-autonomous steward** (off by default): does it all on arrival, never below your gold floor

Every number is a setting: in the window, in MCM, or in a commented `settings.json`.
No Harmony, and nothing is stored in your save, so you can add or remove it mid-campaign.

## Install

1. Download the zip from the [GitHub Releases page](https://github.com/TraxData313/smart_steward/releases/latest),
   or subscribe on the [Steam Workshop](STEAM_WORKSHOP_URL).
2. Extract it into `Mount & Blade II Bannerlord\Modules\`.
3. Enable it in the launcher.

For Bannerlord v1.4.8 (War Sails fine, not needed). Nothing is required.
[Mod Configuration Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=2859238197) is optional.

![The Troops and Horses parts of the table](Screenshots/02_suggestion_troops_horses.jpg)

![The Instructions tab: every standing order, saved at once](Screenshots/03_instructions_general_money.jpg)

Free and open — public domain ([Unlicense](LICENSE)). Do whatever you like with it.

To thank me, open [my GitHub profile](https://github.com/TraxData313) and read my top pinned.

<details>
<summary>How it works</summary>

Building, the tools, the design docs and translations are in [docs/TECHNICAL.md](docs/TECHNICAL.md).
The full specification is [docs/DESIGN.md](docs/DESIGN.md).

</details>
