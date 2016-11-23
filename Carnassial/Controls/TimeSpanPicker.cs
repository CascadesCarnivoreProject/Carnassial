using Carnassial.Util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Carnassial.Controls
{
    public class TimeSpanPicker : TextBox
    {
        public static readonly RoutedEvent FormatChangedEvent = EventManager.RegisterRoutedEvent("FormatChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TimeSpanPicker));
        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register("Format", typeof(string), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(Constant.Time.TimeSpanDisplayFormat, TimeSpanPicker.OnTimeSpanFormatChanged));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(TimeSpan), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(TimeSpan.MaxValue, null, TimeSpanPicker.SetMaximumTimeSpan));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(TimeSpan), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(TimeSpan.MinValue, null, TimeSpanPicker.SetMinimumTimeSpan));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(TimeSpan), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(TimeSpan.Zero, TimeSpanPicker.TimeSpanChanged, TimeSpanPicker.SetTimeSpan));

        private TextBoxUpDownAdorner upDownButtons;

        public event Action<TimeSpanPicker, TimeSpan> ValueChanged;

        public TimeSpanPicker()
        {
            this.GotFocus += this.TimeSpanPicker_GotFocus;
            this.LostFocus += this.TimeSpanPicker_LostFocus;
            this.PreviewMouseUp += this.TimeSpanPicker_PreviewMouseUp;
            this.PreviewKeyDown += this.TimeSpanPicker_PreviewKeyDown;
            this.TextChanged += this.TimeSpanPicker_TextChanged;

            this.Loaded += (s, e) =>
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (adornerLayer != null)
                {
                    adornerLayer.Add(this.upDownButtons = new TextBoxUpDownAdorner(this));
                    this.upDownButtons.Button_Clicked += (textBox, direction) => { this.OnUpDown(direction); };
                }
            };

            this.VerticalContentAlignment = VerticalAlignment.Center;
            TimeSpanPicker.TimeSpanChanged(this, new DependencyPropertyChangedEventArgs(TimeSpanPicker.ValueProperty, TimeSpan.Zero, TimeSpan.Zero));
        }

        public string Format
        {
            get { return Convert.ToString(this.GetValue(TimeSpanPicker.FormatProperty)); }
            set { this.SetValue(TimeSpanPicker.FormatProperty, value); }
        }

        public TimeSpan Maximum
        {
            get { return (TimeSpan)this.GetValue(TimeSpanPicker.MaximumProperty); }
            set { this.SetValue(TimeSpanPicker.MaximumProperty, value); }
        }

        public TimeSpan Minimum
        {
            get { return (TimeSpan)this.GetValue(TimeSpanPicker.MinimumProperty); }
            set { this.SetValue(TimeSpanPicker.MinimumProperty, value); }
        }

        public TimeSpan Value
        {
            get { return (TimeSpan)this.GetValue(TimeSpanPicker.ValueProperty); }
            set { this.SetValue(TimeSpanPicker.ValueProperty, value); }
        }

        private TimeSpan ChangeTimeSpanPart(int selectionStart, int increment)
        {
            int formatIndex = this.ConvertSelectionToFormatIndex(selectionStart);
            string timeSpanFormat = this.Format;
            if (formatIndex < 0 || formatIndex > timeSpanFormat.Length - 1)
            {
                throw new ArgumentOutOfRangeException("selectionStart");
            }

            TimeSpan timeSpan = this.ParseTimeSpan() ?? this.Value;
            timeSpan += this.ConvertIncrementToTimeSpan(timeSpanFormat.Substring(formatIndex, 1), increment);
            if (timeSpan < this.Minimum)
            {
                return this.Minimum;
            }
            if (timeSpan > this.Maximum)
            {
                return this.Maximum;
            }
            return timeSpan;
        }

        protected virtual TimeSpan ConvertIncrementToTimeSpan(string partFormat, int increment)
        {
            switch (partFormat)
            {
                case "d":
                    return TimeSpan.FromDays(increment);
                case "f":
                case "F":
                    return TimeSpan.FromMilliseconds(increment);
                case "h":
                    return TimeSpan.FromHours(increment);
                case "m":
                    return TimeSpan.FromMinutes(increment);
                case "s":
                    return TimeSpan.FromSeconds(increment);
                default:
                    throw new NotSupportedException(String.Format("Unhandled part format {0}.", partFormat));
            }
        }

        private int ConvertSelectionToFormatIndex(int selectionStart)
        {
            // any leading minus sign is not included in the format
            if (this.Text != null && this.Text.StartsWith("-", StringComparison.OrdinalIgnoreCase))
            {
                --selectionStart;
            }

            string timeSpanFormat = this.Format;
            for (int formatIndex = 0; formatIndex < selectionStart; ++formatIndex)
            {
                if (timeSpanFormat[formatIndex] == '\\')
                {
                    ++selectionStart;
                }
            }
            return selectionStart;
        }

        private bool FocusOnTimeSpanPart(int formatIndex)
        {
            Nullable<TimeSpan> timeSpan = this.ParseTimeSpan();
            if (timeSpan.HasValue)
            {
                string newText = TimeSpanPicker.TimeSpanToString(timeSpan.Value, this.Format);
                if (this.Text != newText)
                {
                    this.Text = newText;
                }
            }

            // find beginning of field to select
            string timeSpanFormat = this.Format;
            if (formatIndex > timeSpanFormat.Length - 1)
            {
                formatIndex = timeSpanFormat.Length - 1;
            }

            char firstCharacter = timeSpanFormat[formatIndex];
            while (!char.IsLetter(firstCharacter) && formatIndex + 1 < timeSpanFormat.Length)
            {
                ++formatIndex;
                firstCharacter = timeSpanFormat[formatIndex];
            }
            while (formatIndex > 0 && timeSpanFormat[formatIndex - 1] == firstCharacter)
            {
                --formatIndex;
            }

            int selectionLength = 1;
            while (formatIndex + selectionLength < timeSpanFormat.Length && timeSpanFormat[formatIndex + selectionLength] == firstCharacter)
            {
                ++selectionLength;
            }

            // backslashes in the TimeSpan display format aren't displayed; reposition selection accordingly
            int backslashCount = 0;
            for (int textIndex = 0; textIndex < formatIndex; ++textIndex)
            {
                if (timeSpanFormat[textIndex] == '\\')
                {
                    ++backslashCount;
                }
            }

            this.Focus();
            int selectionStart = formatIndex - backslashCount;
            if (this.Text != null && this.Text.StartsWith("-", StringComparison.OrdinalIgnoreCase))
            {
                ++selectionStart;
            }
            this.Select(selectionStart, selectionLength);
            return true;
        }

        // get location of next or previous time span field
        // returns -1 if there is no next/previous field
        private int GetIndexOfTimeSpanPart(int selectionStart, Direction direction)
        {
            int formatIndex = this.ConvertSelectionToFormatIndex(selectionStart);
            string timeSpanFormat = this.Format;
            if (formatIndex < 0 || formatIndex > timeSpanFormat.Length - 1)
            {
                throw new ArgumentOutOfRangeException("selectionStart");
            }

            if (formatIndex >= timeSpanFormat.Length)
            {
                formatIndex = timeSpanFormat.Length - 1;
            }
            char startChar = timeSpanFormat[formatIndex];

            // seek to next field
            // This finds the beginning of the next field for Direction.Next and the end of the previous field for Direction.Previous.
            formatIndex += (int)direction;
            for (; formatIndex > 0; formatIndex += (int)direction)
            {
                if (formatIndex >= timeSpanFormat.Length)
                {
                    return -1;
                }
                if (timeSpanFormat[formatIndex] == startChar)
                {
                    continue;
                }
                if (Constant.Time.TimeSpanFieldCharacters.Contains(timeSpanFormat[formatIndex]))
                {
                    break;
                }
                startChar = '\0'; // to handle cases like "yyyy-MM-dd (ddd)" correctly
            }

            if (direction == Direction.Previous)
            {
                // move to the beginning of the field
                while (formatIndex > 0 && timeSpanFormat[formatIndex - 1] == timeSpanFormat[formatIndex])
                {
                    --formatIndex;
                }
            }
            return formatIndex;
        }

        private static void OnTimeSpanFormatChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)obj;
            timeSpanPicker.Text = timeSpanPicker.Value.ToString(timeSpanPicker.Format);
        }

        private void OnUpDown(int increment)
        {
            int selectionStart = this.SelectionStart;
            this.Value = this.ChangeTimeSpanPart(selectionStart, increment);
            this.FocusOnTimeSpanPart(selectionStart);
        }

        private TimeSpan? ParseTimeSpan()
        {
            TimeSpan timeSpan;
            if (TimeSpan.TryParseExact(this.Text, this.Format, CultureInfo.InvariantCulture, TimeSpanStyles.None, out timeSpan))
            {
                return timeSpan;
            }
            else if (TimeSpan.TryParseExact(this.Text, @"\-" + this.Format, CultureInfo.InvariantCulture, TimeSpanStyles.AssumeNegative, out timeSpan))
            {
                return timeSpan;
            }
            return null;
        }

        private static object SetMaximumTimeSpan(DependencyObject d, object value)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)d;
            TimeSpan maximumTimeSpan = (TimeSpan)value;
            if (maximumTimeSpan < timeSpanPicker.Minimum)
            {
                throw new ArgumentException("MaximimumTimeSpan cannot be before MinimumTimeSpan.");
            }

            if (maximumTimeSpan < timeSpanPicker.Value)
            {
                timeSpanPicker.Value = maximumTimeSpan;
            }

            return maximumTimeSpan;
        }

        private static object SetMinimumTimeSpan(DependencyObject d, object value)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)d;
            TimeSpan minimumTimeSpan = (TimeSpan)value;
            if (minimumTimeSpan > timeSpanPicker.Maximum)
            {
                throw new ArgumentException("MinimumTimeSpan cannot be greater than MaximumTimeSpan.");
            }

            if (minimumTimeSpan > timeSpanPicker.Value)
            {
                timeSpanPicker.Value = minimumTimeSpan;
            }

            return minimumTimeSpan;
        }

        private bool SelectTimeSpanPart(Direction direction)
        {
            int selectionStart = this.SelectionStart;
            selectionStart = this.GetIndexOfTimeSpanPart(selectionStart, direction);
            if (selectionStart > -1)
            {
                return this.FocusOnTimeSpanPart(selectionStart);
            }
            else
            {
                return false;
            }
        }

        private static object SetTimeSpan(DependencyObject d, object value)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)d;
            TimeSpan timeSpan = (TimeSpan)value;
            if (timeSpan < timeSpanPicker.Minimum)
            {
                timeSpan = timeSpanPicker.Minimum;
            }
            if (timeSpan > timeSpanPicker.Maximum)
            {
                timeSpan = timeSpanPicker.Maximum;
            }
            return timeSpan;
        }

        private static void TimeSpanChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)obj;
            TimeSpan timeSpan = (TimeSpan)args.NewValue;
            timeSpanPicker.Text = TimeSpanPicker.TimeSpanToString(timeSpan, timeSpanPicker.Format);

            if (timeSpanPicker.ValueChanged != null)
            {
                timeSpanPicker.ValueChanged.Invoke(timeSpanPicker, timeSpanPicker.Value);
            }
        }

        private void TimeSpanPicker_GotFocus(object sender, RoutedEventArgs e)
        {
            this.FocusOnTimeSpanPart(this.SelectionStart);
            e.Handled = true;
        }

        private void TimeSpanPicker_LostFocus(object sender, RoutedEventArgs e)
        {
            this.Text = TimeSpanPicker.TimeSpanToString(this.Value, this.Format);

            // if a field loses focus and the user then clicks the same field again, the selection is cleared causing the adnorner not to appear
            // To fix, clear selection in advance.
            this.SelectionLength = 0;
        }

        private void TimeSpanPicker_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (this.ParseTimeSpan() == null)
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
                    this.SelectTimeSpanPart(Direction.Previous);
                    break;
                case Key.Right:
                    if (Keyboard.Modifiers != ModifierKeys.None)
                    {
                        return;
                    }
                    this.SelectTimeSpanPart(Direction.Next);
                    break;
                default:
                    char nextChar = '\0';
                    if (this.SelectionStart < this.Text.Length)
                    {
                        nextChar = this.Text[this.SelectionStart];
                    }

                    if ((e.Key == Key.OemMinus || e.Key == Key.Subtract || e.Key == Key.OemQuestion || e.Key == Key.Divide) &&
                        ((nextChar == '/' || nextChar == '-') || (e.Key == Key.Space && nextChar == ' ') || (e.Key == Key.OemSemicolon && nextChar == ':')))
                    {
                        this.SelectTimeSpanPart(Direction.Next);
                    }
                    else
                    {
                        return;
                    }
                    break;
            }
            e.Handled = true;
        }

        private void TimeSpanPicker_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.SelectionLength == 0)
            {
                this.FocusOnTimeSpanPart(this.SelectionStart);
            }
        }

        private void TimeSpanPicker_TextChanged(object sender, TextChangedEventArgs e)
        {
            Nullable<TimeSpan> timeSpan = this.ParseTimeSpan();
            if (timeSpan.HasValue)
            {
                this.Value = timeSpan.Value;
            }
        }

        private static string TimeSpanToString(TimeSpan value, string format)
        {
            return (value < TimeSpan.Zero ? "-" : String.Empty) + value.ToString(format);
        }
    }
}
