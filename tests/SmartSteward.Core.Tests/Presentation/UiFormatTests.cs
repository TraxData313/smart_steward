using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>How the window writes numbers (PLAN step 7) — only glyphs the game's UI fonts carry.</summary>
public class UiFormatTests
{
    private const string M = "–"; // the en-dash minus

    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(12400, "12,400")]
    [InlineData(-1470, M + "1,470")]
    [InlineData(1000000, "1,000,000")]
    public void Money_groups_thousands_and_uses_the_en_dash_minus(long value, string expected) =>
        Assert.Equal(expected, UiFormat.Money(value));

    [Theory]
    [InlineData(1470, "+1,470")]
    [InlineData(-1470, M + "1,470")]
    [InlineData(0, "0")]
    public void Signed_money_shows_plus_for_gains(long value, string expected) =>
        Assert.Equal(expected, UiFormat.SignedMoney(value));

    [Theory]
    [InlineData(7, "+7")]
    [InlineData(-5, M + "5")]
    [InlineData(0, "0")]
    public void Signed_count_is_the_change_column(int value, string expected) =>
        Assert.Equal(expected, UiFormat.SignedCount(value));

    [Fact]
    public void Range_is_one_number_or_low_dash_high()
    {
        Assert.Equal("11", UiFormat.Range(11, 11));
        Assert.Equal("180–240", UiFormat.Range(180, 240));
        Assert.Equal("180–240", UiFormat.Range(240, 180));
        Assert.Equal("1,200–1,450", UiFormat.Range(1200, 1450));
    }

    [Fact]
    public void Price_cell_is_units_times_price_equals_the_signed_total()
    {
        Assert.Equal("3 × 180–240 = " + M + "630", UiFormat.PriceCell(3, 180, 240, -630));
        Assert.Equal("5 × 48 = +240", UiFormat.PriceCell(5, 48, 48, 240));
        Assert.Equal("", UiFormat.PriceCell(0, 0, 0, 0));
    }

    [Theory]
    [InlineData(-120.54, M + "120.5")]
    [InlineData(3.0, "+3")]
    [InlineData(0.04, "0")]
    [InlineData(-1500.0, M + "1,500")]
    public void Weight_is_signed_with_one_decimal_at_most(double kg, string expected) =>
        Assert.Equal(expected, UiFormat.SignedWeight(kg));

    [Fact]
    public void Influence_and_days()
    {
        Assert.Equal("+2.4", UiFormat.SignedInfluence(2.36));
        Assert.Equal("+1", UiFormat.SignedInfluence(1.0));
        Assert.Equal("40", UiFormat.Days(40.9));
        Assert.Equal("0", UiFormat.Days(-3));
        Assert.Equal("—", UiFormat.Days(null));
    }

    [Theory]
    [InlineData(1.2, "1.2")]
    [InlineData(2.0, "2")]
    [InlineData(0.85, "0.85")]
    [InlineData(0.8000001, "0.8")]
    public void Decimal_drops_trailing_zeros(double value, string expected) =>
        Assert.Equal(expected, UiFormat.Decimal(value));

    [Theory]
    [InlineData("", true, null)]
    [InlineData("   ", true, null)]
    [InlineData("12", true, 12)]
    [InlineData(" 1,200 ", true, 1200)]
    [InlineData("1.200", true, 1200)]
    [InlineData("007", true, 7)]
    [InlineData("0", true, 0)]
    [InlineData("-5", false, null)]
    [InlineData("12a", false, null)]
    [InlineData("1e3", false, null)]
    [InlineData(",", false, null)]
    public void A_typed_base_is_empty_digits_or_rejected(string text, bool ok, int? expected)
    {
        Assert.Equal(ok, UiFormat.TryParseBase(text, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void A_huge_base_saturates_instead_of_overflowing()
    {
        Assert.True(UiFormat.TryParseBase("99999999999999999", out var value));
        Assert.Equal(int.MaxValue, value);
    }

    [Theory]
    [InlineData("-1", true, -1)]
    [InlineData("–1", true, -1)]
    [InlineData("500", true, 500)]
    [InlineData("1,000,000", true, 1000000)]
    [InlineData("", false, 0)]
    [InlineData("-", false, 0)]
    [InlineData("1.5x", false, 0)]
    public void A_typed_whole_number_may_be_negative(string text, bool ok, long expected)
    {
        Assert.Equal(ok, UiFormat.TryParseWhole(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1.2", true, 1.2)]
    [InlineData("1,2", true, 1.2)]
    [InlineData("2", true, 2.0)]
    [InlineData("0.85", true, 0.85)]
    [InlineData(".5", true, 0.5)]
    [InlineData("1.", true, 1.0)]
    [InlineData("", false, 0.0)]
    [InlineData(".", false, 0.0)]
    [InlineData("1.2.3", false, 0.0)]
    [InlineData("-1", false, 0.0)]
    [InlineData("abc", false, 0.0)]
    public void A_typed_decimal_takes_either_separator(string text, bool ok, double expected)
    {
        Assert.Equal(ok, UiFormat.TryParseDecimal(text, out var value));
        Assert.Equal(expected, value, 6);
    }

    [Fact]
    public void Every_symbol_is_one_the_game_fonts_have()
    {
        // FiraSansExtraCondensed-Regular and Galahad (.fnt, checked in step 7) carry these; not → − ≈ ⟲ ▸.
        var allowed = new[] { '–', '—', '×', '·', '»' };
        foreach (var s in new[] { UiFormat.Minus, UiFormat.RangeDash, UiFormat.Times, UiFormat.Dot, UiFormat.Arrow, UiFormat.None })
            Assert.Contains(s[0], allowed);
    }

    [Fact]
    public void Colours_follow_the_direction_and_the_deal()
    {
        Assert.Equal(UiColors.Buy, UiColors.ForChange(3));
        Assert.Equal(UiColors.Sell, UiColors.ForChange(-1));
        Assert.Equal(UiColors.Muted, UiColors.ForChange(0));
        Assert.Equal(UiColors.Buy, UiColors.ForGoldChange(100));
        Assert.Equal(UiColors.Warning, UiColors.ForGoldChange(-100));
        Assert.Equal(UiColors.Muted, UiColors.ForGoldChange(0));
        foreach (var c in new[] { UiColors.Text, UiColors.Buy, UiColors.Sell, UiColors.Muted, UiColors.Placeholder,
                     UiColors.Warning, UiColors.Heading, UiColors.Link })
            Assert.Matches("^#[0-9A-F]{8}$", c); // Gauntlet's Color.ConvertStringToColor
    }

    [Fact]
    public void Ctrl_means_all_shift_five_else_one()
    {
        Assert.Equal(EditSize.One, UiInput.EditSizeFor(false, false));
        Assert.Equal(EditSize.Five, UiInput.EditSizeFor(true, false));
        Assert.Equal(EditSize.All, UiInput.EditSizeFor(false, true));
        Assert.Equal(EditSize.All, UiInput.EditSizeFor(true, true));
    }
}
