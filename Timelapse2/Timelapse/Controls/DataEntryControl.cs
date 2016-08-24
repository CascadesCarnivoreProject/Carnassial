using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

namespace Timelapse.Controls
{
    public abstract class DataEntryControl
    {
        /// <summary>Gets or sets the content of the note</summary>
        public abstract string Content { get; set; }

        /// <summary>Gets or sets a value indicating whether the note contents are copyable.</summary>
        public bool Copyable { get; set; }

        /// <summary>Gets the container that holds the control.</summary>
        public StackPanel Container { get; private set; }

        /// <summary>Gets the data label which corresponds to this control.</summary>
        public string DataLabel { get; private set; }

        /// <summary>Gets or sets a value indicating whether the control's content is user-editable</summary>
        public abstract bool ReadOnly { get; set; }

        /// <summary>Gets or sets the tooltip attached to the Note</summary>
        public string Tooltip
        {
            get { return (string)this.Container.ToolTip; }
            set { this.Container.ToolTip = value; }
        }

        public abstract void Focus();

        protected DataEntryControl(string dataLabel, DataEntryControls styleProvider)
        {
            // The dataLabel used as the column header in the spreadsheet and database column name
            this.DataLabel = dataLabel;

            // Create the stack panel
            this.Container = new StackPanel();
            Style style = styleProvider.FindResource(Constants.ControlStyle.StackPanelCodeBar) as Style;
            this.Container.Style = style;

            // use the containers's tag to point back to this so event handlers can access the DataEntryControl
            // this is needed by callbacks such as DataEntryHandler.Container_PreviewMouseRightButtonDown() and TimelapseWindow.CounterControl_MouseLeave()
            this.Container.Tag = this;
        }
    }

    // A generic control comprises a stack panel containing 
    // - a control containing at least a descriptive label 
    // - another control for displaying / entering data at a given width
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "StyleCop limitation.")]
    public abstract class DataEntryControl<TContent, TLabel> : DataEntryControl
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        public TContent ContentControl { get; private set; }

        /// <summary>Gets or sets the control label's value</summary>
        public string Label
        {
            get { return (string)this.LabelControl.Content; }
            set { this.LabelControl.Content = value; }
        }

        public TLabel LabelControl { get; private set; }

        /// <summary>Gets or sets a value indicating whether the control's content is user-editable</summary>
        public override bool ReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        /// <summary>Gets or sets the width of the content control</summary>
        public int Width
        {
            get { return (int)this.ContentControl.Width; }
            set { this.ContentControl.Width = value; }
        }

        protected DataEntryControl(string dataLabel, DataEntryControls styleProvider, Nullable<ControlContentStyle> contentStyleName, ControlLabelStyle labelStyleName) : 
            base(dataLabel, styleProvider)
        {
            this.ContentControl = new TContent();

            // configure the content
            this.ContentControl.IsTabStop = true;
            if (contentStyleName.HasValue)
            {
                this.ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            this.ReadOnly = false;

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), rely on this
            this.ContentControl.Tag = this;

            // Create the label (which is an actual label)
            this.LabelControl = new TLabel();
            this.LabelControl.Style = (Style)styleProvider.FindResource(labelStyleName.ToString());

            // add the label and content to the stack panel
            this.Container.Children.Add(this.LabelControl);
            this.Container.Children.Add(this.ContentControl);
        }

        public override void Focus()
        {
            this.ContentControl.Focus();
        }
    }
}