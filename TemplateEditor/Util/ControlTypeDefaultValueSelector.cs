using Carnassial.Data;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Editor.Util
{
    internal class ControlTypeDefaultValueSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ControlRow control)
            {
                switch (control.Type)
                {
                    case ControlType.FixedChoice:
                        return (DataTemplate)App.Current.MainWindow.FindResource(EditorConstant.Resources.DefaultValueChoiceComboBox);
                    case ControlType.Flag:
                        return (DataTemplate)App.Current.MainWindow.FindResource(EditorConstant.Resources.DefaultValueFlagComboBox);
                    default:
                        return (DataTemplate)App.Current.MainWindow.FindResource(EditorConstant.Resources.DefaultValueTextBox);
                }
            }

            return base.SelectTemplate(item, container);
        }
    }
}
