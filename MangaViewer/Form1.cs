using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.IO;
using System.Text.Json;
using System.Reflection;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Leayal.MangaViewer.Classes;
using System.Runtime.CompilerServices;
using System;
using System.IO;

namespace Leayal.MangaViewer
{
    public partial class Form1 : Form
    {
        const string Uri_WebHome = "file://app/web/",
            Uri_ArchiveInfo = "file://app/archive/info/",
            Uri_ArchiveOpen = "file://app/archive/open/",
            Uri_ArchiveGetImage = "file://app/archive/image/";

        internal static readonly RecyclableMemoryStreamManager memMgr;
        internal static readonly Assembly CurrentMe;
        internal readonly static System.Buffers.SearchValues<char> directorySeparatorChars = System.Buffers.SearchValues.Create(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

        static Form1()
        {
            CurrentMe = Assembly.GetExecutingAssembly();
            memMgr = new RecyclableMemoryStreamManager(new RecyclableMemoryStreamManager.Options()
            {
                GenerateCallStacks = false,
                AggressiveBufferReturn = true
            });
        }

        private readonly WebView2 web;
        private readonly CancellationTokenSource cancelSrc;
        private CoreWebView2Environment webEnv;
        internal Task<MangaInfo>? t_parseManga;
        internal MangaInfo? currentOpeningManga;
        private readonly BrowserController controller;
        private bool addedController;
        private string? _preopenSomething;
        private readonly string _originalTitle;
        private readonly TaskCompletionSource<WebBrowserTask> tSrc_webTask;
        private readonly Task<WebBrowserTask> t_webTask;
        private readonly TaskCompletionSource<CoreWebView2> tSrc_webCore;
        private readonly Task<CoreWebView2> t_webCore;

        public Form1() : this(string.Empty) { }

        public Form1(string? preopenSomething)
        {
            Unsafe.SkipInit(out web);
            Unsafe.SkipInit(out webEnv);
            this.cancelSrc = new CancellationTokenSource();
            this.controller = new BrowserController(this, Uri_ArchiveGetImage);
            this.addedController = false;

            this.tSrc_webCore = new TaskCompletionSource<CoreWebView2>();
            this.t_webCore = this.tSrc_webCore.Task;
            this.tSrc_webTask = new TaskCompletionSource<WebBrowserTask>();
            this.t_webTask = this.tSrc_webTask.Task;

            this._preopenSomething = preopenSomething;

            InitializeComponent();
            this._originalTitle = this.Text;
            this.LoadFormState();

            var h = this.menu_main.Height;

            if (Program.WebView2Version(out var dir, out var ver))
            {
                this.web = new WebView2()
                {
                    // Dock = DockStyle.Bottom,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right,
                    Location = new Point(0, h),
                    Size = new Size(this.ClientSize.Width, this.ClientSize.Height - h),
                    CreationProperties = new CoreWebView2CreationProperties() { BrowserExecutableFolder = Path.Combine(dir, ver), UserDataFolder = Path.GetFullPath("data", Application.StartupPath) },
                    Visible = true
                };
                this.Controls.Add(this.web);
            }
            else
            {
                this.Controls.AddRange(new Control[] { new Label() { Text = "Please install WebView2 Evergreen Runtime from " },
                    new LinkLabel() { Text = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section" },
                    new Label() { Text = "then restart the application to use." } });
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
            {
                this.SaveFormState();
            }
        }

        public void LoadFormState()
        {
            var filepath = Path.GetFullPath("window-state.json", Application.StartupPath);
            if (File.Exists(filepath))
            {
                using (var fs = File.OpenRead(filepath))
                {
                    if (fs.Length != 0)
                    {
                        using (var jsondoc = JsonDocument.Parse(fs))
                        {
                            var root = jsondoc.RootElement;
                            if (root.TryGetProperty("x", out var prop_x) && prop_x.ValueKind == JsonValueKind.Number
                                && root.TryGetProperty("y", out var prop_y) && prop_y.ValueKind == JsonValueKind.Number
                                && root.TryGetProperty("width", out var prop_width) && prop_width.ValueKind == JsonValueKind.Number
                                && root.TryGetProperty("height", out var prop_height) && prop_height.ValueKind == JsonValueKind.Number)
                            {
                                int x = prop_x.GetInt32(), y = prop_y.GetInt32(), width = prop_width.GetInt32(), height = prop_height.GetInt32();
                                var desktopBound = Screen.GetBounds(this);
                                var formBound = new Rectangle(x, y, width, height);
                                if (desktopBound.Contains(formBound))
                                {
                                    bool isMaximized = (root.TryGetProperty("maximized", out var prop_maximized) && prop_maximized.ValueKind == JsonValueKind.True);
                                    var previousstate = this.WindowState;
                                    if (previousstate == FormWindowState.Maximized || previousstate == FormWindowState.Minimized)
                                    {
                                        this.WindowState = FormWindowState.Normal;
                                    }
                                    this.Location = formBound.Location;
                                    this.Size = formBound.Size;
                                    if (isMaximized)
                                    {
                                        this.WindowState = FormWindowState.Maximized;
                                    }
                                    else
                                    {
                                        this.WindowState = previousstate;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void SaveFormState()
        {
            using (var fs = File.Create(Path.GetFullPath("window-state.json", Application.StartupPath)))
            using (var writer = new Utf8JsonWriter(fs))
            {
                var state = this.WindowState;
                Rectangle bound;
                writer.WriteStartObject();
                switch (state)
                {
                    case FormWindowState.Maximized:
                    case FormWindowState.Minimized:
                        bound = this.RestoreBounds;
                        break;
                    default:
                        bound = new Rectangle(this.Location, this.Size);
                        break;
                }
                writer.WriteNumber("x", bound.X);
                writer.WriteNumber("y", bound.Y);
                writer.WriteNumber("width", bound.Width);
                writer.WriteNumber("height", bound.Height);
                writer.WriteBoolean("maximized", (state == FormWindowState.Maximized));
                writer.WriteEndObject();
                writer.Flush();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (this.t_webTask.IsCompletedSuccessfully)
            {
                this.t_webTask.Result.Dispose();
            }
            this.cancelSrc.Cancel();
            this.web.CoreWebView2?.Stop();
            this.web.Dispose();
            this.CloseCurrentArchive();
            base.OnFormClosed(e);
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            this.webEnv = await CoreWebView2Environment.CreateAsync(this.web.CreationProperties.BrowserExecutableFolder, this.web.CreationProperties.UserDataFolder, new CoreWebView2EnvironmentOptions() { AdditionalBrowserArguments = "--disable-breakpad" });
            var core = await InitWebCore(this.webEnv);
            this.tSrc_webCore.SetResult(core);
            this.tSrc_webTask.SetResult(new WebBrowserTask(core));
            core.WebResourceRequested += this.Core_WebResourceRequested;
            // core.DOMContentLoaded += this.Core_DOMContentLoaded;
            core.WebMessageReceived += this.Core_WebMessageReceived;
            core.ScriptDialogOpening += this.Core_ScriptDialogOpening;
            core.NavigationStarting += this.Core_NavigationStarting;
            core.DocumentTitleChanged += this.Core_DocumentTitleChanged;
            core.AddWebResourceRequestedFilter(Uri_WebHome + "*", CoreWebView2WebResourceContext.All);
            core.AddWebResourceRequestedFilter(Uri_ArchiveInfo, CoreWebView2WebResourceContext.Fetch);
            core.AddWebResourceRequestedFilter(Uri_ArchiveInfo, CoreWebView2WebResourceContext.XmlHttpRequest);
            core.AddWebResourceRequestedFilter(Uri_ArchiveOpen, CoreWebView2WebResourceContext.All);
            core.AddWebResourceRequestedFilter(Uri_ArchiveGetImage + "*", CoreWebView2WebResourceContext.Image);
            core.ContextMenuRequested += Core_ContextMenuRequested;

            core.Settings.IsWebMessageEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.UserAgent = "Leayal Manga Viewer 1.0";
            core.Settings.IsBuiltInErrorPageEnabled = true;
            core.Settings.AreHostObjectsAllowed = true;

#if DEBUG
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.OpenDevToolsWindow();
#else
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
#endif

            if (Uri.TryCreate(new Uri(Uri_WebHome), "index.html", out var homepage))
            {
                this.web.Source = homepage;
            }
        }

        private void Core_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            if (e.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.Image && sender is CoreWebView2 core)
            {
                var count = e.MenuItems.Count;
                for (int i = 0; i < count; i++)
                {
                    switch (e.MenuItems[i].CommandId)
                    {
                        case 50110:
                            var olditem = e.MenuItems[i];
                            var item = core.Environment.CreateContextMenuItem("Sa&ve image as", olditem.Icon, CoreWebView2ContextMenuItemKind.Command);
                            var uri = e.ContextMenuTarget.SourceUri;
                            var openingManga = this.currentOpeningManga;

                            EventHandler<object>? callback = null;
                            callback = (menu, _) =>
                            {
                                item.CustomItemSelected -= callback;
                                if (openingManga == null) return;
                                if (uri.StartsWith(Uri_ArchiveGetImage, StringComparison.OrdinalIgnoreCase))
                                {
                                    var str_Guid = GetRelativePathFromUrl(uri);
                                    var fileId = Guid.Parse(str_Guid.Span);
                                    if (openingManga.TryGetImageContent(fileId, out var stream))
                                    {
                                        this.BeginInvoke(this.Core_ImageSaveAsWorkaround, new Tuple<ReadOnlyMemory<char>, Stream>(openingManga.TryGetFilename(fileId, out var filename) ? filename : str_Guid, stream));
                                    }
                                }
                            };

                            item.CustomItemSelected += callback;
                            e.MenuItems[i] = item;

                            break;
                        case 50111:
                            e.MenuItems.RemoveAt(i);
                            break;
                    }
                }
            }
        }

        private void Core_ImageSaveAsWorkaround(Tuple<ReadOnlyMemory<char>, Stream> streaminfo)
        {
            using (var sfd = new SaveFileDialog())
            {
                var filenameSpan = streaminfo.Item1.Span;
                var ext = Path.GetExtension(filenameSpan);
                sfd.FileName = new string(Path.GetFileName(filenameSpan));
                sfd.RestoreDirectory = true;
                sfd.DefaultExt = new string(ext);
                if (ext.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    sfd.Filter = "PNG Image|*.png";
                }
                else if (ext.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    sfd.Filter = "JPEG Image|*.jpg";
                }
                else if (ext.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    sfd.Filter = "WEBP Image|*.webp";
                }
                else if (ext.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                {
                    sfd.Filter = "Bitmap Image|*.bmp";
                }
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    Task.Factory.StartNew(async tuple =>
                    {
                        if (tuple is Tuple<Stream, string> data)
                        {
                            var (stream, dst) = data;
                            stream.Position = 0;
                            using (var handle = File.OpenHandle(dst, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.Asynchronous, stream.Length))
                            using (var fs = new FileStream(handle, FileAccess.Write, 0))
                            {
                                await stream.CopyToAsync(fs, 4096 * 3).ConfigureAwait(false);
                                await fs.FlushAsync().ConfigureAwait(false);
                            }
                        }
                    }, new Tuple<Stream, string>(streaminfo.Item2, sfd.FileName));
                }
            }
        }

        private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;
            if (!string.IsNullOrEmpty(json))
            {
                using (var jsonDocument = JsonDocument.Parse(json))
                {
                    if (jsonDocument.RootElement.TryGetProperty("event", out var prop_event) && prop_event.ValueKind == JsonValueKind.String)
                    {
                        var eventName = prop_event.GetString();
                        if (string.Equals(eventName, "web-core-ready", StringComparison.OrdinalIgnoreCase))
                        {
                            this.Core_ReadyToBeUsed();
                        }
                    }
                }
            }
        }

        private async void Core_ReadyToBeUsed()
        {
            var duh = Interlocked.Exchange(ref this._preopenSomething, null);
            await this.t_webCore;
            await this.t_webTask;
            if (!string.IsNullOrWhiteSpace(duh))
            {
                if (File.Exists(duh))
                {
                    await this.OpenArchive(duh);
                }
                else if (Directory.Exists(duh))
                {
                    await this.OpenDirectory(duh);
                }
            }
        }

        private async void Core_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith(Uri_WebHome, StringComparison.OrdinalIgnoreCase))
            {
                if (!this.addedController)
                {
                    this.addedController = true;
                    (await this.t_webCore).AddHostObjectToScript("leayal", this.controller);
                }
            }
            else
            {
                if (this.addedController)
                {
                    this.addedController = false;
                    (await this.t_webCore).RemoveHostObjectFromScript("leayal");
                }
            }
        }

        private void Core_ScriptDialogOpening(object? sender, CoreWebView2ScriptDialogOpeningEventArgs e)
        {
            switch (e.Kind)
            {
                case CoreWebView2ScriptDialogKind.Alert:
                    MessageBox.Show(this, e.Message, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
            }
        }

        private async Task<CoreWebView2> InitWebCore(CoreWebView2Environment webenv)
        {
            await this.web.EnsureCoreWebView2Async(this.webEnv);
            return this.web.CoreWebView2;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.CloseCurrentArchive();
            // var core = await this.t_webCore;
            await (await this.t_webTask).SetState("no-archive");
        }

        private async void OpenFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var directoryPath = BrowseForDirectory();
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    await this.OpenDirectory(directoryPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OpenArchiveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var filename = BrowseForArchive();
                if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
                {
                    await this.OpenArchive(filename);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OpenDirectory(string directoryPath)
        {
            await (await this.t_webTask).SetState("loading");
            this.CloseCurrentArchive();
            try
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                MangaInfo DoWork(object? obj) => (obj is string str ? MangaInfo.FromDirectory(str) : throw new Exception("Can't be here anyway"));
                this.t_parseManga = Task.Factory.StartNew(DoWork, directoryPath);
                var newMangaInfo = await this.t_parseManga;
                this.currentOpeningManga = newMangaInfo;
                await this.OnLoadArchiveComplete();
            }
            catch
            {
                this.CloseCurrentArchive();
                throw;
            }
        }

        private async Task OpenArchive(string filename)
        {
            await (await this.t_webTask).SetState("loading");
            this.CloseCurrentArchive();
            try
            {
                this.t_parseManga = Task.Factory.StartNew(this.ParseArchive, filename);
                var newMangaInfo = await this.t_parseManga;
                this.currentOpeningManga = newMangaInfo;
                await this.OnLoadArchiveComplete();
            }
            catch
            {
                this.CloseCurrentArchive();
                throw;
            }
        }

        private MangaInfo ParseArchive(object? obj)
        {
            if (obj is string str)
            {
                return MangaInfo.CreateFromArchive(str);
            }
            return null;
        }

        private async Task OnLoadArchiveComplete()
        {
            await (await this.t_webTask).LoadManga();
        }

        private async void Core_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (e.Request.Uri.StartsWith(Uri_WebHome, StringComparison.OrdinalIgnoreCase))
            {
                using (var deferral = e.GetDeferral())
                {
                    var path = e.Request.Uri.Substring(Uri_WebHome.Length).Replace('/', '.');
                    var stream = CurrentMe.GetManifestResourceStream($"Leayal.MangaViewer.WebPage.{path}");
                    if (stream is not null)
                    {
                        var rep = this.webEnv.CreateWebResourceResponse(stream, 200, "OK", string.Empty);
                        rep.Headers.AppendHeader("Content-Length", stream.Length.ToString());
                        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "text/html; charset=UTF-8");
                        }
                        else if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "text/plain; charset=UTF-8");
                        }
                        else if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "text/javascript");
                        }
                        else if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "text/css");
                        }
                        else if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "application/json");
                        }
                        e.Response = rep;
                        //deferral.Complete();
                    }
                }
            }
            else if (e.ResourceContext == CoreWebView2WebResourceContext.Image && e.Request.Uri.StartsWith(Uri_ArchiveGetImage, StringComparison.OrdinalIgnoreCase))
            {
                using (var deferral = e.GetDeferral())
                {
                    MangaInfo? mangaInfo = (this.t_parseManga == null ? this.currentOpeningManga : (await this.t_parseManga));
                    if (mangaInfo == null)
                    {
                        e.Response = this.webEnv.CreateWebResourceResponse(null, 500, "Internal Error", string.Empty);
                        return;
                    }
                    var str_Guid = GetRelativePathFromUrl(e.Request.Uri);
                    var fileId = Guid.Parse(str_Guid.Span);
                    if (mangaInfo.TryGetImageContent(fileId, out var stream))
                    {
                        stream.Position = 0;
                        var rep = this.webEnv.CreateWebResourceResponse(stream, 200, "OK", string.Empty);
                        rep.Headers.AppendHeader("Content-Length", stream.Length.ToString());
                        rep.Headers.AppendHeader("Cache-Control", "no-cache");

                        static void SetContentType(CoreWebView2WebResourceResponse rep, ReadOnlyMemory<char> path)
                        {
                            var filepath = path.Span;
                            if (filepath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            {
                                rep.Headers.AppendHeader("Content-Type", "image/png");
                            }
                            else if (filepath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                            {
                                rep.Headers.AppendHeader("Content-Type", "image/jpeg");
                            }
                            else if (filepath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                            {
                                rep.Headers.AppendHeader("Content-Type", "image/webp");
                            }
                            else if (filepath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                            {
                                rep.Headers.AppendHeader("Content-Type", "image/bmp");
                            }
                        }

                        if (mangaInfo.TryGetFilename(fileId, out var filename))
                        {
                            SetContentType(rep, filename);
                        }
                        else
                        {
                            rep.Headers.AppendHeader("Content-Type", "image/*");
                        }
                        e.Response = rep;
                    }
                }
            }
        }

        private static ReadOnlyMemory<char> GetRelativePathFromUrl(string url)
        {
            if (url.Length > Uri_ArchiveGetImage.Length)
            {
                return url.AsMemory(Uri_ArchiveGetImage.Length);
            }
            else
            {
                return url.AsMemory();
            }
        }

        private static string NormalizeFilePath(in ReadOnlyMemory<char> path)
        {
            var mem = path.Trim();
            if (mem.Length == 0)
            {
                return string.Empty;
            }
            else
            {
                return string.Create<ReadOnlyMemory<char>>(mem.Length, mem, (c, state) =>
                {
                    state.Span.CopyTo(c);
                    for (int i = 0; i < c.Length; i++)
                    {
                        ref var currentChar = ref c[i];
                        if (currentChar == Path.AltDirectorySeparatorChar) currentChar = Path.DirectorySeparatorChar;
                    }
                });
            }
        }

        private void CloseCurrentArchive()
        {
            Interlocked.Exchange(ref this.currentOpeningManga, null)?.Dispose();
        }

        internal string BrowseForArchive()
        {
            using (var ofd = new OpenFileDialog()
            {
                Filter = "Archive Files|*.zip;*.7z;*.rar;*.tar;*.gz|Any Files|*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            })
            {
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    return ofd.FileName;
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        internal string BrowseForDirectory()
        {
            using (var ofd = new FolderBrowserDialog()
            {
                AutoUpgradeEnabled = true,
                ShowNewFolderButton = false,
                UseDescriptionForTitle = true,
                Description = "Select folder to view all image(s) within"
            })
            {
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    return ofd.SelectedPath;
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        private void Core_DocumentTitleChanged(object? sender, object e)
        {
            var documentTitle = this.web.CoreWebView2.DocumentTitle;
            if (string.IsNullOrEmpty(documentTitle) || documentTitle == "index.html")
            {
                this.Text = this._originalTitle;
            }
            else
            {
                this.Text = $"{this._originalTitle}: {documentTitle}";
            }
        }

        internal class StreamInfo : IEquatable<StreamInfo>
        {
            public readonly string Name;
            public readonly Stream DataStream;

            public StreamInfo(string name, Stream content)
            {
                this.Name = name;
                this.DataStream = content;
            }

            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name);

            public override bool Equals(object? obj)
            {
                if (obj is StreamInfo other)
                {
                    return this.Equals(other);
                }
                else
                {
                    return false;
                }
            }

            public bool Equals(StreamInfo? other)
            {
                if (other is null)
                {
                    return false;
                }
                else
                {
                    return string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}