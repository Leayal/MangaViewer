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

        static Form1()
        {
            CurrentMe = Assembly.GetExecutingAssembly();
            memMgr = new RecyclableMemoryStreamManager();
        }

        private readonly WebView2 web;
        private readonly CancellationTokenSource cancelSrc;
        private CoreWebView2Environment webEnv;
        internal readonly Dictionary<Guid, StreamInfo> mapping_filestreams;
        internal readonly SortedList<string, Guid> mapping_filenames;
        private IArchive? currentArchive;
        internal Task? t_parseArchive;
        internal string archiveName;
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
            this.archiveName = string.Empty;
            this.currentArchive = null;
            this.mapping_filestreams = new Dictionary<Guid, StreamInfo>();
            this.mapping_filenames = new SortedList<string, Guid>(NaturalComparer.Default);
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
                this.currentArchive = new Classes.DirectoryAsArchive(directoryPath);
                this.archiveName = Path.GetFileName(directoryPath);
                this.t_parseArchive = Task.Factory.StartNew(this.ParseArchive, this.currentArchive);
                await this.t_parseArchive;
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
            var fs = File.OpenRead(filename);
            try
            {
                if (SharpCompress.Archives.SevenZip.SevenZipArchive.IsSevenZipFile(fs))
                {
                    this.currentArchive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(filename, new ReaderOptions() { LeaveStreamOpen = false });
                }
                else
                {
                    this.currentArchive = ArchiveFactory.Open(filename, new ReaderOptions() { LeaveStreamOpen = false });
                }
                this.archiveName = Path.GetFileName(fs.Name);
                this.t_parseArchive = Task.Factory.StartNew(this.ParseArchive, this.currentArchive);
                await this.t_parseArchive;
                await this.OnLoadArchiveComplete();
            }
            catch
            {
                this.CloseCurrentArchive();
                fs.Dispose();
                throw;
            }
        }

        private void ParseArchive(object? obj)
        {
            if (obj is IArchive archive)
            {
                lock (this.mapping_filenames)
                    lock (this.mapping_filestreams)
                    {
                        this.mapping_filestreams.Clear();
                        this.mapping_filenames.Clear();
                        using (var walker = archive.ExtractAllEntries())
                        {
                            try
                            {
                                while (walker.MoveToNextEntry())
                                {
                                    var entry = walker.Entry;
                                    if (!entry.IsDirectory)
                                    {
                                        if (entry is Classes.DirectoryEntry fakeEntry)
                                        {
                                            var guid = Guid.NewGuid();
                                            this.mapping_filenames.Add(fakeEntry.Key, guid);
                                            this.mapping_filestreams.Add(guid, new StreamInfo(fakeEntry.Key, fakeEntry.OpenStream()));
                                        }
                                        else
                                        {
                                            var guid = Guid.NewGuid();
                                            this.mapping_filenames.Add(entry.Key, guid);
                                            var stream = new RecyclableMemoryStream(memMgr, guid, entry.Key);
                                            stream.Position = 0;
                                            stream.SetLength(entry.Size);
                                            walker.WriteEntryTo(stream);
                                            stream.Position = 0;
                                            this.mapping_filestreams.Add(guid, new StreamInfo(entry.Key, stream));
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
            }
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
                    if (this.t_parseArchive is not null)
                    {
                        await this.t_parseArchive;
                    }
                    var path = GetRelativePathFromUrl(e.Request.Uri);
                    if (Guid.TryParse(path.Span, out var guid) && this.mapping_filestreams.TryGetValue(guid, out var streaminfo))
                    {
                        var stream = streaminfo.DataStream;
                        var filepath = streaminfo.Name;
                        stream.Position = 0;
                        var rep = this.webEnv.CreateWebResourceResponse(stream, 200, "OK", string.Empty);
                        rep.Headers.AppendHeader("Content-Length", stream.Length.ToString());
                        rep.Headers.AppendHeader("Cache-Control", "no-cache");
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
                        if (c[i] == Path.AltDirectorySeparatorChar)
                        {
                            c[i] = Path.DirectorySeparatorChar;
                        }
                    }
                });
            }
        }

        private void CloseCurrentArchive()
        {
            if (this.currentArchive is not null)
            {
                this.currentArchive.Dispose();
                this.currentArchive = null;
            }
            this.archiveName = string.Empty;
            lock (this.mapping_filenames)
            {
                this.mapping_filenames.Clear();
            }
            lock (this.mapping_filestreams)
            {
                foreach (var stream in this.mapping_filestreams.Values)
                {
                    stream.DataStream.Dispose();
                }
                this.mapping_filestreams.Clear();
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