using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MangaViewer_Zip
{
    public class MangaPage : Image
    {
        public static readonly DependencyProperty MinWidthPercentageProperty = DependencyProperty.Register("MinWidthPercentage", typeof(double), typeof(MangaPage), new FrameworkPropertyMetadata(0.75d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty MinWidthPercentageTargetProperty = DependencyProperty.Register("MinWidthPercentageTarget", typeof(FrameworkElement), typeof(MangaPage), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, (obj, e) =>
        {
            if (obj is MangaPage page)
            {
                if (page.IsLoaded)
                {
                    if (e.OldValue is FrameworkElement oldOne)
                    {
                        oldOne.SizeChanged -= page.Parent_SizeChanged;
                    }
                    if (e.NewValue is FrameworkElement newOne)
                    {
                        newOne.SizeChanged += page.Parent_SizeChanged;
                    }
                    // page.InvalidateMeasure();
                }
            }
        }));

        public MangaPage() : base()
        {
            this.Stretch = Stretch.Uniform;
            this.StretchDirection = StretchDirection.DownOnly;
            // this.VisualBitmapScalingMode = BitmapScalingMode.Fant;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
            this.Loaded += MangaPage_Loaded;
            this.Unloaded += MangaPage_Unloaded;
        }

        public MangaPage(FrameworkElement parent) : this()
        {
            this.MinWidthPercentageTarget = parent;
        }

        public FrameworkElement MinWidthPercentageTarget
        {
            get => (FrameworkElement)this.GetValue(MinWidthPercentageTargetProperty);
            set => this.SetValue(MinWidthPercentageTargetProperty, value);
        }

        public MangaPage(FrameworkElement parent, ImageSource bitmap) : this(parent)
        {
            this.Source = bitmap;
        }

        private static void MangaPage_Unloaded(object sender, RoutedEventArgs e)
        {
            var mangaPage = (MangaPage)sender;
            var target = mangaPage.MinWidthPercentageTarget;
            if (target != null)
            {
                target.SizeChanged -= mangaPage.Parent_SizeChanged;
            }
        }

        private static void MangaPage_Loaded(object sender, RoutedEventArgs e)
        {
            var mangaPage = (MangaPage)sender;
            var target = mangaPage.MinWidthPercentageTarget;
            if (target != null)
            {
                target.SizeChanged += mangaPage.Parent_SizeChanged;
            }
        }

        private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                this.InvalidateMeasure();
            }
        }

        public double MinWidthPercentage
        {
            get => (double)this.GetValue(MinWidthPercentageProperty);
            set => this.SetValue(MinWidthPercentageProperty, value);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var percentage = this.MinWidthPercentage;
            var target = this.MinWidthPercentageTarget;
            if (percentage != 0d && this.MinWidth == 0 && target != null && target.ActualWidth != 0 && this.Source != null && target.ActualWidth > this.Source.Width)
            {
                var desiredWidth = target.ActualWidth * percentage;
                if (this.Source.Width < desiredWidth)
                {
                    var size = base.ArrangeOverride(arrangeSize);
                    if (size.Width < desiredWidth)
                    {
                        return new Size(desiredWidth, size.Height * (desiredWidth / size.Width));
                    }
                }
            }
            return base.ArrangeOverride(arrangeSize);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var percentage = this.MinWidthPercentage;
            var target = this.MinWidthPercentageTarget;
            if (percentage != 0d && this.MinWidth == 0 && target != null && target.ActualWidth != 0 && this.Source != null && target.ActualWidth > this.Source.Width)
            {
                var desiredWidth = target.ActualWidth * percentage;
                if (this.Source.Width < desiredWidth)
                {
                    var size = base.MeasureOverride(constraint);
                    if (size.Width < desiredWidth)
                    {
                        return new Size(desiredWidth, size.Height * (desiredWidth / size.Width));
                    }
                }
            }
            return base.MeasureOverride(constraint);
        }
    }
}
