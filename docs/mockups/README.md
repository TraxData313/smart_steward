# Suggestion tab v2 — the round-4 mockup (PLAN step 19)

## APPROVED by Anton 2026.09.28

Anton approved the mockup on 2026.09.28 ("beautiful"): all ten choices below stand, with **two changes** (relayed by the manager,
recorded here and in DESIGN §1.1; the PNGs are NOT re-rendered — step 21 builds from this file and DESIGN):

1. **"Souls" → "Party", plus a new "Prisoners" column.** Party counts party MEMBERS only — prisoners do not change it; the
   prisoner-count change has its own column, per row, section and Total. Ransoming 50 = Party 0 / Prisoners −50; recruiting 4 =
   Party +4 / Prisoners 0. The Total line reads `party 103 » 104 · prisoners 52 » 0`. The food eaters are unchanged (prisoners
   still eat half) — only the displayed metrics change. (Core: `PlanMetrics.Party` / `.Prisoners`, `SuggestionSheet.TotalText`.)
2. **The Market column moves to the far LEFT** ("in the game the market is always on the left"): Market · Item · Mine · Change ·
   Result · Denari · Party · Prisoners · Land kg · Sea kg. A window layout matter for step 21.

**BUILT in PLAN step 21 (2026.09.28)** — the window follows this file; where it differs (and why) is in DESIGN §1.1 "As built".

Anton also said the final may differ a bit and more polish rounds will follow — the Core model (step 20) is built to be easy
to adjust.

---

Anton: look at `suggestion_v2_folded.png` (the everyday view, the real window size) and `suggestion_v2_expanded.png`
(everything open, the table unrolled), or open `suggestion_v2.html` in a browser and click the section lines. Then answer
the list below in one reply — "all fine", or the numbers you want changed.

## Choices for Anton

1. **Columns** (px of the 1580 window): Item 446 · Mine 84 · Change 224 · Result 84 · Market 90 · Denari 230 · Souls 90 ·
   Land kg 120 · Sea kg 120. Item first, as in the Prices tab.
2. **Unit price** = small grey text after the name ("510 each", "444 to hire"); hover the Denari cell for the sum and your
   limit ("8 × 30–33 = –252 · your max 60"). The cell shows only the total.
3. **A section's title line is its subtotal**: name, stats, and its Denari · Souls · Land · Sea sums. Folded = that line plus
   the lines that always stay (the 4 troop lines, Lords, Others). Total + the weight table are pinned and never scroll.
4. **Influence** = small green text left of the denari number (header, Prisoners, Others, dungeon rows, Total), so the
   numbers stay aligned.
5. ~~**Souls** = mouths (men + prisoners); lines show the change, Total says "souls 155 » 104".~~ **Changed at the approval:
   Party (members only) + Prisoners** — see above. Party, Prisoners and kg are plain white — red only past a limit (104/101, the
   footer's left / slowdown).
6. **Colours**: denari and influence green in, red out; Change green +, red –; zeros left blank in the number columns.
7. **Keep | Ransom | Donate** = three small buttons in the Change column, the chosen one lit gold (the game's plain button,
   its Selected look). Donate greys out where the game forbids it.
8. **Troops**: Recruits only hire (best tier first), Your troops only dismiss (lowest tier first, wounded first; [+] re-adds
   in reverse) — a type you have that is also on offer shows in both. The Troops title opens / closes both groups.
9. **"7/8 kinds"** = kinds you will hold / kinds you could hold here (yours + the market's). "~42 days": the game's font has
   no "≈".
10. **Fit**: the window stays 1580 × 960; rows 32 px (were 36). The everyday view shows every section's line; Other's 5
    lines scroll. I also folded Recruits there (not asked) to make it fit. The sea row's speed icon is War Sails' ship via
    the game's own brush — we never name a DLC sprite.

## What is here

| File | What |
|---|---|
| `suggestion_v2.html` | The mockup: self-contained HTML/CSS + a tiny fold script. No hash = the everyday view; `#expanded` = all open. |
| `gen_suggestion_v2.py` | Writes the HTML from ONE table of numbers and asserts they add up (denari +19,831, land –318, sea –68, souls –51). Edit the numbers there, not in the HTML. |
| `suggestion_v2_folded.png` | Food, Recruits, Your troops and Prisoners' details folded; Horses and Other open. 1640 × 1020. |
| `suggestion_v2_expanded.png` | Everything open; a red dashed line marks where the real window's table ends (it scrolls from there). |

Re-render both PNGs (headless Edge, as `make_thumbnail.py`): `powershell -ExecutionPolicy Bypass -File tools\render-mockup.ps1`.

## The deal drawn (Lycaron, from the round-4 screenshot; the rest invented to fit)

Denari 69,358 » 89,189 (+19,831) and +11.2 influence; party 103 → 104 of 101 (4 Hired Pike hired, 2 T0 Empire Peasant and
1 T1 Imperial Recruit dismissed); Temeon the Shipwright not hired; Imperial Recruit (15) and Imperial Vigla Recruit (3) on
offer; food 188 » 200 (Olives and Date Fruit bought for variety) ~32 » 42 days; 92 men on foot → 101 horses to keep (10 war);
52 prisoners (2 lords ransomed, 10 to a full dungeon, 40 ransomed); 36 loot pieces + 13 goods sold; load land 2,348 » 2,030
of 6,305, sea 7,898 » 7,830 of 13,990 (at sea each horse weighs 50 kg, each pack animal 30, each rider's horse 50 —
RESEARCH §19), no slowdown.

## Buildable in Gauntlet — what each piece is made of

Everything drawn is a piece the window already uses (`tools\check-gui.ps1` verifies them): fixed-width `TextWidget`s in a
horizontal `ListPanel` per row (the grid), `ButtonSimpleBrush` for `[–] [+]` and the three toggle buttons (it has a Selected
style), `RefreshButton.Flat` for ⟲, the SPOptions collapser sprites for ▸ / ▾, `BlankWhiteSquare_9` bands and rules,
`HintWidget` tooltips, wrapper widgets carrying the fold (step 18). The pinned Total and weight table sit outside the
`ScrollablePanel`, like today's footer. New art: the Land slowdown icon `General\Icons\Speed@2x` (Native, `ui_group1`, always
loaded — the party bar's horse); the Sea one through the vanilla brush `Map.Party.Speed.Indicator` in its `Sailing` state
(`Map\ship_speed`, War Sails' always-loaded `ui_naval_common` — the sea row only shows with ships, i.e. with War Sails). The
fonts are stand-ins: the game draws Galahad (titles) and FiraSansExtraCondensed (text), which carry `– × · » ±` but not `≈`.
Nothing here needs CSS-only tricks: no wrapping cells, no variable row heights, no gradients inside the table.
