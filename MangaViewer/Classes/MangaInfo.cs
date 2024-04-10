using Leayal.MultipleReaderFileStream;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// This whole thing is a mess of nested classes.

namespace Leayal.MangaViewer.Classes
{
    abstract partial class MangaInfo : IDisposable
    {
        private readonly static EnumerationOptions EnumMangaChaptersOrPages = new EnumerationOptions()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.System
        };

        public static MangaInfo FromDirectory(string directoryPath)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(directoryPath);
            if (!Directory.Exists(directoryPath)) throw new DirectoryNotFoundException();
            return new MangaInfoFromDirectory(directoryPath);
        }

        public static MangaInfo CreateFromArchive(string archivePath)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(archivePath);
            if (!File.Exists(archivePath)) throw new FileNotFoundException(null, archivePath);
            var fs = File.OpenRead(archivePath);
            return MangaInfoFromArchive.CreateFromArchive(fs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        /// <summary>The returned <seealso cref="ReadOnlyMemory{T}"/> contains the characters of the path that follows the last separator in path.</summary>
        protected static ReadOnlyMemory<char> GetFilenameAsMemory(in ReadOnlyMemory<char> memory)
        {
            var theSupposedLength = Path.GetFileName(memory.Span);
            return memory.Slice(memory.Length - theSupposedLength.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool IsImageFile(ReadOnlySpan<char> filename) => (filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".avif", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase));

        // protected readonly Dictionary<ReadOnlyMemory<char>, string> mapped_files;
        protected readonly List<MangaChapterInfo> chapterList;

        private MangaInfo(IMangaInfoParser parser)
        {
            // this.mapped_filename = new Dictionary<ReadOnlyMemory<char>, string>(MemoryOfCharEqualityComparer.OrdinalIgnoreCase);

            this.MangaName = new string(parser.GetMangaName());

            var list = new List<MangaChapterInfo>();
            var tmpListOfPage = new SortedList<ReadOnlyMemory<char>, Guid>(NaturalComparer.Default);
            int chapterCount = 0;
            foreach (var chapter in parser.BeginReadChapters())
            {
                chapterCount++;
                tmpListOfPage.Clear();
                foreach (var pageInfo in chapter.BeginReadPages())
                {
                    tmpListOfPage.Add(pageInfo.Key, pageInfo.Value);
                }
                var chapName = chapter.GetChapterName();
                list.Add(new MangaChapterInfo(chapName, tmpListOfPage.Values.ToImmutableArray()));
            }
            list.Sort();
            this.chapterList = list;
        }

        public readonly string MangaName;

        public ReadOnlySpan<MangaChapterInfo> Chapters => CollectionsMarshal.AsSpan(this.chapterList);

        public abstract bool TryGetImageContent(in Guid imageId, [NotNullWhen(true)] out Stream? contentStream);
        public abstract bool TryGetFilename(in Guid fileId, out ReadOnlyMemory<char> filename);
        public abstract void Dispose();
    }
}
