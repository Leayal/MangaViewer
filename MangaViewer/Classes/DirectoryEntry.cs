using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    struct DirectoryEntry : IEntry
    {
        private readonly string path;
        internal readonly FileInfo InfoFile;

        internal DirectoryEntry(string path, string fullpath)
        {
            this.path = path;
            this.InfoFile = new FileInfo(fullpath);
        }

        public FileStream OpenStream() => this.InfoFile.OpenRead();

        public CompressionType CompressionType => CompressionType.None;

        public DateTime? ArchivedTime => this.InfoFile.LastAccessTime;

        public long CompressedSize => this.InfoFile.Length;

        public long Crc => 0L;

        public DateTime? CreatedTime => this.InfoFile.CreationTime;

        public string Key => this.path;

        public string? LinkTarget => this.InfoFile.LinkTarget;

        public bool IsDirectory => false;

        public bool IsEncrypted => this.InfoFile.Attributes.HasFlag(FileAttributes.Encrypted);

        public bool IsSplitAfter => false;

        public DateTime? LastAccessedTime => this.InfoFile.LastAccessTime;

        public DateTime? LastModifiedTime => this.InfoFile.LastWriteTime;

        public long Size => this.InfoFile.Length;

        public int? Attrib => (int)this.InfoFile.Attributes;
    }
}
