using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    class DirectoryReader : IReader
    {
        private static readonly Func<IReader, Stream, EntryStream>? _CreateEntryStream;

        static DirectoryReader()
        {
            var t = typeof(EntryStream);
            if (t.GetConstructor(new Type[] { typeof(IReader), typeof(Stream) }) is System.Reflection.ConstructorInfo ctor)
            {
                _CreateEntryStream = new Func<IReader, Stream, EntryStream>((reader, stream) =>
                {
                    return (EntryStream)ctor.Invoke(new object[] { reader, stream });
                });
            }
        }

        private bool _cancel;
        private DirectoryEntry entry;
        private readonly IEnumerator<string> dirWalker;
        private readonly string directoryPath;

        public DirectoryReader(string path)
        {
            this.directoryPath = path;
            this.dirWalker = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).GetEnumerator();
        }

        public ArchiveType ArchiveType => ArchiveType.Zip;

        public IEntry Entry => this.entry;

        public bool Cancelled => this._cancel;

        public event EventHandler<ReaderExtractionEventArgs<IEntry>> EntryExtractionProgress;
        public event EventHandler<CompressedBytesReadEventArgs> CompressedBytesRead;
        public event EventHandler<FilePartExtractionBeginEventArgs> FilePartExtractionBegin;

        public void Cancel()
        {
            this._cancel = true;
        }

        public void Dispose() => this.dirWalker.Dispose();

        public bool MoveToNextEntry()
        {
            while (this.dirWalker.MoveNext())
            {
                var str = this.dirWalker.Current;
                if (str.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || str.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || str.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) || str.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                    || str.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) || str.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                    || str.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    this.entry = new DirectoryEntry(str.Substring(this.directoryPath.Length + 1), str);
                    return true;
                }
            }
            return false;
        }

        public EntryStream OpenEntryStream()
        {
            return _CreateEntryStream?.Invoke(this, this.entry.InfoFile.OpenRead());
        }

        public void WriteEntryTo(Stream writableStream)
        {
            using (var stream = this.entry.InfoFile.OpenRead())
            {
                stream.CopyTo(writableStream);
                writableStream.Flush();
            }
        }
    }
}
