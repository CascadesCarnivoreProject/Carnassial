using System.Windows;
using System.Windows.Input;

namespace Carnassial
{
    public class WindowWithSystemMenu : Window
    {
        protected void CloseWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        protected void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        protected void MaximizeWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
        }

        protected void MinimizeWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        protected void RestoreWindow_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
        }

        protected void ShowSystemMenu_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.ShowSystemMenu(this, Mouse.GetPosition(this));
        }
    }
}
