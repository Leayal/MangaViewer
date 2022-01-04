using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Leayal.MangaViewer
{
    partial class Form1
    {
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
    }

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class BrowserController
    {
        public string Endpoint_ArchiveGetImage { get; }

        private readonly Form1 _form;
        public BrowserController(Form1 form, string endpoint_archiveGetImage)
        {
            this._form = form;
            this.Endpoint_ArchiveGetImage = endpoint_archiveGetImage;
        }

        public string OpenArchive()
        {
            var collection = this._form.mapping_filenames;
            lock (collection)
            {
                if (collection.Count == 0)
                {
                    return string.Empty;
                }
                else
                {
                    using (var mem = Form1.memMgr.GetStream("MangaInfo"))
                    {
                        mem.SetLength(0);
                        using (var writer = new Utf8JsonWriter(mem, new JsonWriterOptions() { Indented = false }))
                        {
                            writer.WriteStartObject();

                            writer.WriteString("name", this._form.archiveName ?? string.Empty);

                            writer.WriteStartArray("images");
                            foreach (var imgname in collection)
                            {
                                writer.WriteStringValue(imgname.Value);
                            }
                            writer.WriteEndArray();

                            writer.WriteEndObject();
                            writer.Flush();
                        }
                        mem.Position = 0;
                        if (mem.TryGetBuffer(out var seg))
                        {
                            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(seg.Array, seg.Offset, seg.Count));
                        }
                        else
                        {
                            return Encoding.UTF8.GetString(mem.ToArray());
                        }
                    }
                }
            }
        }
    }

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class MangaObj
    {

    }
}
