using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data;
using System.Collections;
using System.Diagnostics;

namespace Timelapse
{
    /// <summary>
    /// This  class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class Controls : UserControl
    {
        #region Public Properties
        public Hashtable ControlFromDataLabel = new Hashtable (); // Given a key, return its associated control
        public List<MyCounter> MyCountersList = new List<MyCounter>(); // list of all our counter controls
        public WrapPanel WP { get; set; } // The wrap panel will contain all our controls. If we want to reparent things, we do it by reparenting the wrap panel
        public Propagate Propagate { get; set; }
        #endregion

        #region Constructors
        public Controls(DBData dbData)
        {
            InitializeComponent();
            WP = new WrapPanel ();
        }
        #endregion

        #region Control And Button Generation

        public void GenerateControls(DBData dbData)
        {
            const string EXAMPLE_DATE = "28-Dec-2014";
            const string EXAMPLE_TIME = "04:00 PM";

            string key = ""; // Construct a key 

            Propagate = new Propagate(dbData);
            //this.ControlGrid.Inlines.Clear();
            //this.WP.Children.Clear();
            
            DataTable sortedTemplateTable = dbData.TemplateGetSortedByControls();
            for (int i = 0; i < sortedTemplateTable.Rows.Count; i++)
            {
                // Get the values for each control
                DataRow row = sortedTemplateTable.Rows[i];
                string type = row[Constants.TYPE].ToString();
                string defaultValue = row[Constants.DEFAULT].ToString();
                string label = row[Constants.LABEL].ToString();
                string tooltip = row[Constants.TOOLTIP].ToString();
                string width = row[Constants.TXTBOXWIDTH].ToString();
                int    iwidth = (width == "") ? 0 : Convert.ToInt32(width);
                string visiblity = row[Constants.VISIBLE].ToString();
                bool   bvisiblity = ("true" == visiblity.ToLower ()) ? true : false;
                string copyable = row[Constants.COPYABLE].ToString();
                bool   bcopyable = ("true" == copyable.ToLower ()) ? true : false;
                string list = row[Constants.LIST].ToString();
                int id = Convert.ToInt32 (row[Constants.ID].ToString ()); // TODO Need to use this ID to pass between controls and data

                // Get the key
                key = (string) row[Constants.DATALABEL];
                if (type == Constants.DATE && defaultValue == "")
                {
                    defaultValue = EXAMPLE_DATE;
                }
                else if (type == Constants.TIME && defaultValue == "")
                {
                    defaultValue = EXAMPLE_TIME;
                }

                if (type == Constants.FILE || type == Constants.FOLDER || type == Constants.DATE || type == Constants.TIME || type == Constants.NOTE)
                {
                    bool createContextMenu = (type == Constants.FILE) ? false : true;
                    MyNote myNote = new MyNote(this, createContextMenu);
                    myNote.Key = key;
                    myNote.Label = label;
                    myNote.DataLabel = key; // TODO we probably don't need a datalabel and a key any more as its redundant. Should just keep the DataLabel as its the key
                    myNote.Tooltip = tooltip;
                    myNote.Width = iwidth;
                    myNote.Visible = bvisiblity;
                    myNote.Content = defaultValue;
                    myNote.ReadOnly = (type == Constants.FOLDER || type == Constants.FILE) ? true : false; // File and Folder Notes are read only i.e., non-editable by the user 
                    myNote.Copyable = bcopyable;
                    this.ControlGrid.Inlines.Add(myNote.Container);
                    //this.WP.Children.Add(myNote.Container);
                    this.ControlFromDataLabel.Add(key, myNote);
                }
                else if (type == Constants.FLAG || type == Constants.DELETEFLAG)
                {
                    MyFlag myFlag = new MyFlag(this, true);
                    myFlag.Key = key;
                    myFlag.Label = label;
                    myFlag.DataLabel = key; // TODO we probably don't need a datalabel and a key any more as its redundant. Should just keep the DataLabel as its the key
                    myFlag.Tooltip = tooltip;
                    myFlag.Width = iwidth;
                    myFlag.Visible = bvisiblity;
                    myFlag.Content = defaultValue;
                    myFlag.ReadOnly = false; // Flags are editable by the user 
                    myFlag.Copyable = bcopyable;
                    //this.WP.Children.Add(myFlag.Container);
                    this.ControlGrid.Inlines.Add(myFlag.Container);  
                    this.ControlFromDataLabel.Add(key, myFlag);
                }
                else if (type == Constants.COUNTER)
                {
                    MyCounter myCounter = new MyCounter(this, true);
                    myCounter.Key = key;
                    myCounter.Label = label;
                    myCounter.DataLabel = key; // TODO we probably don't need a datalabel and a key any more as its redundant. Should just keep the DataLabel as its the key
                    myCounter.Tooltip = tooltip;
                    myCounter.Width = iwidth;
                    myCounter.Visible = bvisiblity;
                    myCounter.Content = defaultValue;
                    myCounter.ReadOnly = false; // Couonters are editable by the user 
                    myCounter.Copyable = bcopyable;
                    this.ControlGrid.Inlines.Add(myCounter.Container);
                    //this.WP.Children.Add(myCounter.Container);
                    this.ControlFromDataLabel.Add(key, myCounter);
                }
                else if (type == Constants.FIXEDCHOICE || type == Constants.IMAGEQUALITY)
                {
                    MyFixedChoice myFixedChoice = new MyFixedChoice(this, true, list);
                    myFixedChoice.Key = key;
                    myFixedChoice.Label = label;
                    myFixedChoice.DataLabel = key; // TODO we probably don't need a datalabel and a key any more as its redundant. Should just keep the DataLabel as its the key
                    myFixedChoice.Tooltip = tooltip;
                    myFixedChoice.Width = iwidth;
                    myFixedChoice.Visible = bvisiblity;
                    myFixedChoice.Content = defaultValue;
                    myFixedChoice.ReadOnly = false; // Fixed choices are editable (by selecting a menu) by the user 
                    myFixedChoice.Copyable = bcopyable;
                    this.ControlGrid.Inlines.Add(myFixedChoice.Container);
                    //this.WP.Children.Add(myFixedChoice.Container);
                    this.ControlFromDataLabel.Add(key, myFixedChoice);
                }   
            }
            //var panel = this.ButtonLocation.Parent as Panel;
            //panel.Children.Remove(this.ButtonLocation);
            //this.ControlGrid.Inlines.Add(this.ButtonLocation);
        }
       
        public void AddButton (Control button)
        {
            this.ButtonLocation.Child=button;
        }
        #endregion ControlGeneration
    }
    #region MyControl
    // A generic control comprises a stack panel containing 
    // - a control containing at least a descriptive label 
    // - another control for displaying / entering data at a given width
    public class MyControl
    {
        #region MyControl: Private variables and constants
        protected StackPanel m_panel { get; set; }
        protected ContextMenu menu = new ContextMenu();
        protected string m_dataLabel = "";   // The dataLabel used as the column header in the spreadsheet
        protected bool m_copyable = false;   // Whether the item is copyable; used to indicate background color as well

        protected Controls mycontrols;
        protected bool createContextMenu;
        protected MenuItem miPropagateFromLastValue = new MenuItem();
        protected MenuItem miCopyForward = new MenuItem();
        protected MenuItem miCopyCurrentValue = new MenuItem();
        protected string PassingContentValue { get; set; }
        #endregion 

        #region MyControl: Public properties
        /// <summary>
        /// The container that holds the control
        /// </summary>
        public System.Windows.Controls.StackPanel Container
        {
            set { m_panel = value; }
            get { return m_panel; }
        }

        /// <summary> The Data Label that corresponds to this note </summary>
        public string DataLabel {
            get { return m_dataLabel;  }
            set { m_dataLabel = value; }}

        /// <summary>The Tooltip attached to the Note</summary>
        public string Tooltip  { set { m_panel.ToolTip = value; } }

        /// <summary> Whether the note should be visible in the interface</summary>
        public bool Visible {set { m_panel.Visibility = (value) ? Visibility.Visible : Visibility.Collapsed; }}

        /// <summary> Whether the note contents are copyable. It is virtual as its children will
        ///           make the background color reflect its copyable status </summary>
        public virtual bool Copyable 
        {
            get { return m_copyable; } 
            set { m_copyable = value;}
        }

        public bool PropagateFromLastValue_IsEnabled { 
            set {miPropagateFromLastValue.IsEnabled = value;}
            get {return miPropagateFromLastValue.IsEnabled; }
        }

        public bool CopyForward_IsEnabled
        {
            set { miCopyForward.IsEnabled = value; }
            get { return miCopyForward.IsEnabled; }
        }

        public bool CopyCurrentValue_IsEnabled
        {
            set { miCopyCurrentValue.IsEnabled = value; }
            get { return miCopyCurrentValue.IsEnabled; }
        }
        #endregion

        #region MyControl: Constructor
        public MyControl (Controls mycontrols, bool createContextMenu)
        {
            // Set these parameters so they are available in the derived objects
            this.mycontrols = mycontrols;
            this.Container = new StackPanel();
          
            // Create the stack panel and add the label and textbox to it
            Style style = mycontrols.FindResource("StackPanelCodeBar") as Style; 
            this.Container.Style = style;

            // Create the context menu
            this.createContextMenu = createContextMenu;

            if (createContextMenu)  // Create the context menu
            { 
                //MenuItem miPropagateFromLastValue = new MenuItem();
                miPropagateFromLastValue.IsCheckable = false;
                miPropagateFromLastValue.Header = "Propagate from the last non-empty value to here";
                miPropagateFromLastValue.Click += new RoutedEventHandler(miPropagateFromLastValue_Click);

                //MenuItem miCopyForward = new MenuItem();
                miCopyForward.IsCheckable = false;
                miCopyForward.Header = "Copy forward to end";
                miCopyForward.ToolTip = "The value of this field will be copied forward from this image to the last image in this set";
                miCopyForward.Click += new RoutedEventHandler(miPropagateForward_Click);

                //MenuItem miCopyCurrentValue = new MenuItem();
                miCopyCurrentValue.IsCheckable = false;
                miCopyCurrentValue.Header = "Copy to all";
                miCopyCurrentValue.Click += new RoutedEventHandler(miCopyCurrentValue_Click);

                menu.Items.Add(miPropagateFromLastValue);
                menu.Items.Add(miCopyForward);
                menu.Items.Add(miCopyCurrentValue);

                // This doesn't work, i.e., adding the cut/copy/paste submenu, although it appears.
                // Probably because I have to implement my own handlers
                //MenuItem micut = new MenuItem();
                //micut.Command = ApplicationCommands.Cut;
                //menu.Items.Add(micut);

                //menu.Items.Add(new Separator());
                //MenuItem micopy = new MenuItem();
                //micopy.Command = ApplicationCommands.Copy;
                //menu.Items.Add(micopy);

                //MenuItem mipaste = new MenuItem();
                //mipaste.Command = ApplicationCommands.Paste;
                //menu.Items.Add(mipaste);

                this.Container.PreviewMouseRightButtonDown += Container_PreviewMouseRightButtonDown;
                this.Container.ContextMenu = menu;
            }
        }
        #endregion

        #region MyControl: Protected Menu Callbacks
        // Menu selections for propagating or copying the current value of this control to all images
        protected virtual void miPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            bool isflag = false;
            this.PassingContentValue = this.mycontrols.Propagate.FromLastValue(this.DataLabel, checkForZero, isflag);
            this.Refresh();
        }

        // Copy the current value of this control to all images
        protected virtual void miCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            this.mycontrols.Propagate.CopyValues(this.DataLabel, checkForZero);
        }

        // Propagate the current value of this control forward from this point across the current set of filtered images
        protected virtual void miPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            this.mycontrols.Propagate.Forward(this.DataLabel, checkForZero);
        }
        #endregion

        #region MyControl: Protected and Virtual Methods
        // A callback allowing us to enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Decide which context menu items to enable
            // May not be able to do this without the dbData!
            bool checkForZero = false;
            this.CopyForward_IsEnabled = this.mycontrols.Propagate.Forward_IsPossible(this.DataLabel);
            this.PropagateFromLastValue_IsEnabled = this.mycontrols.Propagate.FromLastValue_IsPossible(this.DataLabel, checkForZero);
        }

        // This will allow children objects to set the contents to whatever is in the PassingContentValue
        // Kind of a hack so we can update a field with a given value without referring to the database
        protected virtual void Refresh() { }
        #endregion
    }
    #endregion

    #region Note
    // A note comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class MyNote: MyControl
    {
        #region Note: Private variables
        private string m_key = "";

        #endregion

        #region Note: Public properties
        // The individual UI controls that make up a note
        public Label LabelCtl { get; set; }
        public TextBox ContentCtl { get; set; }

        /// <summary>The Label's value</summary>
        public string Label 
        { 
            set { LabelCtl.Content = value; } 
        }

        /// <summary>The Content of the Note</summary>
        public string Content 
        { 
            set {ContentCtl.Text = value; }
            get { return ContentCtl.Text; }
        }

        /// <summary>The width of the content control</summary>
        public int Width
        {
            set { ContentCtl.Width = value; }
        }

        /// <summary>Whether the control's content is user-editable </summary>
        public bool ReadOnly
        {
            set { this.ContentCtl.IsEnabled = !value; }
        }

        /// <summary> The key used to associate the control with data </summary>
        public string Key
        {
            get { return m_key; } 
            set
            {
                m_key = value;
                this.ContentCtl.Tag = m_key;  // We also set the tag to the key so we can access the key whenever the content is changed
            }
        }

        /// <summary> Make the background color reflect its copyable status </summary>
        public override bool Copyable
        {
            get { return base.Copyable; }
            set
            {
                base.Copyable = value;
                //this.ContentCtl.Background = (m_copyable) ? Brushes.PaleGreen : Brushes.White; 
            }
        }

        #endregion

        #region Note: Public Methods
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mycontrols"></param>
        /// <param name="createContextMenu"></param>
        public MyNote (Controls mycontrols, bool createContextMenu) : base (mycontrols, createContextMenu)
        {
            // Create the label (which is an actual label)
            this.LabelCtl = new Label();
            LabelCtl.Style = mycontrols.FindResource("LabelCodeBar") as Style;
            this.LabelCtl.IsTabStop = false;

            // Create the content box (which is a text box)
            this.ContentCtl = new TextBox();

            // Modify the context menu so it can have a propage submenu
            this.ContentCtl.ContextMenu = null;

            Style style2 = mycontrols.FindResource("TextBoxCodeBar") as Style;
            this.ContentCtl.Style = style2;
            this.ContentCtl.IsTabStop = true;

            // Add the label and content controls to the stack panel (which will have been created before this in the super class)
            m_panel.Children.Add(LabelCtl);
            m_panel.Children.Add(ContentCtl);

            // Now configure the various elements
            this.m_panel.ToolTip = "Type in text";
        }
        #endregion

        #region Note: Protected Methods
        // Set the contents to whatever is in the PassingContentValue
        // Kind of a hack so we can update a field with a given value without referring to the database
        protected override void Refresh()
        {
            this.Content = this.PassingContentValue;
        }
        #endregion
    }
    #endregion

    #region Counter
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class MyCounter : MyControl
    {
        #region Counter: Private variables
        private string m_key = "";
        #endregion

        #region Counter: Public properties

        // The individual UI controls that make up a note
        public RadioButton LabelCtl { get; set; }
        public TextBox ContentCtl { get; set; }

        /// <summary>The Label's value</summary>
        public string Label
        {
            get { return  LabelCtl.Content.ToString(); }
            set { LabelCtl.Content = value; }

        }

        /// <summary>The Content of the Counter</summary>
        public string Content
        {
            set { ContentCtl.Text = value; }
            get { return ContentCtl.Text; }
        }

        /// <summary>The width of the content control</summary>
        public int Width
        {
            set { ContentCtl.Width = value; }
        }

        /// <summary>Whether the control's content is user-editable</summary>
        public bool ReadOnly
        {
            set { this.ContentCtl.IsEnabled = !value; }
        }

        /// <summary> The key used to associate the control with data </summary>
        public string Key
        {
            get { return m_key; } 
            set
            {
                m_key = value;
                this.ContentCtl.Tag = m_key;  // We also set the tag to the key so we can access the key whenever the content is changed
            }
        }

        public bool isSelected
        {
            get { return (this.LabelCtl.IsChecked.HasValue) ? (bool)this.LabelCtl.IsChecked : false; }
        }

        /// <summary> Make the background color reflect its copyable status </summary>
        public override bool Copyable
        {
            get { return base.Copyable; }
            set
            {
                base.Copyable = value;
                //this.ContentCtl.Background = (m_copyable) ? Brushes.PaleGreen : Brushes.White;
            }
        }

        #endregion

        #region Counter: Public Methods
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mycontrols"></param>
        /// <param name="createContextMenu"></param>
        public MyCounter(Controls mycontrols, bool createContextMenu): base(mycontrols, createContextMenu)
        {
            // Create and configure the label (which is a radio button)
            this.LabelCtl = new RadioButton();
            LabelCtl.Style = mycontrols.FindResource("RadioButtonCodeBar") as Style;
            this.LabelCtl.IsTabStop = false;

            // Create the content box (which is a text box)
            this.ContentCtl = new TextBox();
            Style style2 = mycontrols.FindResource("TextBoxCodeBar") as Style;
            this.ContentCtl.Style = style2;
            this.ContentCtl.IsTabStop = true;

            // Modify the context menu so it can have a propage submenu
            this.ContentCtl.ContextMenu = null;

            // Add the label and content controls to the stack panel (which will have been created before this in the super class)
            m_panel.Children.Add(LabelCtl);
            m_panel.Children.Add(ContentCtl);

            // Now configure the various elements
            this.m_panel.ToolTip = "Select the button, then click on the entity in the image to increment its count OR type in a number";

            // Make this part of a group with all the other radio buttons of this type
            this.LabelCtl.GroupName = "A";

            // Add this counter to the list of counters, so we can quickly access all counters
            this.mycontrols.MyCountersList.Add(this);

            // Change the menu text to indicate that propagate goes back to the last non-zero value
            miPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
        }
        #endregion

        #region Counter: Menu Callbacks
        // Menu selections for propagating or copying the current value of this control to all images
        // Note that this overrides the default where checkForZero is false, as Counters use '0' as the empty value
        protected override void miPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = true;
            bool isflag = false;
            this.PassingContentValue = this.mycontrols.Propagate.FromLastValue(this.DataLabel, checkForZero, isflag);
            this.Refresh();
        }

        // Copy the current value of this control to all images
        protected override void miCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = true;
            this.mycontrols.Propagate.CopyValues(this.DataLabel, checkForZero);
        }

        // Propagate the current value of this control forward from this point across the current set of filtered images
        protected override void miPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = true;
            this.mycontrols.Propagate.Forward(this.DataLabel, checkForZero);
        }

        // A callback allowing us to enable or disable particular context menu items
        protected override void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Decide which context menu items to enable
            // May not be able to do this without the dbData!
            bool checkForZero = true;
            this.CopyForward_IsEnabled = this.mycontrols.Propagate.Forward_IsPossible(this.DataLabel);
            this.PropagateFromLastValue_IsEnabled = this.mycontrols.Propagate.FromLastValue_IsPossible(this.DataLabel, checkForZero);
        }
        #endregion

        #region Counter: Protected Methods
        // Set the contents to whatever is in the PassingContentValue
        // Kind of a hack so we can update a field with a given value without referring to the database
        protected override void Refresh() 
        { 
            this.Content = this.PassingContentValue;
        }
        #endregion
    }
    #endregion

    #region FixedChoice
    // A FixedChoice comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - a combobox (containing the content) at the given width
    public class MyFixedChoice : MyControl
    {
        #region FixedChoice: Private variables
        private string m_key = "";
        #endregion

        #region FixedChoice: Public properties

        // The individual UI controls that make up a note
        public Label LabelCtl { get; set; }
        public ComboBox ContentCtl { get; set; }

        /// <summary>The Label's value</summary>
        public string Label
        {
            set { LabelCtl.Content = value; }
        }

        /// <summary>The Content of the Note</summary>
        public string Content
        {
            set { ContentCtl.Text = value; }
            get { return ContentCtl.Text; }
        }

        /// <summary>The width of the content control</summary>
        public int Width
        {
            set { ContentCtl.Width = value; }
        }

        /// <summary> Whether the control's content is user-editable</summary>
        public bool ReadOnly
        {
            set { this.ContentCtl.IsEnabled = !value; }
        }

        /// <summary> The key used to associate the control with data </summary>
        public string Key
        {
            get { return m_key; } 
            set
            {
                m_key = value;
                this.ContentCtl.Tag = m_key;  // We also set the tag to the key so we can access the key whenever the content is changed
            }
        }

        /// <summary> Make the background color reflect its copyable status </summary>
        public override bool Copyable
        {
            get { return base.Copyable; }
            set
            {
                base.Copyable = value;   
            }
        }


        #endregion

        #region FixedChoice: Public Methods
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mycontrols"></param>
        /// <param name="createContextMenu"></param>
        /// <param name="list"></param>
        public MyFixedChoice (Controls mycontrols, bool createContextMenu, string list)
            : base(mycontrols, createContextMenu)
        {
            // Create the label (which is an actual label)
            this.LabelCtl = new Label();
            LabelCtl.Style = mycontrols.FindResource("LabelCodeBar") as Style;
            this.LabelCtl.IsTabStop = false;

            // Create the content box (which is a text box)
            this.ContentCtl = new ComboBox();
            //Style style2 = mycontrols.FindResource("ComboBoxCodeBar") as Style;
            //this.ContentCtl.Style = style2;

            // The look of the combobox
            this.ContentCtl.Height = 25;
            this.ContentCtl.HorizontalAlignment = HorizontalAlignment.Left;
            this.ContentCtl.VerticalAlignment = VerticalAlignment.Center;
            this.ContentCtl.VerticalContentAlignment = VerticalAlignment.Center;
            this.ContentCtl.BorderThickness = new Thickness(1);
            this.ContentCtl.BorderBrush = Brushes.LightBlue;

            // The behaviour of the combobox
            this.ContentCtl.IsTabStop = true;
            this.ContentCtl.IsTextSearchEnabled = true;
            this.ContentCtl.Focusable=true;
            this.ContentCtl.IsReadOnly = true;
            this.ContentCtl.IsEditable = false;

            // Callback used to allow Enter to select the highlit item
            this.ContentCtl.PreviewKeyDown += ContentCtl_PreviewKeyDown; 

            // Add items to the combo box
            List<string> result = list.Split(new char[] { '|' }).ToList();
            foreach (string str in result)
            {
                this.ContentCtl.Items.Add(str.Trim());
            }
            // Add a separator and an empty field to the list, so the user can return it to an empty state. Note that this means ImageQuality can also be empty.. not sure if this is a good thing
            this.ContentCtl.Items.Add(new Separator());
            this.ContentCtl.Items.Add("");     

            this.ContentCtl.SelectedIndex = 0;

            // Add the label and content controls to the stack panel (which will have been created before this in the super class)
            m_panel.Children.Add(LabelCtl);
            m_panel.Children.Add(ContentCtl);

            // Now configure the various elements
            this.m_panel.ToolTip = "Type in text";
        }

        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return || e.Key == System.Windows.Input.Key.Enter)
            {
                ComboBox cb = sender as ComboBox;
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    ComboBoxItem cbi = (ComboBoxItem) (cb.ItemContainerGenerator.ContainerFromIndex(i));
                    if (cbi.IsHighlighted)
                    {
                        cb.SelectedValue = cbi.Content.ToString();
                    }
                }
                
            }   
        }
        #endregion

        #region FixedChoice: Protected Methods
        // Set the contents to whatever is in the PassingContentValue
        // Kind of a hack so we can update a field with a given value without referring to the database
        protected override void Refresh()
        {
            this.Content = this.PassingContentValue;
        }
        #endregion
    }
    #endregion

    #region Flag
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbobox (the content) at the given width
    public class MyFlag : MyControl
    {
        #region Flag: Private variables
        private string m_key = "";

        #endregion

        #region Flag: Public properties
        // The individual UI controls that make up a flag
        public Label LabelCtl { get; set; }
        public CheckBox ContentCtl { get; set; }

        /// <summary>The Label's value</summary>
        public string Label
        {
            set { LabelCtl.Content = value; }
        }

        /// <summary>The Content of the Note</summary>
        public string Content
        {
            set { ContentCtl.IsChecked = (value == "true") ? true : false;}
            get { return ((bool) ContentCtl.IsChecked) ? "true" : "false"; }
        }

        /// <summary>The width of the content control</summary>
        public int Width
        {
            set { ContentCtl.Width = value;}   
        }

        /// <summary>Whether the control's content is user-editable </summary>
        public bool ReadOnly
        {
            set { this.ContentCtl.IsEnabled = !value; }
        }

        /// <summary> The key used to associate the control with data </summary>
        public string Key
        {
            get { return m_key; }
            set
            {
                m_key = value;
                this.ContentCtl.Tag = m_key;  // We also set the tag to the key so we can access the key whenever the content is changed
            }
        }

        /// <summary> Make the background color reflect its copyable status </summary>
        public override bool Copyable
        {
            get { return base.Copyable; }
            set
            {
                base.Copyable = value;
                //this.ContentCtl.Background = (m_copyable) ? Brushes.PaleGreen : Brushes.White; 
            }
        }

        #endregion

        #region Flag: Public Methods
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mycontrols"></param>
        /// <param name="createContextMenu"></param>
        public MyFlag(Controls mycontrols, bool createContextMenu)
            : base(mycontrols, createContextMenu)
        {
            // Create the label (which is an actual label)
            this.LabelCtl = new Label();
            LabelCtl.Style = mycontrols.FindResource("LabelCodeBar") as Style;
            this.LabelCtl.IsTabStop = false;

            // Create the content box (which is a text box)
            this.ContentCtl = new CheckBox();
            Style style2 = mycontrols.FindResource("FlagCodeBar") as Style;
            this.ContentCtl.Style = style2;
            this.ContentCtl.IsTabStop = true;

            // Add the label and content controls to the stack panel (which will have been created before this in the super class)
            m_panel.Children.Add(LabelCtl);
            m_panel.Children.Add(ContentCtl);

            // Now configure the various elements
            this.m_panel.ToolTip = "Toggle between true (checked) and false (unchecked)";

            // Change the menu text to indicate that propagate goes back to the last non-zero value
            miPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
        }

        protected override void miPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            bool isflag = true;
            this.PassingContentValue = this.mycontrols.Propagate.FromLastValue(this.DataLabel, checkForZero, isflag);
            this.Refresh();
        }
        #endregion

        #region Flag: Protected Methods
        // Set the contents to whatever is in the PassingContentValue
        // Kind of a hack so we can update a field with a given value without referring to the database
        protected override void Refresh()
        {
            this.Content = this.PassingContentValue;
        }
        #endregion
    }
    #endregion
}
