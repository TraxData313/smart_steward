using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// The twin of vanilla's private <c>InventoryScreenHelper.MerchantInventoryListener</c> (RESEARCH §9): the
    /// headless trade's view of the settlement's purse. DoneLogic pays the player at most <see cref="GetGold"/> and
    /// writes the merchant's new gold through <see cref="SetGold"/> (ChangeGold clamps at 0).
    /// </summary>
    internal sealed class StewardMerchantListener : InventoryListener
    {
        private readonly SettlementComponent _settlementComponent;

        public StewardMerchantListener(SettlementComponent settlementComponent)
        {
            _settlementComponent = settlementComponent;
        }

        public override TextObject GetTraderName() => _settlementComponent.Owner.Name;

        public override PartyBase GetOppositeParty() => _settlementComponent.Owner;

        public override int GetGold() => _settlementComponent.Gold;

        public override void SetGold(int gold) => _settlementComponent.ChangeGold(gold - _settlementComponent.Gold);

        /// <summary>Never called by InventoryLogic (vanilla's throws NotImplementedException).</summary>
        public override void OnTransaction()
        {
        }
    }
}
