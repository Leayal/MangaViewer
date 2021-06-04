using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using VisualTreeHelpers = VisualHelper.VisualTreeHelper;

namespace MangaViewer_Zip
{
    public class MangaView : ListView
    {
        // private ScrollBar scrollBar;
        private ScrollViewer scrollViewer;
        private DockPanel contentHolder;

        private Leayal.WPF.SmoothScroller smoothScroller;

        private static readonly DependencyPropertyKey ActualWidthWithoutVerticalScrollBarPropertyKey = DependencyProperty.RegisterReadOnly("ActualWidthWithoutVerticalScrollBar", typeof(double), typeof(MangaView), new PropertyMetadata(0d));
        public static readonly DependencyProperty ActualWidthWithoutVerticalScrollBarProperty = ActualWidthWithoutVerticalScrollBarPropertyKey.DependencyProperty;
        private static readonly DependencyPropertyKey MaximumVerticalScrollOffsetKey = DependencyProperty.RegisterReadOnly("MaximumVerticalScrollOffset", typeof(double), typeof(MangaView), new PropertyMetadata(0d, null, new CoerceValueCallback((obj, val) =>
        {
            if (obj is MangaView viewer)
            {
                return viewer.scrollViewer.ScrollableHeight;
            }
            return val;
        })));
        public static readonly DependencyProperty MaximumVerticalScrollOffsetKeyProperty = MaximumVerticalScrollOffsetKey.DependencyProperty;

        public double ActualWidthWithoutVerticalScrollBar => (double)this.GetValue(ActualWidthWithoutVerticalScrollBarProperty);

        public MangaView() : base()
        {
            // this.scrollBar = null;
            // this.scrollViewer = null;
            this.Style = new Style(typeof(ListView), (Style)this.FindResource(typeof(ListView)));
        }

        public double MaximumVerticalScrollOffset => this.scrollViewer.ScrollableHeight;

        public double VerticalScrollOffset
        {
            /*
            get => this.scrollViewer.VerticalOffset;
            set
            {
                if (this.scrollViewer.VerticalOffset != value)
                {
                    if (value < 0)
                    {
                        this.scrollViewer.ScrollToVerticalOffset(0);
                    }
                    else if (value > this.scrollViewer.ScrollableHeight)
                    {
                        this.scrollViewer.ScrollToVerticalOffset(this.scrollViewer.ScrollableHeight);
                    }
                    else
                    {
                        this.scrollViewer.ScrollToVerticalOffset(value);
                    }
                }
            }
            //*/
            get => this.smoothScroller.ScrollOffset;
            set
            {
                if (this.smoothScroller.ScrollOffset != value)
                {
                    if (value < 0)
                    {
                        this.smoothScroller.ScrollOffset = 0;
                    }
                    else if (value > this.scrollViewer.ScrollableHeight)
                    {
                        this.smoothScroller.ScrollOffset = this.scrollViewer.ScrollableHeight;
                    }
                    else
                    {
                        this.smoothScroller.ScrollOffset = value;
                    }
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.scrollViewer = VisualTreeHelpers.FindChild<ScrollViewer>(this);
            this.scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            this.scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            this.smoothScroller = new Leayal.WPF.SmoothScroller(this.scrollViewer);

            //this.scrollViewer.ApplyTemplate();
            //var grid = VisualTreeHelpers.FindChild<Grid>(this.scrollViewer);
            //grid.ApplyTemplate();
            //var scrollBar = VisualTreeHelpers.FindChild<ScrollBar>(grid, "PART_VerticalScrollBar");
            //if (scrollBar != null)
            //{
            //    smoothScroller = new Leayal.WPF.SmoothScroller(this.scrollViewer);
            //}
            //this.contentHolder = VisualTreeHelpers.FindChild<DockPanel>(grid);
            //this.ComputeActualWidthWithoutVerticalScrollBar();
            //this.contentHolder.SizeChanged += MangaViewContent_SizeChanged;

            if (this.scrollViewer.IsLoaded)
            {
                var grid = VisualTreeHelpers.FindChild<Grid>(this.scrollViewer);
                this.contentHolder = VisualTreeHelpers.FindChild<DockPanel>(grid);
                this.ComputeActualWidthWithoutVerticalScrollBar();
                this.contentHolder.SizeChanged += MangaViewContent_SizeChanged;
            }
            else
            {
                this.scrollViewer.Loaded += this.ScrollViewer_Loaded;
            }
        }

        private void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer viewer)
            {
                var grid = VisualTreeHelpers.FindChild<Grid>(viewer);
                this.contentHolder = VisualTreeHelpers.FindChild<DockPanel>(grid);
                this.ComputeActualWidthWithoutVerticalScrollBar();
                this.contentHolder.SizeChanged += MangaViewContent_SizeChanged;
            }
        }

        private void MangaViewContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                this.ComputeActualWidthWithoutVerticalScrollBar();
            }
        }

        private void ComputeActualWidthWithoutVerticalScrollBar()
        {
            this.SetValue(ActualWidthWithoutVerticalScrollBarPropertyKey, this.contentHolder.ActualWidth);
        }
    }
}
