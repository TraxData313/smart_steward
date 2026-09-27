namespace SmartSteward.Core.Pricing
{
    /// <summary>
    /// The game's own price for ONE more unit (DESIGN §4.1, RESEARCH §8). The Module implements it with
    /// <c>TradeItemPriceFactorModel.GetPrice(element, MainParty, settlement.Party, isSelling,
    /// inStoreValue + categoryStoreValueDelta, supply, demand)</c>; tests use a fake.
    /// </summary>
    /// <remarks>
    /// Core does the bookkeeping of the walk: in a town, every unit bought moves its item CATEGORY's
    /// in-store value by <c>−StoreValueStep</c> and every unit sold by <c>+StoreValueStep</c>, and Core
    /// passes the running sum. A village's price is flat for the whole visit — its implementation simply
    /// ignores the delta. (The sell formula adds the unit being sold itself; that stays inside the model.)
    /// </remarks>
    public interface IPriceOracle
    {
        /// <param name="stackKey">The <c>ItemStack.Key</c> of the element (item + modifier).</param>
        /// <param name="isSelling">True = what the market pays the party; false = what the party pays.</param>
        /// <param name="categoryStoreValueDelta">How far the plan has already moved the in-store value of
        /// this item's category (0 before any trade in it).</param>
        int GetPrice(string stackKey, bool isSelling, int categoryStoreValueDelta);
    }
}
