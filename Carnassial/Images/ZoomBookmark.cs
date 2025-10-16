﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Carnassial.Images
{
    internal class ZoomBookmark
    {
        public Point Scale { get; private set; }
        public Point Translation { get; private set; }

        public ZoomBookmark()
        {
            this.Reset();
        }

        public void Apply(ScaleTransform scale, TranslateTransform translation)
        {
            scale.ScaleX = this.Scale.X;
            scale.ScaleY = this.Scale.Y;
            translation.X = this.Translation.X;
            translation.Y = this.Translation.Y;
        }

        public void Reset()
        {
            this.Scale = new Point(1.0, 1.0);
            this.Translation = new Point(0.0, 0.0);
        }

        public void Set(ScaleTransform scale, TranslateTransform translation)
        {
            // bookmarks use absolute positions and are therefore specific to a particular display size
            // A corollary of this is the scale transform's center need not be persisted as the bookmark's reset when the display size changes.
            this.Scale = new Point(scale.ScaleX, scale.ScaleY);
            this.Translation = new Point(translation.X, translation.Y);
        }

        public override string ToString()
        {
            return String.Create(CultureInfo.InvariantCulture, $"{this.Scale.X},{this.Scale.Y},{this.Translation.X},{this.Translation.Y}");
        }
    }
}
