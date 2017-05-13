using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Controls
{
    public class TabControlWithoutHomeAndEnd : TabControl
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((e.Key == Key.End) || (e.Key == Key.Home))
            {
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
