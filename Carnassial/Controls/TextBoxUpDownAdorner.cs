using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Carnassial.Controls
{
    public class TextBoxUpDownAdorner : Adorner
    {
        private static readonly Pen Outline = new Pen(new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), 5);
        private static readonly Brush Fill = Brushes.Black;

        private bool textBoxHasSelection;
        private StreamGeometry triangle;
        private double x, top, bottom;

        public event Action<TextBox, int> Button_Clicked;

        public TextBoxUpDownAdorner(TextBox adornedTextBox) :
            base(adornedTextBox)
        {
            this.triangle = new StreamGeometry();
            this.triangle.FillRule = FillRule.Nonzero;
            using (StreamGeometryContext c = this.triangle.Open())
            {
                c.BeginFigure(new Point(-6, 0), true /* filled */, true /* closed */);
                c.LineTo(new Point(6, 0), true, false);
                c.LineTo(new Point(0, 8), true, false);
            }
            this.triangle.Freeze();

            this.MouseDown += (s, e) =>
            {
                if (this.Button_Clicked != null)
                {
                    bool up = e.GetPosition(this.AdornedElement).Y < (top + bottom) / 2;
                    this.Button_Clicked((TextBox)this.AdornedElement, up ? 1 : -1);
                }
            };

            adornedTextBox.LostFocus += this.FocusLostOrSelectionChanged;
            adornedTextBox.SelectionChanged += this.FocusLostOrSelectionChanged;
        }

        private void FocusLostOrSelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox box = (TextBox)this.AdornedElement;
            if (box.IsFocused)
            {
                // during OnRender() GetRectFromCharacterIndex() may return infinite values so check there's a selection first
                int selectionLength = box.SelectionLength;
                int start = box.SelectionStart;
                if (this.textBoxHasSelection = selectionLength > 0)
                {
                    Rect rect1 = box.GetRectFromCharacterIndex(start);
                    Rect rect2 = box.GetRectFromCharacterIndex(start + selectionLength);
                    this.top = rect1.Top - 2;
                    this.bottom = rect1.Bottom + 2;
                    this.x = (rect1.Left + rect2.Left) / 2;
                }
            }
            else
            {
                this.textBoxHasSelection = false;
            }

            this.InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (this.textBoxHasSelection)
            {
                // up button
                drawingContext.PushTransform(new TranslateTransform(this.x, this.top));
                drawingContext.PushTransform(new ScaleTransform(1, -1));
                drawingContext.DrawGeometry(TextBoxUpDownAdorner.Fill, TextBoxUpDownAdorner.Outline, this.triangle);
                drawingContext.Pop();
                drawingContext.Pop();

                // down button
                drawingContext.PushTransform(new TranslateTransform(this.x, this.bottom));
                drawingContext.DrawGeometry(TextBoxUpDownAdorner.Fill, TextBoxUpDownAdorner.Outline, this.triangle);
                drawingContext.Pop();
            }
        }
    }
}
