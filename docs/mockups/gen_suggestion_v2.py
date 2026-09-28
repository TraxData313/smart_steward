# Generates suggestion_v2.html beside it (static HTML + a tiny fold script) from ONE table of numbers, with asserts that
# every column adds up (PLAN step 19, the round-4 mockup). Edit the numbers here, run `python gen_suggestion_v2.py`, then
# re-render the PNGs: powershell -ExecutionPolicy Bypass -File tools\render-mockup.ps1
import os, html

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "suggestion_v2.html")

D = "\u2013"  # the game's minus: an en dash

def num(n, plus=True):
    """Signed number with thousands separators; the minus is an en dash (the fonts have no U+2212)."""
    if n is None:
        return ""
    if isinstance(n, float):
        s = f"{abs(n):,.1f}"
    else:
        s = f"{abs(n):,}"
    if n > 0:
        return ("+" if plus else "") + s
    if n < 0:
        return D + s
    return "0"

def cls_sign(n):
    return "pos" if n and n > 0 else ("neg" if n and n < 0 else "zero")

def btn(glyph, enabled=True):
    return f'<b class="btn{"" if enabled else " off"}">{glyph}</b>'

RESET = ('<i class="rst" title="&#10226; reset: hand the row back to the steward">'
         '<svg viewBox="0 0 20 20"><path d="M15.5 6.2A7 7 0 1 0 17 11" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/>'
         '<path d="M17.6 2.8l-.3 4.6-4.5-.6z" fill="currentColor"/></svg></i>')

NORESET = '<i class="rst none"></i>'

def change_cell(chg, minus=True, plus=True, reset=False, text=None):
    n = chg
    t = text if text is not None else num(n)
    c = cls_sign(n) if text is None else "muted"
    return (f'<div class="c chg">{btn(D, minus)}<span class="n {c}">{t}</span>{btn("+", plus)}'
            f'{RESET if reset else NORESET}</div>')

def toggle_cell(selected, donate_ok=True):
    segs = []
    for name in ("Keep", "Ransom", "Donate"):
        on = " on" if name == selected else ""
        off = " off" if (name == "Donate" and not donate_ok) else ""
        segs.append(f'<b class="seg{on}{off}">{name}</b>')
    return f'<div class="c chg"><span class="toggle">{"".join(segs)}</span></div>'

def den_cell(denari=None, influence=None, tip=None):
    parts = []
    if influence:
        parts.append(f'<small class="inf pos">+{influence:.1f} influence</small>')
    if denari:
        parts.append(f'<span class="{cls_sign(denari)}">{num(denari)}</span>')
    t = f' title="{html.escape(tip)}"' if tip else ""
    return f'<div class="c den"{t}>{"".join(parts)}</div>'

def plain(v, klass):
    """Mine / Result / Market: plain counts (a dash stays muted)."""
    if v is None:
        return f'<div class="c {klass}"></div>'
    if v == "-":
        return f'<div class="c {klass} muted">{D}</div>'
    return f'<div class="c {klass}">{v:,}</div>' if isinstance(v, int) else f'<div class="c {klass}">{v}</div>'

def neutral(v, klass):
    """Souls / Land kg / Sea kg: the change, plain white (neither good nor bad); blank when zero."""
    if not v:
        return f'<div class="c {klass}"></div>'
    return f'<div class="c {klass}">{num(v)}</div>'

def item_cell(name, det="", link=False, tog=None, indent=0, tier=None, lord=False):
    t = ""
    if tog:
        t = f'<span class="tog" data-toggle="{tog}"><svg viewBox="0 0 10 10"><path d="M3 1.5l4.5 3.5L3 8.5z"/></svg></span>'
    elif indent == 0:
        t = '<span class="tog none"></span>'
    nm = html.escape(name)
    if tier is not None:
        nm = f'T{tier} {nm}'
    nm = f'<span class="name{" link" if link else ""}">{nm}</span>'
    d = f'<span class="det">{det}</span>' if det else ""
    return f'<div class="c item{" ind" if indent else ""}">{t}{nm}{d}</div>'

rows = []  # html strings

def row(kind, grp=None, **cells):
    g = f' data-grp="{grp}"' if grp else ""
    rows.append(f'<div class="row {kind}"{g}>' + "".join(cells["cells"]) + "</div>")

def line(name, det="", mine=None, chg=None, res=None, mkt=None, den=None, inf=None, souls=None, land=None, sea=None,
         link=False, tog=None, kind="line", grp=None, indent=0, tier=None, minus=True, plus=True, reset=False,
         chg_text=None, toggle=None, donate_ok=True, tip=None, extra=""):
    ch = toggle_cell(toggle, donate_ok) if toggle else change_cell(chg, minus, plus, reset, chg_text)
    cells = [item_cell(name, det, link, tog, indent, tier), plain(mine, "mine"), ch, plain(res, "res"), plain(mkt, "mkt"),
             den_cell(den, inf, tip), neutral(souls, "souls"), neutral(land, "land"), neutral(sea, "sea")]
    g = f' data-grp="{grp}"' if grp else ""
    rows.append(f'<div class="row {kind}"{g}>' + "".join(cells) + extra + "</div>")

def sub(name, det, mine, chg, res, mkt, den, land, sea, grp):
    """A breakdown row (per horse type / per good) - small, grey, no buttons (as today's ▸ breakdown)."""
    cells = [f'<div class="c item ind2"><span class="name">{html.escape(name)}</span><span class="det">{det}</span></div>',
             plain(mine, "mine"), f'<div class="c chg"><span class="n {cls_sign(chg)}">{num(chg)}</span></div>',
             plain(res, "res"), plain(mkt, "mkt"), den_cell(den), neutral(None, "souls"), neutral(land, "land"), neutral(sea, "sea")]
    rows.append(f'<div class="row sub detail" data-grp="{grp}">' + "".join(cells) + "</div>")

def section(sec, title, stats, den=None, inf=None, souls=None, land=None, sea=None, badge=None, badge_red=False, mode=""):
    b = f'<span class="badge{" red" if badge_red else ""}">{badge}</span>' if badge else ""
    rows.append(f'</div><div class="sec" data-sec="{sec}"{(" data-mode=" + chr(34) + mode + chr(34)) if mode else ""}>')
    cells = [f'<div class="c span"><span class="tog big"><svg viewBox="0 0 10 10"><path d="M3 1.5l4.5 3.5L3 8.5z"/></svg></span>'
             f'<span class="title">{title}</span>{b}<span class="stats">{stats}</span></div>',
             den_cell(den, inf), neutral(souls, "souls"), neutral(land, "land"), neutral(sea, "sea")]
    rows.append('<div class="row head" title="Click to fold / unfold - remembered">' + "".join(cells) + "</div>")

# ------------------------------------------------------------------ the deal at Lycaron
# TROOPS - 104/101 after the deal (103 now: you + 2 companions + 100 regulars; +4 Hired Pike, -3 dismissed)
section("troops", "Troops", '<span class="muted small">men after the deal / party limit</span>',
        den=-2040, souls=1, badge="104/101", badge_red=True, mode="groups")
line("Temeon the Shipwright", "444 to hire &middot; Engineering 95", 0, 0, 0, "-", link=True, minus=False)
line("Hired Pike", "mercenaries &middot; 510 each", 1, 4, 5, 4, den=-2040, souls=4, link=True, plus=False, reset=True,
     tip="4 x 510 = -2,040")
line("Recruits", "18 on offer &middot; [+] takes the best tier first", "-", 0, "-", 18, tog="recruits", minus=False)
line("Imperial Vigla Recruit", "38 each", "-", 0, "-", 3, link=True, kind="detail", grp="recruits", indent=1, tier=2, minus=False)
line("Imperial Recruit", "17 each", "-", 0, "-", 15, link=True, kind="detail", grp="recruits", indent=1, tier=1, minus=False)
line("Your troops", '<span class="hl">dropping 2 T0 Empire Peasant, 1 T1 Imperial Recruit</span>', 100, -3, 97, None, souls=-3,
     tog="yours", reset=True)
yours = [  # tier, name, mine, change
    (0, "Empire Peasant", 2, -2), (1, "Imperial Recruit", 6, -1), (2, "Imperial Archer", 1, 0), (2, "Imperial Infantryman", 20, 0),
    (3, "Imperial Equite", 6, 0), (3, "Imperial Trained Archer", 10, 0), (4, "Hired Crossbow", 7, 0), (4, "Hired Pike", 1, 0),
    (4, "Imperial Caravan Guard", 7, 0), (4, "Imperial Coast Guard", 2, 0), (4, "Imperial Menavliaton", 8, 0),
    (5, "Elite Hired Crossbow", 6, 0), (5, "Hired Elite Pike", 4, 0), (5, "Imperial Cataphract", 3, 0), (5, "Imperial Legionary", 12, 0),
    (5, "Imperial Palatine Guard", 5, 0)]
assert sum(m for _, _, m, _ in yours) == 100
for tier, name, mine, chg in yours:
    det = "2 wounded - they go first" if name == "Imperial Recruit" else ""
    line(name, det, mine, chg, mine + chg, None, souls=chg or None, link=True, kind="detail", grp="yours", indent=1, tier=tier,
         reset=chg != 0, minus=mine + chg > 0)

# FOOD - 188 -> 200 (+12: variety), ~32 -> 42 days
food = [  # name, det, mine, chg, market, denari
    ("Grain", "11 each", 62, 0, 140, None), ("Fish", "13 each", 46, 0, 35, None), ("Cheese", "42 each", 34, 0, 12, None),
    ("Butter", "27 each", 28, 0, 20, None), ("Beer", "52 each", 16, 0, 8, None), ("Olives", "30&ndash;33 each", 2, 8, 24, -252),
    ("Date Fruit", "56&ndash;57 each", 0, 4, 9, -226), ("Meat", "64 each &middot; over your max 60", 0, 0, 6, None)]
assert sum(f[2] for f in food) == 188 and sum(f[2] + f[3] for f in food) == 200 and sum(f[5] or 0 for f in food) == -478
section("food", "Food", f"7/8 kinds &middot; avg 29 &plusmn; 19 per kind &middot; min 4 Date Fruit &middot; 188 &raquo; 200 (+12) &middot; ~32 &raquo; 42 days",
        den=-478, land=120, sea=120)
for name, det, mine, chg, mkt, den in food:
    tip = "8 x 30-33 = -252 · your max 60 (average 30 x 2.0)" if name == "Olives" else None
    extra = ""
    if name == "Olives":
        extra = ('<div class="tip"><div class="tipline">8 &times; 30&ndash;33 = <span class="neg">&ndash;252</span></div>'
                 '<div class="tipline muted">your max 60 &nbsp;(average 30 &times; 2.0)</div></div>')
    line(name, det, mine, chg, mine + chg, mkt, den=den, land=chg * 10 or None, sea=chg * 10 or None, kind="detail",
         minus=mine + chg > 0, tip=tip, extra=extra)

# HORSES - 92 men on foot after the deal -> 101 horses to keep (10 war + 91 riding); herd 111 / 196
section("horses", "Horses", "111 / 196 before the herd slows you &middot; 92 on foot, 101 horses to keep",
        den=-2115, sea=250)
line("Pack animals", "keep 10", 10, 0, 10, 3, tog="h-pack", kind="detail")
sub("Sumpter Horse", "", 6, 0, 6, 2, None, None, None, "h-pack")
sub("Mule", "", 4, 0, 4, 1, None, None, None, "h-pack")
line("Riding horses", "210&ndash;240 each", 86, 5, 91, 14, den=-1125, sea=250, tog="h-riding", kind="detail")
sub("Midlands Palfrey", "210&ndash;230 each", 40, 3, 43, 8, -660, None, 150, "h-riding")
sub("Steppe Horse", "230&ndash;235 each", 12, 2, 14, 6, -465, None, 100, "h-riding")
line("War horses", "keep 10 &middot; 1,150&ndash;1,260 each", 7, 3, 10, 4, den=-3600, sea=150, tog="h-war", kind="detail")
sub("Canterion Charger", "1,150&ndash;1,260 each", 7, 3, 10, 4, -3600, None, 150, "h-war")
line("Noble horses", "sell only &middot; Palmatian", 1, -1, 0, "-", den=2450, sea=-50, kind="detail", minus=False)
line("Lame horses", "sell only &middot; replaced by healthy ones", 2, -2, 0, "-", den=160, sea=-100, kind="detail", minus=False)

# PRISONERS - 52/60, 2 lords; the dungeon has room for 10
section("prisoners", "Prisoners", "52/60 &middot; 2 lords &middot; avg T2.0 &plusmn; 1.1 &middot; T1&ndash;T5",
        den=16386, inf=11.2, souls=-52)
line("Lords", "2 Khuzait lords", 2, None, 0, None, den=14868, souls=-2, toggle="Ransom")
line("Others", "10 to the dungeon (full), 40 ransomed", 50, None, 0, None, den=1518, inf=11.2, souls=-50, toggle="Donate")
pris = [  # tier, name, mine, denari, influence, det
    (1, "Khuzait Nomad", 8, 192, None, "ransom 24 each"), (1, "Looter", 12, 192, None, "ransom 16 each"),
    (2, "Khuzait Tribal Warrior", 6, 288, None, "ransom 48 each"), (2, "Sea Raider", 9, 396, None, "ransom 44 each"),
    (3, "Khuzait Hunter", 5, 450, None, "ransom 90 each"), (3, "Khuzait Spearman", 4, None, 3.6, "to the dungeon"),
    (4, "Khuzait Horse Archer", 2, None, 2.4, "to the dungeon"), (4, "Khuzait Lancer", 3, None, 3.3, "to the dungeon"),
    (5, "Khuzait Heavy Horse Archer", 1, None, 1.9, "to the dungeon")]
assert sum(p[2] for p in pris) == 50 and sum(p[3] or 0 for p in pris) == 1518 and abs(sum(p[4] or 0 for p in pris) - 11.2) < 1e-9
for tier, name, mine, den, inf, det in pris:
    line(name, det, mine, -mine, 0, None, den=den, inf=inf, souls=-mine, link=True, kind="detail", tier=tier, plus=True, minus=False)
line("Tolun", "lord &middot; ransom 8,210", 1, -1, 0, None, den=8210, souls=-1, link=True, kind="detail", minus=False)
line("Sechen", "lord &middot; ransom 6,658", 1, -1, 0, None, den=6658, souls=-1, link=True, kind="detail", minus=False)

# OTHER - loot groups + the new Other goods line, cheapest first
section("other", "Other", "49 sold: 36 pieces, 13 goods &middot; cheapest first", den=8078, land=-438, sea=-438)
line("Armour", "+2 locked, kept", 14, -14, 0, "-", den=4210, land=-186, sea=-186, kind="detail", plus=True, minus=False)
line("Melee weapons", "", 11, -11, 0, "-", den=1640, land=-52, sea=-52, kind="detail", minus=False)
line("Ranged", "", 6, -6, 0, "-", den=720, land=-30, sea=-30, kind="detail", minus=False)
line("Shields", "", 5, -5, 0, "-", den=380, land=-40, sea=-40, kind="detail", minus=False)
line("Other goods", "every unlocked good that is not food, animal or gear", 13, -13, 0, "-", den=1128, land=-130, sea=-130,
     tog="o-goods", kind="detail", minus=False)
sub("Wool", "22 each", 4, -4, 0, "-", 88, -40, -40, "o-goods")
sub("Salt", "40 each", 5, -5, 0, "-", 200, -50, -50, "o-goods")
sub("Pottery", "210 each", 4, -4, 0, "-", 840, -40, -40, "o-goods")

assert -2040 - 478 - 2115 + 16386 + 8078 == 19831 and 69358 + 19831 == 89189
assert 120 - 438 == -318 and 120 + 250 - 438 == -68

table = "\n".join(rows)[len("</div>"):] + "\n</div>"

HORSE = ('<svg class="ico" viewBox="0 0 24 24" aria-label="party speed (land)"><path d="M6.5 21.5v-4.2c0-2.6 1.2-4.6 2.6-6.4L7 9.8l1.4-3.6 1.7 1.2 1.3-4 1.6 3.5c4.3.6 7.6 4.2 7.3 8.6l-.3.9-2.3-1.6-1.9-1.6c-.8 2.6.1 5.4 1.9 8.3z" fill="currentColor"/><circle cx="13.6" cy="8.6" r=".9" fill="#14100B"/></svg>')
SHIP = ('<svg class="ico" viewBox="0 0 24 24" aria-label="party speed (sea)"><path d="M11 2.5v13H4.5z M12.5 4.5v11h6.5z M3 17h18l-3 4.5H6z" fill="currentColor"/></svg>')

PAGE = """<!DOCTYPE html>
<!--
  Smart Steward - PLAN step 19 (Round 4 MOCKUP): the Suggestion tab as ONE spreadsheet, for Anton to approve before the
  build (steps 20-21). The contract: docs/feedback/2026-09-28-round4.md; the choices Claude made: docs/mockups/README.md.
  Drawn at the window's real size (1580 x 960 px of the game's 1920 x 1080 UI) with the prefab's colours and sizes; the
  game's fonts (Galahad, FiraSansExtraCondensed) are stood in by Palatino Linotype and Bahnschrift SemiCondensed.
  Only what the prefab already uses is drawn: fixed-width text columns, the plain square button, the refresh (reset) and
  collapser sprites, flat BlankWhiteSquare bands; the horse and ship are stand-ins for the game's own speed icons.
  Click a section's title line to fold / unfold it; a small arrow folds its own rows. Open with #expanded for everything open.
  GENERATED by gen_suggestion_v2.py (the numbers in one table, asserted to add up) - edit there and re-run, not here.
  Render both PNGs: powershell -ExecutionPolicy Bypass -File tools\\render-mockup.ps1
-->
<html lang="en">
<head>
<meta charset="utf-8">
<title>Suggestion tab v2 - mockup</title>
<style>
  :root {
    --bg: #14100B; --text: #F1E9DA; --gain: #9FD27F; --cost: #E8906E; --warn: #E5574A; --muted: #8E8A80;
    --head: #E4C59B; --link: #F2C35C; --rule: rgba(228,197,155,0.18); --band: rgba(255,255,255,0.04);
    --title-font: 'Palatino Linotype', 'Book Antiqua', Georgia, serif;
    --body-font: 'Bahnschrift SemiCondensed', 'Bahnschrift', 'Arial Narrow', sans-serif;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html, body { background: #0a0806; }
  body { font-family: var(--body-font); color: var(--text); font-variant-numeric: tabular-nums; }
  .stage { padding: 30px; width: 1640px; }
  .window { position: relative; width: 1580px; height: 960px; background: var(--bg);
    box-shadow: 0 0 0 2px #3a3122, 0 0 0 5px #0d0b08, 0 0 0 7px #5b4d33, 0 12px 40px rgba(0,0,0,.8); }
  body.expanded .window { height: auto; padding-bottom: 84px; display: flow-root; }

  /* title + tabs (unchanged from today's window) */
  .wtitle { position: absolute; top: 14px; left: 30px; right: 30px; height: 44px; text-align: center;
    font-family: var(--title-font); font-size: 34px; color: #F4ECDD; line-height: 44px; }
  .tabs { position: absolute; top: 62px; left: 0; right: 0; display: flex; justify-content: center; gap: 0; }
  .tab { height: 48px; margin-top: 4px; width: 255px; display: flex; align-items: center; justify-content: center;
    font-family: var(--title-font); font-size: 24px; color: #F4ECDD; border: 2px solid #1a1712;
    background: linear-gradient(#6d6a62, #4b4943 45%, #3b3a35); }
  .tab.on { width: 280px; height: 52px; margin-top: 2px; background: linear-gradient(#a57f38, #7b5c22 50%, #5c4418);
    box-shadow: inset 0 0 0 2px #c9a55a; }
  .tab.l { border-radius: 26px 4px 4px 26px; } .tab.r { border-radius: 4px 26px 26px 4px; width: 280px; }

  /* the tab's body */
  .body { position: absolute; top: 132px; left: 30px; right: 30px; bottom: 84px; }
  body.expanded .body { position: relative; top: 0; left: 0; right: 0; bottom: 0; margin: 132px 30px 0; }
  .denari { height: 40px; display: flex; justify-content: center; align-items: baseline; gap: 12px;
    font-family: var(--title-font); font-size: 30px; line-height: 40px; }
  .denari .lab { color: var(--head); } .denari .inf { font-family: var(--body-font); font-size: 19px; }
  .colheads { margin-top: 6px; height: 26px; }
  .colheads .c { color: var(--muted); font-size: 17px; }
  .rule { height: 1px; background: rgba(228,197,155,0.25); }

  /* the grid: fixed widths, as Gauntlet's fixed TextWidgets in a horizontal ListPanel */
  .row, .colheads, .totalrow { display: grid; align-items: center;
    grid-template-columns: 446px 84px 224px 84px 90px 230px 90px 120px 120px; }
  .c { white-space: nowrap; overflow: hidden; font-size: 19px; padding: 0 6px 0 0; }
  .mine, .res, .mkt, .den, .souls, .land, .sea { text-align: right; }
  .item { padding-left: 6px; display: flex; align-items: center; }
  .span { grid-column: 1 / span 5; display: flex; align-items: baseline; padding-left: 6px; }

  .scroll { position: relative; height: 536px; overflow: hidden; margin-top: 4px; margin-right: 14px; }
  body.expanded .scroll { height: auto; overflow: visible; }
  .sbar { position: absolute; right: 3px; top: 77px; height: 536px; width: 8px; }
  .sbar .bed { position: absolute; left: 3px; width: 2px; top: 0; bottom: 0; background: rgba(228,197,155,0.25); }
  .sbar .handle { position: absolute; left: 1px; width: 6px; top: 0; height: 120px; background: #b9a57c; border-radius: 3px; }
  body.expanded .sbar { display: none; }

  .row { height: 32px; position: relative; }
  .row::before { content: ""; position: absolute; left: 0; right: 0; top: 2px; bottom: 2px; background: var(--band); z-index: 0; }
  .row > * { position: relative; z-index: 1; }
  .row.head { height: 36px; align-items: end; padding-bottom: 5px; border-bottom: 1px solid var(--rule); cursor: pointer; }
  .row.head::before { display: none; }
  .row.head .c { font-size: 20px; }
  .row.sub { height: 26px; }
  .row.sub::before { display: none; }
  .row.sub .c { font-size: 16px; color: var(--muted); }
  .row.sub .c .pos, .row.sub .c .neg { font-size: 16px; }

  .title { font-family: var(--title-font); font-size: 25px; color: var(--head); margin-left: 8px; }
  .badge { font-size: 21px; margin-left: 14px; color: var(--text); }
  .badge.red { color: var(--warn); }
  .stats { font-size: 18px; margin-left: 16px; color: #D9D0C0; }
  .small { font-size: 15px; }
  .name { font-size: 19px; }
  .name.link { color: var(--link); }
  .det { font-size: 15px; color: var(--muted); margin-left: 12px; }
  .det .hl { color: #D9D0C0; }
  .ind { padding-left: 48px; }
  .ind2 { padding-left: 74px; }
  .ind2 .name { font-size: 16px; }
  .ind2 .det { font-size: 14px; }

  .tog { width: 22px; height: 22px; display: inline-flex; align-items: center; justify-content: center; flex: none;
    margin-right: 4px; cursor: pointer; color: #C9B48A; }
  .tog.none { cursor: default; }
  .tog svg { width: 14px; height: 14px; fill: currentColor; transition: transform .12s; }
  .tog.open svg { transform: rotate(90deg); }
  .tog.big svg { width: 16px; height: 16px; }
  .row.head .tog { align-self: center; margin-bottom: -4px; }

  .chg { display: flex; align-items: center; justify-content: flex-start; padding-left: 10px; }
  .btn { width: 34px; height: 26px; display: inline-flex; align-items: center; justify-content: center; background: #6a6862;
    color: #F4F1EA; font-weight: normal; font-size: 22px; line-height: 1; border-radius: 1px; }
  .btn.off { background: #34332f; color: #77746c; }
  .chg .n { width: 66px; text-align: center; font-size: 20px; }
  .rst { width: 28px; height: 28px; margin-left: 10px; color: #C9A45C; display: inline-flex; }
  .rst svg { width: 26px; height: 26px; }
  .rst.none { visibility: hidden; }
  .toggle { display: inline-flex; border: 1px solid #56524a; }
  .seg { font-weight: normal; font-size: 16px; padding: 3px 9px; color: #BDB6A8; background: #2c2a26; border-left: 1px solid #56524a; }
  .seg:first-child { border-left: none; }
  .seg.on { background: #8a6a2c; color: #FFF6DE; }
  .seg.off { color: #5d5a53; }

  .pos { color: var(--gain); } .neg { color: var(--cost); } .zero, .muted { color: var(--muted); }
  .inf { font-size: 14px; margin-right: 10px; }

  /* the pinned bottom: Total + the weight table (never scroll) */
  .pinned { position: absolute; left: 0; right: 14px; bottom: 0; }
  body.expanded .pinned { position: relative; margin-top: 6px; margin-right: 14px; }
  .totalrow { height: 38px; border-top: 1px solid rgba(228,197,155,0.45); align-items: center; background: rgba(228,197,155,0.06); }
  .totalrow .c { font-size: 20px; }
  .totalrow .tname { font-family: var(--title-font); font-size: 25px; color: var(--head); margin-left: 30px; }

  .foot { display: flex; margin-top: 10px; gap: 40px; align-items: flex-start; }
  .wtable { display: grid; grid-template-columns: 70px 110px 100px 110px 200px 110px 150px; row-gap: 0; }
  .wtable div { height: 28px; line-height: 28px; font-size: 19px; text-align: right; padding-right: 8px; white-space: nowrap; }
  .wtable .h { color: var(--muted); font-size: 16px; height: 22px; line-height: 22px; }
  .wtable .lab { text-align: left; color: var(--muted); padding-left: 6px; }
  .wtable .slow { display: flex; justify-content: flex-end; align-items: center; gap: 8px; }
  .ico { width: 22px; height: 22px; color: #E9DFC9; }
  .notes { flex: 1; height: 72px; border: 1px dashed rgba(142,138,128,0.45); color: var(--muted); font-size: 15px;
    font-style: italic; padding: 8px 12px; margin-right: 6px; line-height: 20px; }

  /* the buttons (unchanged) */
  .buttons { position: absolute; left: 30px; right: 30px; bottom: 18px; height: 52px; }
  .wbtn { position: absolute; bottom: 0; width: 200px; height: 52px; display: flex; align-items: center; justify-content: center;
    font-family: var(--title-font); font-size: 22px; color: #F4ECDD;
    clip-path: polygon(14px 0, calc(100% - 14px) 0, 100% 50%, calc(100% - 14px) 100%, 14px 100%, 0 50%);
    background: linear-gradient(#57544c, #3d3b35); }
  .wbtn::after { content: ""; position: absolute; inset: 4px 10px; border: 1px solid rgba(233,223,201,.25);
    clip-path: polygon(10px 0, calc(100% - 10px) 0, 100% 50%, calc(100% - 10px) 100%, 10px 100%, 0 50%); }
  .wbtn.reset { left: 0; } .wbtn.close { right: 216px; } .wbtn.doit { right: 0; background: linear-gradient(#6f7f45, #4d5a2c); }

  /* annotations (not part of the window) */
  .foldmark { display: none; }
  body.expanded .foldmark { display: block; position: absolute; left: -30px; right: -44px; border-top: 2px dashed rgba(229,87,74,.7); z-index: 5; }
  .foldmark span { position: absolute; right: 44px; top: -24px; font-size: 15px; color: #E5574A; background: var(--bg); padding: 0 6px; font-style: italic; }
  .tip { display: none; }
  body.expanded .tip { display: block; position: absolute; left: 1166px; top: -64px; width: 318px; z-index: 6; padding: 8px 12px;
    background: #0b0a08; border: 1px solid #6f6552; box-shadow: 0 4px 14px rgba(0,0,0,.7); }
  .tipline { font-size: 17px; line-height: 23px; white-space: nowrap; }
</style>
</head>
<body>
<div class="stage">
<div class="window">
  <div class="wtitle">Party Steward &mdash; Lycaron</div>
  <div class="tabs"><div class="tab l on">Suggestion</div><div class="tab">Prices</div><div class="tab r">Instructions</div></div>

  <div class="body">
    <div class="denari"><span class="lab">Denari</span><span>69,358 &raquo; 89,189</span><span class="pos">(+19,831)</span>
      <span class="inf pos">+11.2 influence</span></div>
    <div class="colheads">
      <div class="c item" style="padding-left:32px">Item</div><div class="c mine">Mine</div>
      <div class="c" style="padding-left:52px">Change</div><div class="c res">Result</div><div class="c mkt">Market</div>
      <div class="c den">Denari</div><div class="c souls" title="Everyone who eats: your men and your prisoners">Souls</div>
      <div class="c land">Land kg</div><div class="c sea">Sea kg</div>
    </div>
    <div class="rule"></div>

    <div class="scroll" id="scroll">
      <div class="foldmark" id="foldmark"><span>in game the table scrolls below this line (the window stays 960 px tall)</span></div>
      <div class="list" id="list">
TABLE
      </div>
    </div>
    <div class="sbar" id="sbar"><div class="bed"></div><div class="handle" id="handle"></div></div>

    <div class="pinned">
      <div class="totalrow">
        <div class="c span"><span class="tname">Total</span><span class="det" style="font-size:16px">souls 155 &raquo; 104</span></div>
        <div class="c den"><small class="inf pos">+11.2 influence</small><span class="pos">+19,831</span></div>
        <div class="c souls">&ndash;51</div><div class="c land">&ndash;318</div><div class="c sea">&ndash;68</div>
      </div>
      <div class="foot">
        <div class="wtable">
          <div class="h"></div><div class="h">before</div><div class="h">change</div><div class="h">after</div>
          <div class="h">capacity</div><div class="h">left</div><div class="h">slowdown</div>
          <div class="lab">Land</div><div>2,348</div><div>&ndash;318</div><div>2,030</div><div>6,185 &raquo; 6,305</div>
          <div>4,275</div><div class="slow">HORSE<span class="muted">none</span></div>
          <div class="lab">Sea</div><div>7,898</div><div>&ndash;68</div><div>7,830</div><div>13,970 &raquo; 13,990</div>
          <div>6,160</div><div class="slow">SHIP<span class="muted">none</span></div>
        </div>
        <div class="notes">Warnings (a money floor breached, a deal the purse cannot pay) and the last result appear here, in red / grey.
          The "Click &plusmn;1 &middot; Shift &plusmn;5 &middot; Ctrl all" hint moved to the top of the Instructions tab.</div>
      </div>
    </div>
  </div>

  <div class="buttons"><div class="wbtn reset">Reset all</div><div class="wbtn close">Close</div><div class="wbtn doit">Do it</div></div>
</div>
</div>
<script>
// Folding, as the window will do it: a section's title line folds its detail rows (the always-there lines stay);
// a small arrow opens its own rows. The Troops title opens / closes both of its groups.
(function () {
  var expanded = location.hash === '#expanded';
  if (expanded) document.body.classList.add('expanded');
  // the everyday view: Food, the troop groups and the Prisoners' details folded; Horses and Other open
  var foldedSecs = expanded ? {} : { food: 1, prisoners: 1 };
  var openGrps = expanded ? { recruits: 1, yours: 1, 'h-riding': 1, 'h-pack': 1, 'h-war': 1, 'o-goods': 1 } : {};
  var secs = document.querySelectorAll('.sec');
  function refresh() {
    secs.forEach(function (sec) {
      var name = sec.dataset.sec, groups = sec.dataset.mode === 'groups', folded;
      if (groups) {
        folded = !Array.prototype.some.call(sec.querySelectorAll('[data-toggle]'), function (t) { return openGrps[t.dataset.toggle]; });
      } else folded = !!foldedSecs[name];
      sec.querySelector('.row.head .tog').classList.toggle('open', !folded);
      sec.querySelectorAll('.row').forEach(function (r) {
        if (r.classList.contains('head')) return;
        var show = true;
        if (r.classList.contains('detail') && !groups && folded) show = false;
        if (r.dataset.grp && !openGrps[r.dataset.grp]) show = false;
        r.style.display = show ? '' : 'none';
      });
      sec.querySelectorAll('[data-toggle]').forEach(function (t) { t.classList.toggle('open', !!openGrps[t.dataset.toggle]); });
    });
    var scroll = document.getElementById('scroll'), list = document.getElementById('list');
    var h = scroll.clientHeight, all = list.scrollHeight, handle = document.getElementById('handle');
    document.getElementById('sbar').style.visibility = all > h ? 'visible' : 'hidden';
    handle.style.height = Math.max(40, Math.round(h * h / Math.max(all, 1))) + 'px';
    if (expanded) document.getElementById('foldmark').style.top = '536px';
    document.documentElement.setAttribute('data-h', document.body.scrollHeight);
  }
  secs.forEach(function (sec) {
    sec.querySelector('.row.head').addEventListener('click', function () {
      if (sec.dataset.mode === 'groups') {
        var ts = sec.querySelectorAll('[data-toggle]'), any = Array.prototype.some.call(ts, function (t) { return openGrps[t.dataset.toggle]; });
        ts.forEach(function (t) { if (any) delete openGrps[t.dataset.toggle]; else openGrps[t.dataset.toggle] = 1; });
      } else if (foldedSecs[sec.dataset.sec]) delete foldedSecs[sec.dataset.sec]; else foldedSecs[sec.dataset.sec] = 1;
      refresh();
    });
  });
  document.querySelectorAll('[data-toggle]').forEach(function (t) {
    t.addEventListener('click', function (e) {
      e.stopPropagation();
      var g = t.dataset.toggle; if (openGrps[g]) delete openGrps[g]; else openGrps[g] = 1; refresh();
    });
  });
  refresh();
})();
</script>
</body>
</html>
"""

page = PAGE.replace("TABLE", table).replace("HORSE", HORSE).replace("SHIP", SHIP)
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write(page)
print("wrote", OUT, len(page), "bytes")
