using Carnassial.Util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Carnassial.Controls
{
    public partial class DateTimeOffsetPicker : UserControl
    {
        public static readonly RoutedEvent FormatChangedEvent = EventManager.RegisterRoutedEvent("FormatChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DateTimeOffsetPicker));
        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register("Format", typeof(string), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(Constant.Time.DateTimeDisplayFormat, DateTimeOffsetPicker.OnDateTimeFormatChanged));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(DateTimeOffset), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(DateTimeOffset.MaxValue, null, DateTimeOffsetPicker.SetMaximumDateTime));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(DateTimeOffset), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(Constant.ControlDefault.DateTimeValue, null, DateTimeOffsetPicker.SetMinimumDateTime));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(DateTimeOffset), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(Constant.ControlDefault.DateTimeValue, DateTimeOffsetPicker.DateTimeChanged, DateTimeOffsetPicker.SetDateTime));

        private string inputDateFormat;
        private TextBoxUpDownAdorner upDownButtons;

        public DateTimeOffsetPicker()
        {
            this.InitializeComponent();

            this.Calendar.SelectedDatesChanged += this.Calendar_SelectedDatesChanged;
            this.DateTimeDisplay.GotFocus += this.DateTimeDisplay_GotFocus;
            this.DateTimeDisplay.LostFocus += this.DateTimeDisplay_LostFocus;
            this.DateTimeDisplay.PreviewMouseUp += this.DateTimeDisplay_PreviewMouseUp;
            this.DateTimeDisplay.PreviewKeyDown += this.DateTimeDisplay_PreviewKeyDown;
            this.DateTimeDisplay.TextChanged += this.DateTimeDisplay_TextChanged;

            this.Loaded += (s, e) =>
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.DateTimeDisplay);
                if (adornerLayer != null)
                {
                    adornerLayer.Add(this.upDownButtons = new TextBoxUpDownAdorner(this.DateTimeDisplay));
                    this.upDownButtons.Button_Clicked += (textBox, direction) => { this.OnUpDown(direction); };
                }
            };
        }

        public event RoutedEventHandler DateTimeFormatChanged
        {
            add { this.AddHandler(FormatChangedEvent, value); }
            remove { this.RemoveHandler(FormatChangedEvent, value); }
        }

        public string Format
        {
            get { return Convert.ToString(this.GetValue(DateTimeOffsetPicker.FormatProperty)); }
            set { this.SetValue(DateTimeOffsetPicker.FormatProperty, value); }
        }

        public DateTimeOffset Maximum
        {
            get { return (DateTimeOffset)this.GetValue(DateTimeOffsetPicker.MaximumProperty); }
            set { this.SetValue(DateTimeOffsetPicker.MaximumProperty, value); }
        }

        public DateTimeOffset Minimum
        {
            get { return (DateTimeOffset)this.GetValue(DateTimeOffsetPicker.MinimumProperty); }
            set { this.SetValue(DateTimeOffsetPicker.MinimumProperty, value); }
        }

        public bool ShowCalendarButton
        {
            get { return this.CalendarButton.Visibility == Visibility.Visible; }
            set { this.CalendarButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        public DateTimeOffset Value
        {
            get { return (DateTimeOffset)this.GetValue(DateTimeOffsetPicker.ValueProperty); }
            set { this.SetValue(DateTimeOffsetPicker.ValueProperty, value); }
        }

        public event Action<DateTimeOffsetPicker, DateTimeOffset> ValueChanged;

        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            this.CalendarButton.IsChecked = false;
            this.Value = new DateTimeOffset(this.Calendar.SelectedDate.Value.Date + this.Value.TimeOfDay, this.Value.Offset);
        }

        private DateTimeOffset ChangeDateTimePart(int selectionStart, int increment)
        {
            if (selectionStart < 0 || selectionStart > this.Format.Length - 1)
            {
                throw new ArgumentOutOfRangeException("selectionStart");
            }

            DateTimeOffset dateTime = this.ParseDateTime(false) ?? this.Value;
            switch (this.Format.Substring(selectionStart, 1))
            {
                case "d":
                    dateTime = dateTime.AddDays(increment);
                    break;
                case "f":
                    dateTime = dateTime.AddMilliseconds(increment);
                    break;
                case "h":
                case "H":
                    dateTime = dateTime.AddHours(increment);
                    break;
                case "K":
                    dateTime = dateTime.SetOffset(dateTime.Offset + TimeSpan.FromTicks(increment * Constant.Time.UtcOffsetGranularity.Ticks));
                    break;
                case "m":
                    dateTime = dateTime.AddMinutes(increment);
                    break;
                case "M":
                    dateTime = dateTime.AddMonths(increment);
                    break;
                case "s":
                    dateTime = dateTime.AddSeconds(increment);
                    break;
                case "y":
                    dateTime = dateTime.AddYears(increment);
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled increment {0}.", this.Format.Substring(selectionStart, 1)));
            }

            if (dateTime < this.Minimum)
            {
                return this.Minimum;
            }
            if (dateTime > this.Maximum)
            {
                return this.Maximum;
            }
            return dateTime;
        }

        private static void DateTimeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)obj;

            DateTimeOffset dateTime = (DateTimeOffset)args.NewValue;
            dateTimeOffsetPicker.Calendar.DisplayDate = dateTime.DateTime;
            dateTimeOffsetPicker.Calendar.SelectedDate = dateTime.Date;
            dateTimeOffsetPicker.DateTimeDisplay.Text = dateTime.ToString(dateTimeOffsetPicker.Format);

            if (dateTimeOffsetPicker.ValueChanged != null)
            {
                dateTimeOffsetPicker.ValueChanged.Invoke(dateTimeOffsetPicker, dateTimeOffsetPicker.Value);
            }
        }

        private void DateTimeDisplay_GotFocus(object sender, RoutedEventArgs e)
        {
            this.FocusOnDateTimePart(this.DateTimeDisplay.SelectionStart);
            e.Handled = true;
        }

        private void DateTimeDisplay_LostFocus(object sender, RoutedEventArgs e)
        {
            this.DateTimeDisplay.Text = this.Value.ToString(this.Format);

            // if a field loses focus and the user then clicks the same field again, the selection is cleared causing the adnorner not to appear
            // To fix, clear selection in advance.
            this.DateTimeDisplay.SelectionLength = 0;
        }

        private void DateTimeDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (this.ParseDateTime(false) == null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Up:
                    this.OnUpDown(Utilities.GetIncrement(true, Keyboard.Modifiers));
                    break;
                case Key.Down:
                    this.OnUpDown(Utilities.GetIncrement(false, Keyboard.Modifiers));
                    break;
                case Key.Left:
                    if (Keyboard.Modifiers != ModifierKeys.None)
                    {
                        return;
                    }
                    this.SelectDateTimePart(Direction.Previous);
                    break;
                case Key.Right:
                    if (Keyboard.Modifiers != ModifierKeys.None)
                    {
                        return;
                    }
                    this.SelectDateTimePart(Direction.Next);
                    break;
                default:
                    char nextChar = '\0';
                    if (this.DateTimeDisplay.SelectionStart < this.DateTimeDisplay.Text.Length)
                    {
                        nextChar = this.DateTimeDisplay.Text[this.DateTimeDisplay.SelectionStart];
                    }

                    if ((e.Key == Key.OemMinus || e.Key == Key.Subtract || e.Key == Key.OemQuestion || e.Key == Key.Divide) &&
                        ((nextChar == '/' || nextChar == '-') || (e.Key == Key.Space && nextChar == ' ') || (e.Key == Key.OemSemicolon && nextChar == ':')))
                    {
                        this.SelectDateTimePart(Direction.Next);
                    }
                    else
                    {
                        return;
                    }
                    break;
            }
            e.Handled = true;
        }

        private void DateTimeDisplay_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.DateTimeDisplay.SelectionLength == 0)
            {
                this.FocusOnDateTimePart(this.DateTimeDisplay.SelectionStart);
            }
        }

        private void DateTimeDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            Nullable<DateTimeOffset> dateTime = this.ParseDateTime(true);
            if (dateTime.HasValue)
            {
                this.Value = dateTime.Value;
            }
        }

        private bool FocusOnDateTimePart(int selectionStart)
        {
            Nullable<DateTimeOffset> dateTime = this.ParseDateTime(true);
            if (dateTime.HasValue)
            {
                string newText = dateTime.Value.ToString(this.Format);
                if (this.DateTimeDisplay.Text != newText)
                {
                    this.DateTimeDisplay.Text = newText;
                }
            }

            // Find beginning of field to select
            string dateTimeFormat = this.Format;
            if (selectionStart > dateTimeFormat.Length - 1)
            {
                selectionStart = dateTimeFormat.Length - 1;
            }
            char firstCharacter = dateTimeFormat[selectionStart];
            while (!char.IsLetter(firstCharacter) && selectionStart + 1 < dateTimeFormat.Length)
            {
                ++selectionStart;
                firstCharacter = dateTimeFormat[selectionStart];
            }
            while (selectionStart > 0 && dateTimeFormat[selectionStart - 1] == firstCharacter)
            {
                --selectionStart;
            }

            int selectionLength = 1;
            while (selectionStart + selectionLength < dateTimeFormat.Length && dateTimeFormat[selectionStart + selectionLength] == firstCharacter)
            {
                ++selectionLength;
            }

            // don't select AM/PM: we have no interface to change it.
            if (firstCharacter == 't')
            {
                return false;
            }

            this.DateTimeDisplay.Focus();
            this.DateTimeDisplay.Select(selectionStart, selectionLength);
            return true;
        }

        // get location of next or previous date time field
        // returns -1 if there is no next/previous field
        private int GetIndexOfNextDateTimePart(int selectionStart, Direction direction)
        {
            if (selectionStart < 0 || selectionStart > this.Format.Length - 1)
            {
                throw new ArgumentOutOfRangeException("selectionStart");
            }

            string dateTimeFormat = this.Format;
            if (selectionStart >= dateTimeFormat.Length)
            {
                selectionStart = dateTimeFormat.Length - 1;
            }
            char startChar = dateTimeFormat[selectionStart];

            // seek to next field
            // This finds the beginning of the next field for Direction.Next and the end of the previous field for Direction.Previous.
            int index = selectionStart + (int)direction;
            for (; index > 0; index += (int)direction)
            {
                if (index >= dateTimeFormat.Length)
                {
                    return -1;
                }
                if (dateTimeFormat[index] == startChar)
                {
                    continue;
                }
                if (Constant.Time.DateTimeFieldCharacters.Contains(dateTimeFormat[index]))
                {
                    break;
                }
                startChar = '\0'; // to handle cases like "yyyy-MM-dd (ddd)" correctly
            }

            if (direction == Direction.Previous)
            {
                // move to the beginning of the field
                while (index > 0 && dateTimeFormat[index - 1] == dateTimeFormat[index])
                {
                    --index;
                }
            }
            return index;
        }

        private string GetInputDateFormat()
        {
            if (this.inputDateFormat == null)
            {
                string dateTimeFormat = this.Format;
                if (!dateTimeFormat.Contains("MMM"))
                {
                    dateTimeFormat = dateTimeFormat.Replace("MM", "M");
                }
                if (!dateTimeFormat.Contains("ddd"))
                {
                    dateTimeFormat = dateTimeFormat.Replace("dd", "d");
                }
                // Note: do not replace Replace("tt", "t") because a single "t" will not accept "AM" or "PM".
                this.inputDateFormat = dateTimeFormat.Replace("hh", "h").Replace("HH", "H").Replace("mm", "m").Replace("ss", "s");
            }
            return this.inputDateFormat;
        }

        private Nullable<DateTimeOffset> ParseDateTime(bool flexible)
        {
            DateTimeOffset selectedDate;
            if (!DateTimeOffset.TryParseExact(this.DateTimeDisplay.Text, this.GetInputDateFormat(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out selectedDate))
            {
                if (!flexible || !DateTimeOffset.TryParse(this.DateTimeDisplay.Text, out selectedDate))
                {
                    return null;
                }
            }
            return selectedDate;
        }

        private static void OnDateTimeFormatChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)obj;
            dateTimeOffsetPicker.inputDateFormat = null; // will be recomputed on-demand
            dateTimeOffsetPicker.DateTimeDisplay.Text = dateTimeOffsetPicker.Value.ToString(dateTimeOffsetPicker.Format);
        }

        private void OnUpDown(int increment)
        {
            int selectionStart = this.DateTimeDisplay.SelectionStart;
            this.Value = this.ChangeDateTimePart(selectionStart, increment);
            this.FocusOnDateTimePart(selectionStart);
        }

        private static object SetDateTime(DependencyObject d, object value)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)d;
            DateTimeOffset dateTimeValue = (DateTimeOffset)value;
            if (dateTimeValue < dateTimeOffsetPicker.Minimum)
            {
                dateTimeValue = dateTimeOffsetPicker.Minimum;
            }
            if (dateTimeValue > dateTimeOffsetPicker.Maximum)
            {
                dateTimeValue = dateTimeOffsetPicker.Maximum;
            }
            return dateTimeValue;
        }

        private static object SetMaximumDateTime(DependencyObject d, object value)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)d;
            DateTimeOffset maximumDateTime = (DateTimeOffset)value;
            if (maximumDateTime < dateTimeOffsetPicker.Minimum)
            {
                throw new ArgumentException("MaximimumDateTime cannot be before MinimumDateTime.");
            }

            if (maximumDateTime < dateTimeOffsetPicker.Value)
            {
                dateTimeOffsetPicker.Value = maximumDateTime;
            }

            return maximumDateTime;
        }

        private static object SetMinimumDateTime(DependencyObject d, object value)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)d;
            DateTimeOffset minimumDateTime = (DateTimeOffset)value;
            if (minimumDateTime > dateTimeOffsetPicker.Maximum)
            {
                throw new ArgumentException("MinimumDateTime cannot be after the MaximumDateTime.");
            }

            if (minimumDateTime > dateTimeOffsetPicker.Value)
            {
                dateTimeOffsetPicker.Value = minimumDateTime;
            }

            return minimumDateTime;
        }

        private bool SelectDateTimePart(Direction direction)
        {
            int selectionStart = this.DateTimeDisplay.SelectionStart;
            selectionStart = this.GetIndexOfNextDateTimePart(selectionStart, direction);
            if (selectionStart > -1)
            {
                return this.FocusOnDateTimePart(selectionStart);
            }
            else
            {
                return false;
            }
        }
    }
}