using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Timelapse.Util
{
    public abstract class DataEntryControl
    {
        protected Controls ControlsPanel { get; private set; }
        protected MenuItem MenuItemPropagateFromLastValue { get; private set; }
        protected MenuItem MenuItemCopyForward { get; private set; }
        protected MenuItem MenuItemCopyCurrentValue { get; private set; }
        protected string PassingContentValue { get; set; }

        /// <summary>Gets or sets the content of the note</summary>
        public abstract string Content { get; set; }

        /// <summary>Gets or sets a value indicating whether the note contents are copyable.</summary>
        public bool Copyable { get; set; }

        public bool CopyForwardEnabled
        {
            get { return this.MenuItemCopyForward.IsEnabled; }
            set { this.MenuItemCopyForward.IsEnabled = value; }
        }

        /// <summary>Gets the container that holds the control.</summary>
        public StackPanel Container { get; private set; }

        /// <summary>Gets the data label which corresponds to this control.</summary>
        public string DataLabel { get; private set; }

        public bool PropagateFromLastValueEnabled
        {
            get { return this.MenuItemPropagateFromLastValue.IsEnabled; }
            set { this.MenuItemPropagateFromLastValue.IsEnabled = value; }
        }

        /// <summary>Gets or sets a value indicating whether the control's content is user-editable</summary>
        public abstract bool ReadOnly { get; set; }

        /// <summary>Gets or sets the tooltip attached to the Note</summary>
        public string Tooltip
        {
            get { return (string)this.Container.ToolTip; }
            set { this.Container.ToolTip = value; }
        }

        public abstract void Focus();

        protected DataEntryControl(string dataLabel, Controls dataEntryControls, bool createContextMenu)
        {
            // Set these parameters so they are available in the derived objects
            this.ControlsPanel = dataEntryControls;

            // The dataLabel used as the column header in the spreadsheet and database column name
            this.DataLabel = dataLabel;

            // Create the stack panel
            this.Container = new StackPanel();
            Style style = dataEntryControls.FindResource("StackPanelCodeBar") as Style;
            this.Container.Style = style;

            // Create the context menu
            if (createContextMenu)
            {
                this.MenuItemPropagateFromLastValue = new MenuItem();
                this.MenuItemPropagateFromLastValue.IsCheckable = false;
                this.MenuItemPropagateFromLastValue.Header = "Propagate from the last non-empty value to here";
                this.MenuItemPropagateFromLastValue.Click += new RoutedEventHandler(this.MenuItemPropagateFromLastValue_Click);

                this.MenuItemCopyForward = new MenuItem();
                this.MenuItemCopyForward.IsCheckable = false;
                this.MenuItemCopyForward.Header = "Copy forward to end";
                this.MenuItemCopyForward.ToolTip = "The value of this field will be copied forward from this image to the last image in this set";
                this.MenuItemCopyForward.Click += new RoutedEventHandler(this.MenuItemPropagateForward_Click);

                this.MenuItemCopyCurrentValue = new MenuItem();
                this.MenuItemCopyCurrentValue.IsCheckable = false;
                this.MenuItemCopyCurrentValue.Header = "Copy to all";
                this.MenuItemCopyCurrentValue.Click += new RoutedEventHandler(this.MenuItemCopyCurrentValue_Click);

                ContextMenu menu = new ContextMenu();
                menu.Items.Add(this.MenuItemPropagateFromLastValue);
                menu.Items.Add(this.MenuItemCopyForward);
                menu.Items.Add(this.MenuItemCopyCurrentValue);

                this.Container.PreviewMouseRightButtonDown += this.Container_PreviewMouseRightButtonDown;
                this.Container.ContextMenu = menu;
            }
        }

        // Menu selections for propagating or copying the current value of this control to all images
        protected virtual void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            bool isflag = false;
            this.PassingContentValue = this.ControlsPanel.Propagate.FromLastValue(this.DataLabel, checkForZero, isflag);
            this.Refresh();
        }

        // Copy the current value of this control to all images
        protected virtual void MenuItemCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            this.ControlsPanel.Propagate.CopyValues(this.DataLabel, checkForZero);
        }

        // Propagate the current value of this control forward from this point across the current set of filtered images
        protected virtual void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            this.ControlsPanel.Propagate.Forward(this.DataLabel, checkForZero);
        }

        // A callback allowing us to enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Decide which context menu items to enable
            // May not be able to do this without the dbData!
            bool checkForZero = false;
            this.CopyForwardEnabled = this.ControlsPanel.Propagate.Forward_IsPossible(this.DataLabel);
            this.PropagateFromLastValueEnabled = this.ControlsPanel.Propagate.FromLastValue_IsPossible(this.DataLabel, checkForZero);
        }

        // Set the contents to whatever is in the PassingContentValue
        // Kind of a hack so we can update a field with a given value without referring to the database
        protected void Refresh()
        {
            this.Content = this.PassingContentValue;
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

        protected DataEntryControl(string dataLabel, Controls dataEntryControls, Nullable<ControlContentStyle> contentStyleName, ControlLabelStyle labelStyleName, bool createContextMenu) : 
            base(dataLabel, dataEntryControls, createContextMenu)
        {
            this.ContentControl = new TContent();

            // configure the content
            this.ContentControl.IsTabStop = true;
            if (contentStyleName.HasValue)
            {
                this.ContentControl.Style = (Style)dataEntryControls.FindResource(contentStyleName.Value.ToString());
            }
            this.ReadOnly = false;

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), rely on this
            this.ContentControl.Tag = this;

            // Create the label (which is an actual label)
            this.LabelControl = new TLabel();
            this.LabelControl.Style = (Style)dataEntryControls.FindResource(labelStyleName.ToString());

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