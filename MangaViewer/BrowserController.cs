using Leayal.MangaViewer.Classes;
using Microsoft.IO;
using SharpCompress.Writers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace Leayal.MangaViewer
{
    public interface IBrowserController
    {
        string Endpoint_ArchiveGetImage { get; }

        string OpenArchive();
    }

    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class BrowserController : IBrowserController
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
            if (this._form.currentOpeningManga is MangaInfo mangaObj)
            {
                lock (mangaObj)
                {
                    var mangaName = string.IsNullOrEmpty(mangaObj.MangaName) ? string.Empty : mangaObj.MangaName;
                    using (var tmpMem = Form1.memMgr.GetStream())
                    {
                        tmpMem.Position = 0;
                        using (var writer = new Utf8JsonWriter((System.Buffers.IBufferWriter<byte>)tmpMem, new JsonWriterOptions() { Indented = false }))
                        {
                            writer.WriteStartObject();
                            writer.WriteString("name", mangaName);
                            writer.WritePropertyName("chapters");
                            writer.WriteStartObject();

                            var chapters = mangaObj.Chapters;
                            var chapterLen = chapters.Length;
                            for (int chapterIndex = 0; chapterIndex < chapterLen; chapterIndex++)
                            {
                                ref readonly var chapterInfo = ref chapters[chapterIndex];
                                // Begin chapter descriptions
                                var chapterName = chapterInfo.ChapterName.Span;
                                var pages = chapterInfo.Pages;
                                var pageLen = pages.Count;

                                writer.WriteStartArray(chapterName.IsEmpty ? IMangaChapterParser.Chapter0 : chapterName);
                                for (int pageIndex = 0; pageIndex < pageLen; pageIndex++)
                                {
                                    writer.WriteStringValue(pages[pageIndex].ToString());
                                }

                                writer.WriteEndArray();
                            }

                            writer.WriteEndObject();
                            writer.WriteEndObject();

                            writer.Flush();
                        }
                        tmpMem.Position = 0;
                        using (var sr = new StreamReader(tmpMem, leaveOpen: true))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
            else
            {
                return string.Empty;
            }
        }
    }

    public interface IMangaObj
    {

    }

    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MangaObj : IMangaObj
    {

    }
}
