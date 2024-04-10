using SharpCompress.Common;

namespace Leayal.MangaViewer.Classes
{
    readonly struct DirectoryEntry : IEntry
    {
        private readonly string path;
        internal readonly FileInfo InfoFile;

        internal DirectoryEntry(string path, string fullpath)
        {
            this.path = path;
            this.InfoFile = new FileInfo(fullpath);
        }

        public readonly FileStream OpenStream() => this.InfoFile.OpenRead();

        public readonly CompressionType CompressionType => CompressionType.None;

        public readonly DateTime? ArchivedTime => this.InfoFile.LastAccessTime;

        public readonly long CompressedSize => this.InfoFile.Length;

        public readonly long Crc => 0L;

        public readonly DateTime? CreatedTime => this.InfoFile.CreationTime;

        public readonly string Key => this.path;

        public readonly string? LinkTarget => this.InfoFile.LinkTarget;

        public readonly bool IsDirectory => false;

        public readonly bool IsEncrypted => this.InfoFile.Attributes.HasFlag(FileAttributes.Encrypted);

        public readonly bool IsSplitAfter => false;

        public readonly DateTime? LastAccessedTime => this.InfoFile.LastAccessTime;

        public readonly DateTime? LastModifiedTime => this.InfoFile.LastWriteTime;

        public readonly long Size => this.InfoFile.Length;

        public readonly int? Attrib => (int)this.InfoFile.Attributes;

        public readonly bool IsSolid => true;

        public int VolumeIndexFirst => 0;

        public int VolumeIndexLast => 0;
    }
}
