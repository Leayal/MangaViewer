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

namespace Leayal.MangaViewer
{
    public partial class Form1 : Form
    {
        const string Uri_WebHome = "file://app/web/",
            Uri_ArchiveInfo = "file://app/archive/info/",
            Uri_ArchiveOpen = "file://app/archive/open/",
            Uri_ArchiveGetImage = "file://app/archive/image/",
            Uri_ArchiveGetImageList = "file://app/archive/imagelist/";

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
        private Task<CoreWebView2> t_webCore;
        private bool _IsArchiveLoaded;
        internal readonly Dictionary<string, Stream> mapping_filestreams;
        internal readonly List<string> mapping_filenames;
        private IArchive? currentArchive;
        internal Task? t_parseArchive;
        internal string archiveName;
        private readonly BrowserController controller;
        private bool addedController;
        private string? _preopenSomething;

        public Form1() : this(string.Empty) { }

        public Form1(string? preopenSomething)
        {
            this.archiveName = string.Empty;
            this.currentArchive = null;
            this.mapping_filestreams = new Dictionary<string, Stream>(StringComparer.OrdinalIgnoreCase);
            this.mapping_filenames = new List<string>();
            this._IsArchiveLoaded = false;
            this.cancelSrc = new CancellationTokenSource();
            this.controller = new BrowserController(this);
            this.addedController = false;

            this._preopenSomething = preopenSomething;

            InitializeComponent();

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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.cancelSrc.Cancel();
            this.web.CoreWebView2?.Stop();
            this.web.Dispose();
            this.CloseCurrentArchive();
            base.OnFormClosed(e);
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            this.webEnv = await CoreWebView2Environment.CreateAsync(this.web.CreationProperties.BrowserExecutableFolder, this.web.CreationProperties.UserDataFolder, new CoreWebView2EnvironmentOptions() { AdditionalBrowserArguments = "--disable-breakpad" });
            this.t_webCore = InitWebCore(this.webEnv);
            var core = await this.t_webCore;
            core.NavigationStarting += this.Core_NavigationStarting;
            core.DocumentTitleChanged += this.Core_DocumentTitleChanged;
            core.AddWebResourceRequestedFilter(Uri_WebHome + "*", CoreWebView2WebResourceContext.All);
            core.AddWebResourceRequestedFilter(Uri_ArchiveInfo, CoreWebView2WebResourceContext.Fetch);
            core.AddWebResourceRequestedFilter(Uri_ArchiveInfo, CoreWebView2WebResourceContext.XmlHttpRequest);
            core.AddWebResourceRequestedFilter(Uri_ArchiveOpen, CoreWebView2WebResourceContext.All);
            core.AddWebResourceRequestedFilter(Uri_ArchiveGetImage + "*", CoreWebView2WebResourceContext.Image);
            core.WebResourceRequested += this.Core_WebResourceRequested;
            core.DOMContentLoaded += this.Core_DOMContentLoaded;
            core.ScriptDialogOpening += this.Core_ScriptDialogOpening;
            
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.UserAgent = "Leayal Manga Viewer 1.0";
            core.Settings.IsBuiltInErrorPageEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreHostObjectsAllowed = true;
            core.Settings.AreDevToolsEnabled = false;

            // core.OpenDevToolsWindow();

            if (Uri.TryCreate(new Uri(Uri_WebHome), "index.html", out var homepage))
            {
                this.web.Source = homepage;
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

        private async void Core_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            // this.webCore.RemoveHostObjectFromScript("leayalMangaObj");
            if (!string.IsNullOrWhiteSpace(this._preopenSomething))
            {
                var duh = this._preopenSomething;
                this._preopenSomething = null;
                if (File.Exists(duh))
                {
                    await this.OpenArchive(duh);
                }
                else if (Directory.Exists(duh))
                {
                    await this.OpenDirectory(duh);
                }
            }
            else if (this._IsArchiveLoaded)
            {
                (await this.t_webCore).AddHostObjectToScript("leayalMangaObj", null);
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.CloseCurrentArchive();
            (await this.t_webCore).Reload();
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
            this.CloseCurrentArchive();
            try
            {
                this.currentArchive = new Classes.DirectoryAsArchive(directoryPath);
                this.archiveName = Path.GetFileName(directoryPath);
                this.t_parseArchive = Task.Factory.StartNew(this.ParseArchive, this.currentArchive);
                await this.t_parseArchive;
                this.OnLoadArchiveComplete();
            }
            catch
            {
                this.CloseCurrentArchive();
                throw;
            }
        }

        private async Task OpenArchive(string filename)
        {
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
                this.OnLoadArchiveComplete();
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
                            while (walker.MoveToNextEntry())
                            {
                                var entry = walker.Entry;
                                if (!entry.IsDirectory)
                                {
                                    if (entry is Classes.DirectoryEntry fakeEntry)
                                    {
                                        var name = NormalizeFilePath(fakeEntry.Key.AsMemory());
                                        this.mapping_filenames.Add(name);
                                        this.mapping_filestreams.Add(name, fakeEntry.OpenStream());
                                    }
                                    else
                                    {
                                        var name = NormalizeFilePath(entry.Key.AsMemory());
                                        this.mapping_filenames.Add(name);
                                        var stream = memMgr.GetStream(name);
                                        stream.Position = 0;
                                        stream.SetLength(entry.Size);
                                        walker.WriteEntryTo(stream);
                                        stream.Position = 0;
                                        this.mapping_filestreams.Add(name, stream);
                                    }
                                }
                            }
                        }
                        this.mapping_filenames.Sort(StrCmpLogicalW);
                    }
            }
        }

        private async void OnLoadArchiveComplete()
        {
            if (this.t_webCore is not null)
            {
                var core = await this.t_webCore;
                core.Reload();
            }
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

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
                    var path = NormalizeFilePath(GetRelativePathFromUrl(e.Request.Uri));
                    if (this.mapping_filestreams.TryGetValue(path, out var stream))
                    {
                        stream.Position = 0;
                        var rep = this.webEnv.CreateWebResourceResponse(stream, 200, "OK", string.Empty);
                        rep.Headers.AppendHeader("Content-Length", stream.Length.ToString());
                        rep.Headers.AppendHeader("Cache-Control", "no-cache");
                        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "image/png");
                        }
                        else if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "image/jpeg");
                        }
                        else if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        {
                            rep.Headers.AppendHeader("Content-Type", "image/webp");
                        }
                        else if (path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
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
                    stream.Dispose();
                }
                this.mapping_filestreams.Clear();
            }
        }

        private void Core_DocumentTitleChanged(object? sender, object e)
        {
            this.Text = this.web.CoreWebView2.DocumentTitle;
        }
    }
}