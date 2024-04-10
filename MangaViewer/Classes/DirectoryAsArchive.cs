using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    class DirectoryAsArchive : IArchive
    {
        private readonly string directoryPath;

        public DirectoryAsArchive(string path)
        {
            this.directoryPath = path;
        }

        public IEnumerable<IArchiveEntry> Entries => Array.Empty<IArchiveEntry>();

        public IEnumerable<IVolume> Volumes => Array.Empty<IVolume>();

        public ArchiveType Type => ArchiveType.Zip;

        public bool IsSolid => false;

        public bool IsComplete => true;

        public long TotalSize => -1;

        public long TotalUncompressSize => -1;

        public event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>>? EntryExtractionBegin;
        public event EventHandler<ArchiveExtractionEventArgs<IArchiveEntry>>? EntryExtractionEnd;
        public event EventHandler<CompressedBytesReadEventArgs>? CompressedBytesRead;
        public event EventHandler<FilePartExtractionBeginEventArgs>? FilePartExtractionBegin;

        public void Dispose() { }

        public IReader ExtractAllEntries()
        {
            return new DirectoryReader(directoryPath);
        }
    }
}
