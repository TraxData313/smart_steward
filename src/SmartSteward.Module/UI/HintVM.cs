using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmartSteward.UI
{
    /// <summary>
    /// A tooltip for a <c>HintWidget</c> (vanilla's HintViewModel, with a text that can change): hover shows it
    /// through the game's own hint popup, leaving hides it. Empty text = no tooltip. Used for the reason a button is
    /// greyed out (the vanilla disabled-button pattern: a HintWidget beside the button, listening to the wrapper).
    /// </summary>
    public sealed class HintVM : ViewModel
    {
        public HintVM(string text = "")
        {
            Text = text ?? "";
        }

        /// <summary>What the tooltip says; set it any time.</summary>
        public string Text { get; set; }

        public void ExecuteBeginHint()
        {
            try
            {
                if (!string.IsNullOrEmpty(Text))
                    MBInformationManager.ShowHint(Text);
            }
            catch
            {
                // a tooltip is never worth a crash
            }
        }

        public void ExecuteEndHint()
        {
            try
            {
                MBInformationManager.HideInformations();
            }
            catch
            {
                // nothing shown
            }
        }
    }
}
