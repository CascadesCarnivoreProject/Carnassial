﻿using Carnassial.Database;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Controls
{
    public abstract class DataEntryControl
    {
        /// <summary>Gets the value of the control</summary>
        public abstract string Content { get; }

        /// <summary>Gets or sets a value indicating whether the control's content is user editable</summary>
        public abstract bool ContentReadOnly { get; set; }

        /// <summary>Gets or sets a value indicating whether the control's content is copyable.</summary>
        public bool Copyable { get; set; }

        /// <summary>Gets the container that holds the control.</summary>
        public StackPanel Container { get; private set; }

        /// <summary>Gets the data label which corresponds to this control.</summary>
        public string DataLabel { get; private set; }

        public string DefaultValue { get; private set; }

        public abstract void Focus(DependencyObject focusScope);

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider)
        {
            // populate properties from database definition of control
            // this.Content and Tooltip can't be set, however, as the caller hasn't instantiated the content control yet
            this.Copyable = control.Copyable;
            this.DataLabel = control.DataLabel;
            this.DefaultValue = control.DefaultValue;

            // Create the stack panel
            this.Container = new StackPanel();
            Style style = styleProvider.FindResource(Constant.ControlStyle.ContainerStyle) as Style;
            this.Container.Style = style;

            // use the containers's tag to point back to this so event handlers can access the DataEntryControl
            // this is needed by callbacks such as DataEntryHandler.Container_PreviewMouseRightButtonDown() and CarnassialWindow.CounterControl_MouseLeave()
            this.Container.Tag = this;
        }

        public abstract void SetContentAndTooltip(string valueAsString);
        public abstract void SetValue(object valueAsObject);
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

        /// <summary>Gets the control label's value</summary>
        public string Label
        {
            get { return (string)this.LabelControl.Content; }
        }

        public TLabel LabelControl { get; private set; }

        /// <summary>Gets or sets the width of the content control</summary>
        public long Width
        {
            get { return (long)this.ContentControl.Width; }
            set { this.ContentControl.Width = value; }
        }

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider, Nullable<ControlContentStyle> contentStyleName, ControlLabelStyle labelStyleName) : 
            base(control, styleProvider)
        {
            this.ContentControl = new TContent();

            // configure the content
            this.ContentControl.IsTabStop = true;
            if (contentStyleName.HasValue)
            {
                this.ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            this.ContentReadOnly = false;
            this.SetContentAndTooltip(control.DefaultValue);
            this.Width = control.Width;

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just ContentControl
            // the data update callback for each control type in CarnassialWindow, such as NoteControl_TextChanged(), relies on this
            this.ContentControl.Tag = this;

            // Create the label (which is an actual label)
            this.LabelControl = new TLabel();
            this.LabelControl.Content = control.Label;
            this.LabelControl.Style = (Style)styleProvider.FindResource(labelStyleName.ToString());
            this.LabelControl.ToolTip = control.Tooltip;

            // add the label and content to the stack panel
            this.Container.Children.Add(this.LabelControl);
            this.Container.Children.Add(this.ContentControl);
        }

        public override void Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with CarnassialWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, this.ContentControl);
        }
    }
}