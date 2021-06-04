using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Interop;
using System.Collections.Concurrent;
using Linearstar.Windows.RawInput;
using System.Runtime.InteropServices;
using WinForm = System.Windows.Forms;

namespace MangaViewer_Zip
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private const int WM_INPUT = 0x00FF;
        private static readonly double ScrollStepFromKeyboard = (double)System.Windows.Forms.SystemInformation.MouseWheelScrollDelta;

        private readonly OpenFileDialog ofd;
        private ArchivedImages? archive;

        private readonly static DependencyPropertyKey IsInLoadingPropertyKey = DependencyProperty.RegisterReadOnly("IsInLoading", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public readonly static DependencyProperty IsInLoadingProperty = IsInLoadingPropertyKey.DependencyProperty;
        private bool _rawInputKeyboardRegistered, _rawInputMouseRegistered;
        private HwndSource? thisWindowHandleSrc;

        public bool IsInLoading
        {
            get => (bool)this.GetValue(IsInLoadingProperty);
            set => this.SetValue(IsInLoadingPropertyKey, value);
        }

        public MainWindow()
        {
            InitializeComponent();
            this.IsInLoading = false;
            this.ofd = new OpenFileDialog();

            // this.ImageList.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var thisWindowHandle = (new WindowInteropHelper(this)).Handle;
            try
            {
                RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.None, thisWindowHandle);
                this._rawInputKeyboardRegistered = true;
            }
            catch
            {
                this._rawInputKeyboardRegistered = false;
            }
            try
            {
                RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, thisWindowHandle);
                this._rawInputMouseRegistered = true;
            }
            catch
            {
                this._rawInputMouseRegistered = false;
            }
            if (this._rawInputKeyboardRegistered || this._rawInputMouseRegistered)
            {
                this.thisWindowHandleSrc = HwndSource.FromHwnd(thisWindowHandle);
                this.thisWindowHandleSrc.AddHook(new HwndSourceHook(WndProc));
            }
            else
            {
                this.thisWindowHandleSrc = null;
            }
        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            using (var reg = Registry.CurrentUser.OpenSubKey(Path.Combine("SOFTWARE", "leayal", "ArchivedImageViewer"), false))
            {
                if (reg != null)
                {
                    HashSet<string> keys = new HashSet<string>(reg.GetValueNames(), StringComparer.OrdinalIgnoreCase);
                    int x, y, width, height;
                    if (keys.Contains("X") && reg.GetValueKind("X") == RegistryValueKind.DWord)
                    {
                        x = (int)reg.GetValue("X", 0);
                    }
                    else
                    {
                        x = 0;
                    }
                    if (keys.Contains("Y") && reg.GetValueKind("Y") == RegistryValueKind.DWord)
                    {
                        y = (int)reg.GetValue("Y", 0);
                    }
                    else
                    {
                        y = 0;
                    }
                    if (keys.Contains("Width") && reg.GetValueKind("Width") == RegistryValueKind.DWord)
                    {
                        width = (int)reg.GetValue("Width", 800);
                    }
                    else
                    {
                        width = 800;
                    }
                    if (keys.Contains("Height") && reg.GetValueKind("Height") == RegistryValueKind.DWord)
                    {
                        height = (int)reg.GetValue("Height", 600);
                    }
                    else
                    {
                        height = 600;
                    }
                    var windowrect = new Rect(x, y, width, height);
                    var desktop = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
                    if (desktop.Contains(windowrect))
                    {
                        this.WindowState = WindowState.Normal;
                        this.Left = windowrect.Left;
                        this.Top = windowrect.Top;
                        this.Width = windowrect.Width;
                        this.Height = windowrect.Height;
                    }
                    bool maximized;
                    if (keys.Contains("Maximized") && reg.GetValueKind("Maximized") == RegistryValueKind.DWord)
                    {
                        maximized = (((int)reg.GetValue("Maximized", 0)) != 0);
                    }
                    else
                    {
                        maximized = false;
                    }
                    if (maximized)
                    {
                        this.WindowState = WindowState.Maximized;
                    }
                }
            }
            var args = App.Item.Args;
            if (args.Count != 0)
            {
                var file = args[0];
                if (!string.IsNullOrWhiteSpace(file))
                {
                    if (File.Exists(file))
                    {
                        await this.LoadArchive(file);
                    }
                }
            }
        }

        private async void ButtonOpenFile_Click(object sender, RoutedEventArgs e)
        {
            this.ofd.Reset();

            this.ofd.Title = "Open ZIP archive that contains image(s)";
            this.ofd.CheckFileExists = true;
            this.ofd.CheckPathExists = true;
            this.ofd.Filter = "Supported Files|*.zip;*.7z;*.rar;*.tar;*.bz2|Any Files|*";
            this.ofd.ShowReadOnly = false;
            this.ofd.Multiselect = false;
            // this.ofd.InitialDirectory = "";

            if (this.ofd.ShowDialog(this) == true)
            {
                try
                {
                    await this.LoadArchive(this.ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ButtonOpenFileDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (this.archive != null)
            {
                using (var proc = new Process())
                {
                    proc.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                    proc.StartInfo.Arguments = $"/select,\"{this.archive.Filename}\"";
                    proc.StartInfo.UseShellExecute = true;
                    proc.Start();
                    proc.WaitForExit(30);
                }
            }
        }

        private void ButtonOptions_Click(object sender, RoutedEventArgs e)
        {

        }

        class VirtualScreenWidthInfo
        {
            public readonly double VirtualScreenWidth;
            public readonly int RoundedVirtualScreenWidth;

            public VirtualScreenWidthInfo()
            {
                this.VirtualScreenWidth = SystemParameters.VirtualScreenWidth;
                this.RoundedVirtualScreenWidth = Convert.ToInt32(this.VirtualScreenWidth);
            }
        }

        private async Task LoadArchive(string filename)
        {
            if (this.IsInLoading) return;

            this.IsInLoading = true;

            var concurrentCount = Math.Min(Environment.ProcessorCount, 4);

            ArchivedImages newarchive = null;
            try
            {
                if (this.archive != null)
                {
                    await this.UnloadArchive();
                }
                newarchive = new ArchivedImages(filename, concurrentCount);
                newarchive.Read();
                this.Title = Path.GetFileNameWithoutExtension(filename);
            }
            catch
            {
                if (newarchive != null)
                {
                    await newarchive.DisposeAsync();
                }
                throw;
            }
            finally
            {
                this.IsInLoading = false;
            }

            this.archive = newarchive;

            try
            {
                var imglist = this.ImageList;
                imglist.ItemsSource = null;
                // imglist.Items.SortDescriptions.Add(new SortDescription("EntryName", ListSortDirection.Ascending));
                // this.ImageList.Items.IsLiveSorting = true;
                var imgs = new ObservableCollection<MangaPageBitmap>();
                var view = new ListCollectionView(imgs) { CustomSort = NativeMethods.NaturalComparer };
                imglist.ItemsSource = view;
                Task[] tasks = new Task[concurrentCount];
                var _dispatchBitmapImage = new DispatchBitmapImage((name, bitmap) =>
                {
                    imgs.Add(new MangaPageBitmap() { Name = name, Bitmap = bitmap });
                });

                var virtualScreenWidthInfo = new VirtualScreenWidthInfo();

                for (int i = 0; i < concurrentCount; i++)
                {
                    tasks[i] = Task.Factory.StartNew(async () =>
                    {
                        foreach (var stuff in newarchive.GetConsumer())
                        {
                            var filename = stuff.Key;
                            using (var contentStream = stuff.Value)
                            {
                                // contentStream.Position = 0;
                                bool isImg = false;

                                int imgwidth = 0;
                                BitmapFrame frame;

                                try
                                {
                                    frame = BitmapFrame.Create(contentStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                                    imgwidth = frame.PixelWidth;
                                    frame.Freeze();
                                    isImg = true;
                                }
                                catch (NotSupportedException) { isImg = false; }
                                catch (FileFormatException)
                                {
                                    imgwidth = 0;
                                    isImg = false;
                                }
                                finally
                                {
                                    frame = null;
                                }

                                if (isImg)
                                {
                                    BitmapSource img;
                                    try
                                    {
                                        contentStream.Position = 0;
                                        var bm = new BitmapImage();
                                        bm.BeginInit();
                                        if (virtualScreenWidthInfo.VirtualScreenWidth < imgwidth) bm.DecodePixelWidth = virtualScreenWidthInfo.RoundedVirtualScreenWidth;
                                        bm.StreamSource = contentStream;
                                        bm.CacheOption = BitmapCacheOption.OnLoad;
                                        bm.EndInit();
                                        bm.Freeze();
                                        img = bm;
                                        isImg = true;
                                    }
                                    catch (NotSupportedException) { img = null; isImg = false; }
                                    catch (FileFormatException)
                                    {
                                        try
                                        {
                                            contentStream.Position = 0;
                                            using (var gdi_bm = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromStream(contentStream))
                                            {
                                                WriteableBitmap writableBm = null;
                                                System.Drawing.Imaging.BitmapData locked;
                                                switch (gdi_bm.PixelFormat)
                                                {
                                                    case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                                                        locked = gdi_bm.LockBits(new System.Drawing.Rectangle(0, 0, gdi_bm.Width, gdi_bm.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                                                        writableBm = new WriteableBitmap(gdi_bm.Width, gdi_bm.Height, 96, 96, PixelFormats.Pbgra32, null);
                                                        writableBm.Lock();
                                                        try
                                                        {
                                                            writableBm.WritePixels(new Int32Rect(0, 0, gdi_bm.Width, gdi_bm.Height), locked.Scan0, locked.Stride * locked.Height, locked.Stride);
                                                        }
                                                        finally
                                                        {
                                                            gdi_bm.UnlockBits(locked);
                                                            writableBm.Unlock();
                                                            writableBm.Freeze();
                                                        }
                                                        break;
                                                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                                                        locked = gdi_bm.LockBits(new System.Drawing.Rectangle(0, 0, gdi_bm.Width, gdi_bm.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                                        writableBm = new WriteableBitmap(gdi_bm.Width, gdi_bm.Height, 96, 96, PixelFormats.Bgra32, null);
                                                        writableBm.Lock();
                                                        try
                                                        {
                                                            writableBm.WritePixels(new Int32Rect(0, 0, gdi_bm.Width, gdi_bm.Height), locked.Scan0, locked.Stride * locked.Height, locked.Stride);
                                                        }
                                                        finally
                                                        {
                                                            gdi_bm.UnlockBits(locked);
                                                            writableBm.Unlock();
                                                            writableBm.Freeze();
                                                        }
                                                        break;
                                                    case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                                                        locked = gdi_bm.LockBits(new System.Drawing.Rectangle(0, 0, gdi_bm.Width, gdi_bm.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                                                        writableBm = new WriteableBitmap(gdi_bm.Width, gdi_bm.Height, 96, 96, PixelFormats.Bgr32, null);
                                                        writableBm.Lock();
                                                        try
                                                        {
                                                            writableBm.WritePixels(new Int32Rect(0, 0, gdi_bm.Width, gdi_bm.Height), locked.Scan0, locked.Stride * locked.Height, locked.Stride);
                                                        }
                                                        finally
                                                        {
                                                            gdi_bm.UnlockBits(locked);
                                                            writableBm.Unlock();
                                                            writableBm.Freeze();
                                                        }
                                                        break;
                                                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                                                        locked = gdi_bm.LockBits(new System.Drawing.Rectangle(0, 0, gdi_bm.Width, gdi_bm.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                                                        writableBm = new WriteableBitmap(gdi_bm.Width, gdi_bm.Height, 96, 96, PixelFormats.Bgr24, null);
                                                        writableBm.Lock();
                                                        try
                                                        {
                                                            writableBm.WritePixels(new Int32Rect(0, 0, gdi_bm.Width, gdi_bm.Height), locked.Scan0, locked.Stride * locked.Height, locked.Stride);
                                                        }
                                                        finally
                                                        {
                                                            gdi_bm.UnlockBits(locked);
                                                            writableBm.Unlock();
                                                            writableBm.Freeze();
                                                        }
                                                        break;
                                                    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                                                        locked = gdi_bm.LockBits(new System.Drawing.Rectangle(0, 0, gdi_bm.Width, gdi_bm.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
                                                        BitmapPalette pal = null;
                                                        if (gdi_bm.Palette != null)
                                                        {
                                                            int totalCount = gdi_bm.Palette.Entries.Length;
                                                            var colorList = new List<Color>(totalCount);
                                                            for (int i = 0; i < totalCount; i++)
                                                            {
                                                                var a = gdi_bm.Palette.Entries[i];
                                                                colorList.Add(new Color() { A = a.A, B = a.B, R = a.R, G = a.G });
                                                            }
                                                            pal = new BitmapPalette(colorList);
                                                        }
                                                        writableBm = new WriteableBitmap(gdi_bm.Width, gdi_bm.Height, 96, 96, PixelFormats.Indexed8, pal);
                                                        writableBm.Lock();
                                                        try
                                                        {
                                                            writableBm.WritePixels(new Int32Rect(0, 0, gdi_bm.Width, gdi_bm.Height), locked.Scan0, locked.Stride * locked.Height, locked.Stride);
                                                        }
                                                        finally
                                                        {
                                                            gdi_bm.UnlockBits(locked);
                                                            writableBm.Unlock();
                                                            writableBm.Freeze();
                                                        }
                                                        break;
                                                    default:
                                                        isImg = false;
                                                        break;
                                                }
                                                img = writableBm;
                                            }
                                        }
                                        catch
                                        {
                                            img = null;
                                            isImg = false;
                                        }
                                    }

                                    if (isImg)
                                    {
                                       await this.Dispatcher.BeginInvoke(_dispatchBitmapImage, System.Windows.Threading.DispatcherPriority.Normal, new object[] { filename, img });
                                    }
                                }
                            }
                        }
                    }, TaskCreationOptions.LongRunning).Unwrap();
                }
                await Task.WhenAll(tasks);
            }
            catch
            {
                await this.UnloadArchive();
                throw;
            }
        }

        private delegate void DispatchBitmapImage(string name, BitmapSource bm);

        public class MangaPageBitmap : IComparable, IComparable<MangaPageBitmap>
        {
            public string Name { get; init; }
            public ImageSource Bitmap { get; init; }

            public int CompareTo(object obj)
            {
                if (obj is MangaPageBitmap mpb)
                {
                    return this.CompareTo(mpb);
                }
                return 0;
            }

            public int CompareTo(MangaPageBitmap other)
            {
                return NativeMethods.SafeNativeMethods.StrCmpLogicalW(this.Name, other.Name);
            }
        }

        private async Task UnloadArchive()
        {
            this.Title = "Archived Image Viewer";
            this.ImageList.ItemsSource = null;
            if (this.archive != null)
            {
                await this.archive.DisposeAsync();
                this.archive = null;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;

            if (msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(lParam);
                var imgList = this.ImageList;

                if (data is RawInputKeyboardData keyboard && !keyboard.Keyboard.Flags.HasFlag(Linearstar.Windows.RawInput.Native.RawKeyboardFlags.Up))
                {
                    double scrollVal;
                    switch (KeyInterop.KeyFromVirtualKey(keyboard.Keyboard.VirutalKey))
                    {
                        case Key.Home:
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal != 0)
                            {
                                imgList.VerticalScrollOffset = 0;
                            }
                            break;
                        case Key.End:
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal != imgList.MaximumVerticalScrollOffset)
                            {
                                imgList.VerticalScrollOffset = imgList.MaximumVerticalScrollOffset;
                            }
                            break;
                        case Key.Up:
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal > 0)
                            {
                                imgList.VerticalScrollOffset = scrollVal - ScrollStepFromKeyboard;
                            }
                            break;
                        case Key.Down:
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal < imgList.MaximumVerticalScrollOffset)
                            {
                                imgList.VerticalScrollOffset = scrollVal + ScrollStepFromKeyboard;
                            }
                            break;
                        case Key.PageDown:
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal < imgList.MaximumVerticalScrollOffset)
                            {
                                imgList.VerticalScrollOffset = scrollVal + imgList.ActualHeight;
                            }
                            break;
                        case Key.PageUp:
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal < imgList.MaximumVerticalScrollOffset)
                            {
                                imgList.VerticalScrollOffset = scrollVal - imgList.ActualHeight;
                            }
                            break;
                    }
                }
                else if (data is RawInputMouseData mouse)
                {
                    double scrollVal;
                    if (mouse.Mouse.Buttons == Linearstar.Windows.RawInput.Native.RawMouseButtonFlags.MouseWheel)
                    {
                        var delta = mouse.Mouse.ButtonData;
                        if (delta > 0)
                        {
                            // Scroll up

                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal > 0)
                            {
                                imgList.VerticalScrollOffset = scrollVal - delta;
                            }
                        }
                        else if (delta < 0)
                        {
                            handled = true;
                            scrollVal = imgList.VerticalScrollOffset;
                            if (scrollVal < imgList.MaximumVerticalScrollOffset)
                            {
                                imgList.VerticalScrollOffset = scrollVal - delta;
                            }
                        }
                    }
                }
            }


            return IntPtr.Zero;
        }

        private async void MetroWindow_Closed(object sender, EventArgs e)
        {
            if (this._rawInputKeyboardRegistered)
            {
                this._rawInputKeyboardRegistered = false;
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);
            }
            if (this._rawInputMouseRegistered)
            {
                this._rawInputKeyboardRegistered = false;
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);
            }

            this.thisWindowHandleSrc?.Dispose();
            await this.UnloadArchive();
        }

        private void MetroWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    break;
                case Key.Down:
                    break;
            }
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            using (var reg = Registry.CurrentUser.CreateSubKey(Path.Combine("SOFTWARE", "leayal", "ArchivedImageViewer"), true))
            {
                var bound = this.RestoreBounds;
                bool isMaximized = this.WindowState == WindowState.Maximized;
                reg.SetValue("Maximized", (isMaximized ? 1 : 0), RegistryValueKind.DWord);
                if (this.WindowState != WindowState.Normal)
                {
                    reg.SetValue("X", Convert.ToInt32(bound.Left), RegistryValueKind.DWord);
                    reg.SetValue("Y", Convert.ToInt32(bound.Top), RegistryValueKind.DWord);
                    reg.SetValue("Width", Convert.ToInt32(bound.Width), RegistryValueKind.DWord);
                    reg.SetValue("Height", Convert.ToInt32(bound.Height), RegistryValueKind.DWord);
                }
                else
                {
                    reg.SetValue("X", Convert.ToInt32(this.Left), RegistryValueKind.DWord);
                    reg.SetValue("Y", Convert.ToInt32(this.Top), RegistryValueKind.DWord);
                    reg.SetValue("Width", Convert.ToInt32(this.Width), RegistryValueKind.DWord);
                    reg.SetValue("Height", Convert.ToInt32(this.Height), RegistryValueKind.DWord);
                }
                reg.Flush();
            }
        }

        sealed class ArchiveFilenameComparer : StringComparer
        {
            private static readonly char[] splitters = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            public override int Compare(string x, string y)
            {
                if (this.Equals(x, y)) return 0;

                x.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                return NativeMethods.SafeNativeMethods.StrCmpLogicalW(x, y);
            }

            public override bool Equals(string x, string y) => string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);

            public override int GetHashCode(string obj) => StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj);
        }
    }
}
