"""Writes the Suggestion tab of module/GUI/Prefabs/SmartStewardWindow.xml (PLAN step 21, the round-4 spreadsheet) from ONE
set of column widths - the column heads, the section title lines, the lines, the breakdown lines, the pinned Total
line and the footer weight table - so they always align. Edit the widths / sizes HERE, then run (from the repo root):

    python tools/gen-suggestion-tab.py module/GUI/Prefabs/SmartStewardWindow.xml

It replaces everything from the Suggestion tab's start marker to the Prices tab's marker. Afterwards run
tools/check-gui.ps1 (every binding, brush and sprite against the game and the built DLL)."""
import sys

PREFAB = sys.argv[1]
STAGE = "c"  # the build stages of step 21 (a: skeleton, b: toggles, c: Total + footer) - only the final one is kept

# Column widths (px) - the table is 1506 wide (1520 minus the 14 px scrollbar lane). Round 5 (step 23, Anton 2026.09.28):
# Item · Market · Goal · Mine · Change · Result · Denari · Party · Prisoners · Land weight · Sea weight - Item back at the far
# left, the Goal column a typed box (GOAL_BOX) with the goal's reset button beside it (GOAL_RESET).
# Step 27 (Anton 2026.10.01, "do just this part"): the Part column right after Result - a small "Do" button on every section
# title and on the lines that are a deal of their own; its 62 px came from Item (432 -> 396), Denari (204 -> 190) and
# Prisoners (92 -> 80).
PAD, ITEM, MARKET = 6, 396, 66
GOAL_GAP, GOAL_BOX, GOAL_RESET_GAP, GOAL_RESET = 8, 70, 4, 26
GOAL = GOAL_BOX + GOAL_RESET_GAP + GOAL_RESET  # 100
MINE, CHANGE_GAP, CHANGE, RESULT = 66, 8, 176, 66
DO_GAP, DO = 6, 56
DENARI_GAP, DENARI, PARTY, PRISONERS, LAND, SEA = 8, 190, 66, 80, 104, 104
LEFT = PAD + ITEM + MARKET + GOAL_GAP + GOAL + MINE + CHANGE_GAP + CHANGE + RESULT + DO_GAP + DO  # 954: everything left of Denari
NAME_PART = PAD + ITEM + MARKET  # 504: the title line's name (its numbers start at the Goal column)
TITLE_HEIGHT, TITLE_ROW, OVERVIEW_ROW = 54, 30, 20  # round 5: the overview on its own row under the name, full width

TXT = 'Brush="Popup.Description.Text"'


def text(width, value, size=19, align="Right", color=None, margin_left=0, visible=None, extra=""):
    w = f'WidthSizePolicy="Fixed" SuggestedWidth="{width}"' if width else 'WidthSizePolicy="CoverChildren"'
    ml = f' MarginLeft="{margin_left}"' if margin_left else ""
    c = f' Brush.FontColor="{color}"' if color else ""
    v = f' IsVisible="{visible}"' if visible else ""
    return (f'<TextWidget {w} HeightSizePolicy="StretchToParent"{ml} {TXT} Brush.FontSize="{size}" '
            f'Brush.TextHorizontalAlignment="{align}" Brush.TextVerticalAlignment="Center"{c} Text="{value}"{v}{extra} />')


def denari_cell(size, small, hint=True):
    hint_xml = ('\n  <HintWidget DataSource="{DenariHint}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" '
                'Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />') if hint else ""
    return f'''<Widget WidthSizePolicy="Fixed" SuggestedWidth="{DENARI}" HeightSizePolicy="StretchToParent" MarginLeft="{DENARI_GAP}">
  <Children>
    <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" HorizontalAlignment="Right" StackLayout.LayoutMethod="HorizontalLeftToRight">
      <Children>
        {text(0, "@InfluenceText", small, "Right", "@InfluenceColor", extra=' MarginRight="8"')}
        {text(0, "@DenariText", size, "Right", "@DenariColor")}
      </Children>
    </ListPanel>{hint_xml}
  </Children>
</Widget>'''


def number_cells(size, color=None):
    return "\n".join([
        text(PARTY, "@PartyText", size, color=color),
        text(PRISONERS, "@PrisonersText", size, color=color),
        text(LAND, "@LandText", size, color=color),
        text(SEA, "@SeaText", size, color=color, visible="@ShowSea"),
    ])


def small_button(command, enabled, hint, label, margin_left=0, width=34, font=22, visible=None):
    ml = f' MarginLeft="{margin_left}"' if margin_left else ""
    v = f' IsVisible="{visible}"' if visible else ""
    return f'''<Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="{width}" SuggestedHeight="26" VerticalAlignment="Center"{ml}{v}>
  <Children>
    <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="ButtonSimpleBrush" Command.Click="{command}" IsEnabled="{enabled}" UpdateChildrenStates="true">
      <Children>
        <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Button.Text" Brush.FontSize="{font}" Text="{label}" />
      </Children>
    </ButtonWidget>
    <HintWidget DataSource="{{{hint}}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />
  </Children>
</Widget>'''


def toggle_button(command, lit, enabled, hint, label, margin_left):
    ml = f' MarginLeft="{margin_left}"' if margin_left else ""
    return f'''<Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="64" SuggestedHeight="24" VerticalAlignment="Center"{ml}>
  <Children>
    <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="ButtonSimpleBrush" Command.Click="{command}" IsEnabled="{enabled}" UpdateChildrenStates="true">
      <Children>
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="BlankWhiteSquare_9" Color="#B8893EFF" IsVisible="{lit}" DoNotAcceptEvents="true" />
        <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Button.Text" Brush.FontSize="16" Text="{label}" />
      </Children>
    </ButtonWidget>
    <HintWidget DataSource="{{{hint}}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />
  </Children>
</Widget>'''


def hinted_text(width, value, size, color, hint, align="Right", margin_left=0, visible=None, margin_right=0):
    """A text cell with a hover (vanilla's wrapper: a Widget holding the text and a disabled HintWidget)."""
    ml = f' MarginLeft="{margin_left}"' if margin_left else ""
    v = f' IsVisible="{visible}"' if visible else ""
    mr = f' MarginRight="{margin_right}"' if margin_right else ""
    return f'''<Widget WidthSizePolicy="Fixed" SuggestedWidth="{width}" HeightSizePolicy="StretchToParent"{ml}{v}>
  <Children>
    <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"{mr} {TXT} Brush.FontSize="{size}" Brush.TextHorizontalAlignment="{align}" Brush.TextVerticalAlignment="Center" Brush.FontColor="{color}" Text="{value}" />
    <HintWidget DataSource="{{{hint}}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />
  </Children>
</Widget>'''


def goal_cell():
    """Round 5 (step 23): the Goal column of a line - a typed box on the food and pack / riding / war rows (Enter or a click
    elsewhere commits, Escape reverts; gold = yours, with its reset button beside it), plain text on every other line, the
    grey hands-off mark with its hover."""
    return f'''<Widget WidthSizePolicy="Fixed" SuggestedWidth="{GOAL}" HeightSizePolicy="StretchToParent" MarginLeft="{GOAL_GAP}">
  <Children>
{indent(hinted_text(GOAL_BOX, "@GoalText", 19, "@GoalColor", "GoalHint", visible="@IsGoalPlain", margin_right=6), 4)}
    <Widget WidthSizePolicy="Fixed" SuggestedWidth="{GOAL_BOX}" HeightSizePolicy="Fixed" SuggestedHeight="26" VerticalAlignment="Center" IsVisible="@IsGoalBox">
      <Children>
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="BlankWhiteSquare_9" Color="#000000FF" AlphaFactor="0.55" DoNotAcceptEvents="true" />
        <EditableTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginLeft="4" MarginRight="6" Brush="Review.NameInput.Text" Brush.FontSize="19" Brush.TextHorizontalAlignment="Right" Brush.FontColor="@GoalColor" RealText="@GoalText" MaxLength="7" Command.TextEntered="ExecuteGoalEntered" Command.FocusGained="ExecuteGoalFocusGained" Command.FocusLost="ExecuteGoalFocusLost">
          <Children>
            <HintWidget DataSource="{{GoalHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />
          </Children>
        </EditableTextWidget>
      </Children>
    </Widget>
    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="{GOAL_RESET}" SuggestedHeight="{GOAL_RESET}" HorizontalAlignment="Right" VerticalAlignment="Center">
      <Children>
        <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="RefreshButton.Flat" Command.Click="ExecuteReset" IsVisible="@CanResetGoal">
          <Children>
            <HintWidget DataSource="{{GoalResetHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsEnabled="false" />
          </Children>
        </ButtonWidget>
      </Children>
    </Widget>
  </Children>
</Widget>'''


def do_cell():
    """Step 27: the Part column - "Do" runs this part of the plan alone (greyed with the reason on hover when it has nothing
    it can do alone); an empty cell on the lines that carry no part."""
    return f'''<Widget WidthSizePolicy="Fixed" SuggestedWidth="{DO}" HeightSizePolicy="StretchToParent" MarginLeft="{DO_GAP}">
  <Children>
{indent(small_button("ExecuteDoPart", "@CanDoPart", "PartHint", "@DoPartText", width=DO - 8, font=17, visible="@HasPart"), 4).replace('VerticalAlignment="Center"', 'HorizontalAlignment="Center" VerticalAlignment="Center"', 1)}
  </Children>
</Widget>'''


def indent(block, n):
    pad = " " * n
    return "\n".join(pad + line if line.strip() else line for line in block.split("\n"))


toggle = ""
if STAGE in ("b", "c"):
    toggle = f'''
<!-- the Lords / Others toggle: Keep | Ransom | Donate = the setting (mockup choice 7) - the chosen one lit gold, Donate greyed
     where the game forbids it -->
<ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight" IsVisible="@HasToggle">
  <Children>
{indent(toggle_button("ExecuteKeep", "@IsKeep", "@CanKeep", "KeepHint", "@KeepText", 8), 4)}
{indent(toggle_button("ExecuteRansom", "@IsRansom", "@CanRansom", "RansomHint", "@RansomText", 2), 4)}
{indent(toggle_button("ExecuteDonate", "@IsDonate", "@CanDonate", "DonateHint", "@DonateText", 2), 4)}
  </Children>
</ListPanel>'''

main_line = f'''<!-- a line: a row, a troop line or a prisoner line (32 px, mockup choice 10) -->
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="32" IsVisible="@IsMainLine">
  <Children>
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginTop="1" MarginBottom="1" Sprite="BlankWhiteSquare_9" Color="#FFFFFFFF" AlphaFactor="0.04" />
    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
      <Children>
        <!-- Item: [indent] [fold] the name (gold = the Encyclopedia) and the small grey note - far left again (round 5) -->
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{ITEM}" HeightSizePolicy="StretchToParent" MarginLeft="{PAD}" ClipContents="true">
          <Children>
            <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
              <Children>
                <Widget WidthSizePolicy="Fixed" SuggestedWidth="20" HeightSizePolicy="StretchToParent" IsVisible="@IsIndented" />
                <Widget WidthSizePolicy="Fixed" SuggestedWidth="24" HeightSizePolicy="StretchToParent">
                  <Children>
                    <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="20" SuggestedHeight="20" VerticalAlignment="Center" Command.Click="ExecuteToggleFold" IsVisible="@HasExpander">
                      <Children>
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="SPGeneral\\SPOptions\\collapser_indicator_closed" IsVisible="@IsExpanderClosed" />
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="SPGeneral\\SPOptions\\collapser_indicator_open" IsVisible="@IsExpanderOpenShown" />
                        <HintWidget DataSource="{{FoldHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsEnabled="false" />
                      </Children>
                    </ButtonWidget>
                  </Children>
                </Widget>
                {text(0, "@NameText", 19, "Left", "@TextColor", visible="@IsPlainName")}
                <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" Command.Click="ExecuteOpenLink" IsVisible="@IsLink">
                  <Children>
                    {text(0, "@NameText", 19, "Left", "@LinkColor")}
                    <HintWidget DataSource="{{LinkHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsEnabled="false" />
                  </Children>
                </ButtonWidget>
                {text(0, "@NoteText", 15, "Left", "@MutedColor", margin_left=10)}
              </Children>
            </ListPanel>
          </Children>
        </Widget>
        {text(MARKET, "@MarketText", 19)}
        <!-- Goal (round 5): where the line should end - typed on the food and horse role rows -->
{indent(goal_cell(), 8)}
        {text(MINE, "@MineText", 19)}
        <!-- Change: [-] n [+] [reset] - or the prisoner toggle (a goal row's reset sits in the Goal column) -->
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{CHANGE}" HeightSizePolicy="StretchToParent" MarginLeft="{CHANGE_GAP}">
          <Children>
            <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight" IsVisible="@HasSpinner">
              <Children>
{indent(small_button("ExecuteDecrease", "@CanDecrease", "DecreaseHint", "&#x2013;", 8), 16)}
                {text(60, "@ChangeText", 19, "Center", "@ChangeColor")}
{indent(small_button("ExecuteIncrease", "@CanIncrease", "IncreaseHint", "+"), 16)}
                <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="28" SuggestedHeight="28" VerticalAlignment="Center" MarginLeft="10">
                  <Children>
                    <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="RefreshButton.Flat" Command.Click="ExecuteReset" IsVisible="@CanResetInChange">
                      <Children>
                        <HintWidget DataSource="{{ResetHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsEnabled="false" />
                      </Children>
                    </ButtonWidget>
                  </Children>
                </Widget>
              </Children>
            </ListPanel>{indent(toggle, 12)}
            {text(0, "@ChangeText", 19, "Center", "@ChangeColor", visible="@HasChangeOnly", extra=' HorizontalAlignment="Center"')}
          </Children>
        </Widget>
        <!-- Result: its hover says why it stops short of the goal (round 5) -->
{indent(hinted_text(RESULT, "@ResultText", 19, "@TextColor", "ResultHint"), 8)}
        <!-- Part (step 27): "Do" - just this line's deal -->
{indent(do_cell(), 8)}
{indent(denari_cell(19, 14), 8)}
{indent(number_cells(19), 8)}
      </Children>
    </ListPanel>
  </Children>
</Widget>'''

sub_line = f'''<!-- a breakdown line (26 px): one kind inside a row's fold, small and grey, no buttons -->
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="26" IsVisible="@IsSubLine">
  <Children>
    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
      <Children>
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{ITEM}" HeightSizePolicy="StretchToParent" MarginLeft="{PAD}" ClipContents="true">
          <Children>
            <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
              <Children>
                <Widget WidthSizePolicy="Fixed" SuggestedWidth="64" HeightSizePolicy="StretchToParent" />
                {text(0, "@NameText", 16, "Left", "@MutedColor")}
                {text(0, "@NoteText", 14, "Left", "@MutedColor", margin_left=10)}
              </Children>
            </ListPanel>
          </Children>
        </Widget>
        {text(MARKET, "@MarketText", 16, color="@MutedColor")}
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{GOAL}" HeightSizePolicy="StretchToParent" MarginLeft="{GOAL_GAP}" />
        {text(MINE, "@MineText", 16, color="@MutedColor")}
        {text(CHANGE, "@ChangeText", 16, "Center", "@ChangeColor", margin_left=CHANGE_GAP)}
        {text(RESULT, "@ResultText", 16, color="@MutedColor")}
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{DO}" HeightSizePolicy="StretchToParent" MarginLeft="{DO_GAP}" />
{indent(denari_cell(16, 13), 8)}
{indent(number_cells(16, "@MutedColor"), 8)}
      </Children>
    </ListPanel>
  </Children>
</Widget>'''

title_line = f'''<!-- the section's title line: [fold] the name and its subtotal in every column (mockup choice 3) - since round 5 its Goal,
     Mine and Result too (Troops: the party size limit, the members now red over it), so the overview moved to a row of its own
     under the name, the table's whole left width (hover: all of it) -->
<Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="{TITLE_HEIGHT}">
  <Children>
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginTop="3" Sprite="BlankWhiteSquare_9" Color="#E4C59BFF" AlphaFactor="0.06" />
    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="{TITLE_ROW}" MarginTop="3" StackLayout.LayoutMethod="HorizontalLeftToRight">
      <Children>
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{NAME_PART}" HeightSizePolicy="StretchToParent" ClipContents="true">
          <Children>
            <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" MarginLeft="{PAD}" StackLayout.LayoutMethod="HorizontalLeftToRight">
              <Children>
                <ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" Command.Click="ExecuteToggle">
                  <Children>
                    <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
                      <Children>
                        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="22" SuggestedHeight="22" VerticalAlignment="Center" MarginRight="8">
                          <Children>
                            <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="SPGeneral\\SPOptions\\collapser_indicator_closed" IsVisible="@IsClosedIndicator" />
                            <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="SPGeneral\\SPOptions\\collapser_indicator_open" IsVisible="@IsOpenIndicator" />
                          </Children>
                        </Widget>
                        <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" Brush="Popup.Title.Text" Brush.FontSize="24" Brush.TextHorizontalAlignment="Left" Brush.TextVerticalAlignment="Center" Brush.FontColor="@HeadingColor" Text="@TitleText" />
                      </Children>
                    </ListPanel>
                    <HintWidget DataSource="{{ToggleHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsEnabled="false" />
                  </Children>
                </ButtonWidget>
              </Children>
            </ListPanel>
          </Children>
        </Widget>
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{GOAL}" HeightSizePolicy="StretchToParent" MarginLeft="{GOAL_GAP}">
          <Children>
{indent(hinted_text(GOAL_BOX, "@GoalText", 20, "@GoalColor", "GoalHint", margin_right=6), 12)}
          </Children>
        </Widget>
        {text(MINE, "@MineText", 20, color="@MineColor")}
        <Widget WidthSizePolicy="Fixed" SuggestedWidth="{CHANGE}" HeightSizePolicy="StretchToParent" MarginLeft="{CHANGE_GAP}" />
        {text(RESULT, "@ResultText", 20, color="@ResultColor")}
        <!-- Part (step 27): "Do" - the whole section alone, folded or not -->
{indent(do_cell(), 8)}
{indent(denari_cell(20, 15, hint=False), 8)}
{indent(number_cells(20), 8)}
      </Children>
    </ListPanel>
    <!-- the overview (red past a limit: Troops 104/101, the Horses when the herd slows) and its small grey note -->
    <Widget WidthSizePolicy="Fixed" SuggestedWidth="{LEFT - 36}" HeightSizePolicy="Fixed" SuggestedHeight="{OVERVIEW_ROW}" VerticalAlignment="Bottom" MarginLeft="36" MarginBottom="2" ClipContents="true">
      <Children>
        <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
          <Children>
            {text(0, "@OverviewText", 16, "Left", "@OverviewColor")}
            {text(0, "@OverviewNote", 14, "Left", "@MutedColor", margin_left=12)}
          </Children>
        </ListPanel>
        <HintWidget DataSource="{{OverviewHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />
      </Children>
    </Widget>
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="1" VerticalAlignment="Bottom" Sprite="BlankWhiteSquare_9" Color="#E4C59BFF" AlphaFactor="0.18" />
  </Children>
</Widget>'''


def head(width, value, align="Right", margin_left=0, visible=None):
    return text(width, value, 17, align, "@MutedColor", margin_left, visible)


heads = "\n".join([
    head(ITEM - 24, "@ColItem", "Left", PAD + 24),
    head(MARKET, "@ColMarket"),
    head(GOAL_BOX - 6, "@ColGoal", "Right", GOAL_GAP),
    f'<Widget WidthSizePolicy="Fixed" SuggestedWidth="{GOAL - GOAL_BOX + 6}" HeightSizePolicy="StretchToParent" />',
    head(MINE, "@ColMine"),
    head(CHANGE, "@ColChange", "Center", CHANGE_GAP),
    head(RESULT, "@ColResult"),
    head(DO, "@ColPart", "Center", DO_GAP),
    head(DENARI, "@ColDenari", "Right", DENARI_GAP),
    head(PARTY, "@ColParty"),
    head(PRISONERS, "@ColPrisoners"),
    head(LAND, "@ColLand"),
    head(SEA, "@ColSea", visible="@ShowSea"),
])

# The table's bottom: stage a keeps the old footer (176 px); stage c pins the Total line and the weight table (see below).
table_bottom = 182 if STAGE in ("a", "b") else 150

tab = f'''            <!-- ============================ Suggestion tab (DESIGN §1.1) ============================ -->
            <!-- Round 4 (PLAN step 21): ONE spreadsheet - the mockup Anton approved on 2026.09.28 (docs/mockups). Every width below
                 comes from one table (tools/gen-suggestion-tab.py), so the heads, the title lines, the lines and the breakdown lines align.
                 Round 5 (PLAN step 23): the Goal column, Item back at the far left, the overview on its own row under each title. -->
            <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginLeft="30" MarginRight="30" MarginTop="132" MarginBottom="84" IsVisible="@IsSuggestionSelected">
              <Children>
                <Widget DataSource="{{Suggestion}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
                  <Children>

                    <!-- The header: Denari now » after (change) + the small green influence (round 4) -->
                    <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="Fixed" SuggestedHeight="40" HorizontalAlignment="Center" VerticalAlignment="Top" StackLayout.LayoutMethod="HorizontalLeftToRight">
                      <Children>
                        <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" Brush="Popup.Title.Text" Brush.FontSize="30" Brush.TextVerticalAlignment="Center" Brush.FontColor="@HeadingColor" Text="@HeaderLabel" />
                        <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" MarginLeft="12" Brush="Popup.Title.Text" Brush.FontSize="30" Brush.TextVerticalAlignment="Center" Brush.FontColor="@HeadingColor" Text="@HeaderFlowText" />
                        <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" MarginLeft="12" Brush="Popup.Title.Text" Brush.FontSize="30" Brush.TextVerticalAlignment="Center" Brush.FontColor="@HeaderChangeColor" Text="@HeaderChangeText" />
                        <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" MarginLeft="14" MarginTop="6" Brush="Popup.Description.Text" Brush.FontSize="18" Brush.TextVerticalAlignment="Center" Brush.FontColor="@InfluenceColor" Text="@HeaderInfluenceText" />
                      </Children>
                    </ListPanel>

                    <!-- A closed market: the game's own reason, on the header line's right (round 1) -->
                    <TextWidget WidthSizePolicy="Fixed" SuggestedWidth="560" HeightSizePolicy="Fixed" SuggestedHeight="40" HorizontalAlignment="Right" VerticalAlignment="Top" Brush="Popup.Description.Text" Brush.FontSize="18" Brush.TextHorizontalAlignment="Right" Brush.TextVerticalAlignment="Center" Brush.FontColor="@WarningColor" Text="@MarketClosedText" IsVisible="@HasMarketNotice" />

                    <!-- Column heads: Item · Market · Goal · Mine · Change · Result · Part · Denari · Party · Prisoners · Land weight · Sea weight (round 5; Part step 27), the same widths as the lines -->
                    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="26" VerticalAlignment="Top" MarginTop="46" MarginRight="14" StackLayout.LayoutMethod="HorizontalLeftToRight">
                      <Children>
{indent(heads, 24)}
                      </Children>
                    </ListPanel>
                    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="1" VerticalAlignment="Top" MarginTop="74" Sprite="BlankWhiteSquare_9" Color="#E4C59BFF" AlphaFactor="0.25" />

                    <!-- Nothing to do - or, at a closed market with nothing to do, the game's reason -->
                    <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="40" VerticalAlignment="Top" MarginTop="120" Brush="Popup.Description.Text" Brush.FontSize="22" Brush.TextHorizontalAlignment="Center" Brush.FontColor="@MutedColor" Text="@EmptyText" IsVisible="@IsEmpty" />

                    <!-- The table: the sections, their title lines and their lines -->
                    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginTop="78" MarginBottom="{table_bottom}" IsVisible="@ShowTable">
                      <Children>
                        <ScrollablePanel Id="SuggestionScroller" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginRight="14" ClipRect="SuggestionClip" InnerPanel="SuggestionClip\\SuggestionList" VerticalScrollbar="..\\SuggestionScrollbar" AutoHideScrollBars="true">
                          <Children>
                            <Widget Id="SuggestionClip" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" ClipContents="true">
                              <Children>
                                <ListPanel Id="SuggestionList" DataSource="{{Sections}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" VerticalAlignment="Top" StackLayout.LayoutMethod="VerticalTopToBottom">
                                  <ItemTemplate>
                                    <!-- ===== one section ===== -->
                                    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                                      <Children>
{indent(title_line, 40)}
                                        <!-- its lines, as the folds leave them (a fold inserts / removes lines: nothing hidden is built) -->
                                        <ListPanel DataSource="{{Items}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                                          <ItemTemplate>
                                            <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                                              <Children>
{indent(main_line, 48)}
{indent(sub_line, 48)}
                                              </Children>
                                            </ListPanel>
                                          </ItemTemplate>
                                        </ListPanel>
                                      </Children>
                                    </ListPanel>
                                  </ItemTemplate>
                                </ListPanel>
                              </Children>
                            </Widget>
                          </Children>
                        </ScrollablePanel>
                        <ScrollbarWidget Id="SuggestionScrollbar" WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent" SuggestedWidth="8" HorizontalAlignment="Right" VerticalAlignment="Center" AlignmentAxis="Vertical" Handle="SuggestionScrollbarHandle" MinValue="0" MaxValue="100">
                          <Children>
                            <BrushWidget WidthSizePolicy="Fixed" HeightSizePolicy="StretchToParent" SuggestedWidth="2" HorizontalAlignment="Center" Brush="Encyclopedia.Scrollbar.Flat.Bed" />
                            <BrushWidget Id="SuggestionScrollbarHandle" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="6" SuggestedHeight="50" HorizontalAlignment="Center" Brush="Encyclopedia.Scrollbar.Flat.Handle" />
                          </Children>
                        </ScrollbarWidget>
                      </Children>
                    </Widget>
'''

def weight_row(icon):
    return f"""<ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
  <Children>
    {text(64, "@LabelText", 19, "Left", "@MutedColor", margin_left=6)}
    {text(110, "@BeforeText", 19)}
    {text(100, "@ChangeText", 19)}
    {text(110, "@AfterText", 19)}
    {text(200, "@CapacityText", 19)}
    {text(110, "@LeftText", 19, color="@LeftColor")}
    <Widget WidthSizePolicy="Fixed" SuggestedWidth="170" HeightSizePolicy="StretchToParent" MarginLeft="30">
      <Children>
        <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
          <Children>
            {icon}
            {text(0, "@SlowdownText", 19, "Left", "@SlowdownColor", margin_left=8)}
          </Children>
        </ListPanel>
        <HintWidget DataSource="{{SlowdownHint}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteBeginHint" Command.HoverEnd="ExecuteEndHint" IsDisabled="true" />
      </Children>
    </Widget>
  </Children>
</ListPanel>"""


# The game's own party-speed icon (mockup choice 10): SandBox's Map.Party.Speed.Indicator - its Default style is Native's
# General\Icons\Speed@2x (the horse of the party bar), its "Sailing" style War Sails' ship; the Sea row only shows with ships.
LAND_ICON = ('<BrushWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="26" SuggestedHeight="26" '
             'VerticalAlignment="Center" Brush="Map.Party.Speed.Indicator" />')
SEA_ICON = ('<BoolStateChangerWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="26" SuggestedHeight="26" '
            'VerticalAlignment="Center" Brush="Map.Party.Speed.Indicator" BooleanCheck="true" TrueState="Sailing" FalseState="Default" />')

footer = f"""
                    <!-- The pinned Total line: every row once, in every column (mockup choice 3) - it never scrolls -->
                    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="40" VerticalAlignment="Bottom" MarginBottom="104" MarginRight="14" IsVisible="@ShowTable">
                      <Children>
                        <Widget DataSource="{{Total}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
                          <Children>
                            <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginTop="1" Sprite="BlankWhiteSquare_9" Color="#E4C59BFF" AlphaFactor="0.08" />
                            <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="1" VerticalAlignment="Top" Sprite="BlankWhiteSquare_9" Color="#E4C59BFF" AlphaFactor="0.4" />
                            <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" StackLayout.LayoutMethod="HorizontalLeftToRight">
                              <Children>
                                <Widget WidthSizePolicy="Fixed" SuggestedWidth="{LEFT}" HeightSizePolicy="StretchToParent" ClipContents="true">
                                  <Children>
                                    <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" MarginLeft="36" StackLayout.LayoutMethod="HorizontalLeftToRight">
                                      <Children>
                                        <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="StretchToParent" Brush="Popup.Title.Text" Brush.FontSize="26" Brush.TextHorizontalAlignment="Left" Brush.TextVerticalAlignment="Center" Brush.FontColor="@HeadingColor" Text="@TitleText" />
                                        {text(0, "@TotalText", 16, "Left", "@MutedColor", margin_left=14)}
                                      </Children>
                                    </ListPanel>
                                  </Children>
                                </Widget>
{indent(denari_cell(21, 15, hint=False), 32)}
{indent(number_cells(21), 32)}
                              </Children>
                            </ListPanel>
                          </Children>
                        </Widget>
                      </Children>
                    </Widget>

                    <!-- The footer (round 4): ONLY the weight table - rows Land / Sea, columns before, change, after, capacity,
                         left, slowdown with the game's own speed icon -, the warnings and the last result beside it. The spent /
                         earned / influence / food / party / herd lines are gone (the header and the sections carry them). -->
                    <ListPanel WidthSizePolicy="Fixed" SuggestedWidth="900" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" VerticalAlignment="Bottom" MarginBottom="4" StackLayout.LayoutMethod="VerticalTopToBottom">
                      <Children>
                        <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="24" StackLayout.LayoutMethod="HorizontalLeftToRight">
                          <Children>
                            <Widget WidthSizePolicy="Fixed" SuggestedWidth="70" HeightSizePolicy="StretchToParent" />
                            {head(110, "@WeightBefore")}
                            {head(100, "@WeightChange")}
                            {head(110, "@WeightAfter")}
                            {head(200, "@WeightCapacity")}
                            {head(110, "@WeightLeft")}
                            {head(170, "@WeightSlowdown", "Left", 30)}
                          </Children>
                        </ListPanel>
                        <Widget DataSource="{{LandRow}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="32">
                          <Children>
{indent(weight_row(LAND_ICON), 28)}
                          </Children>
                        </Widget>
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="32" IsVisible="@HasSeaRow">
                          <Children>
                            <Widget DataSource="{{SeaRow}}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
                              <Children>
{indent(weight_row(SEA_ICON), 32)}
                              </Children>
                            </Widget>
                          </Children>
                        </Widget>
                      </Children>
                    </ListPanel>

                    <!-- The warnings (red) and the last result, in their own box right of the weight table -->
                    <Widget WidthSizePolicy="Fixed" SuggestedWidth="580" HeightSizePolicy="Fixed" SuggestedHeight="90" HorizontalAlignment="Right" VerticalAlignment="Bottom" MarginBottom="4" MarginRight="14" ClipContents="true">
                      <Children>
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Sprite="BlankWhiteSquare_9" Color="#FFFFFFFF" AlphaFactor="0.04" />
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="1" VerticalAlignment="Top" Sprite="BlankWhiteSquare_9" Color="#E4C59BFF" AlphaFactor="0.18" />
                        <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" VerticalAlignment="Top" MarginLeft="12" MarginRight="12" MarginTop="8" StackLayout.LayoutMethod="VerticalTopToBottom">
                          <Children>
                            <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" Brush="Popup.Description.Text" Brush.FontSize="17" Brush.TextHorizontalAlignment="Left" Brush.TextVerticalAlignment="Top" Brush.FontColor="@WarningColor" Text="@WarningText" IsVisible="@HasWarnings" />
                            <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" MarginTop="4" Brush="Popup.Description.Text" Brush.FontSize="17" Brush.TextHorizontalAlignment="Left" Brush.TextVerticalAlignment="Top" Brush.FontColor="@TextColor" Text="@StatusText" IsVisible="@HasStatus" />
                          </Children>
                        </ListPanel>
                      </Children>
                    </Widget>

                  </Children>
                </Widget>
              </Children>
            </Widget>

"""


src = open(PREFAB, encoding="utf-8").read()
start = src.index("            <!-- ============================ Suggestion tab")
if STAGE in ("a", "b"):
    end = src.index("                    <!-- The footer: spent / earned")
    src = src[:start] + tab + "\n" + src[end:]
else:
    end = src.index("            <!-- ============================ Prices tab")
    src = src[:start] + tab + footer + src[end:]
open(PREFAB, "w", encoding="utf-8", newline="\n").write(src)
ROW = LEFT + DENARI_GAP + DENARI + PARTY + PRISONERS + LAND + SEA
assert ROW <= 1506, f"the row is {ROW} px - the table has 1506"
print("wrote the Suggestion tab, stage", STAGE, "- left part", LEFT, "px, row", ROW, "px")
