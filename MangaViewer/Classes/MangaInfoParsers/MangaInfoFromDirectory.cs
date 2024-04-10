using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Security.Policy;
using Leayal.MultipleReaderFileStream;

namespace Leayal.MangaViewer.Classes
{
    partial class MangaInfo
    {
        sealed class MangaInfoFromDirectory : MangaInfo
        {
            private static readonly char[] TrimStart_ = { '.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            private readonly string currentDirectory;

            private readonly ConcurrentDictionary<string, IFileStreamBroker> mapped_files;
            private readonly ImmutableDictionary<Guid, ReadOnlyMemory<char>> cached_map_fileId_filename;

            public MangaInfoFromDirectory(string dir) : this(dir, new DirectoryMangaInfoReader(dir)) { }

            private MangaInfoFromDirectory(string dir, in DirectoryMangaInfoReader reader) : base(reader) 
            {
                this.currentDirectory = dir;
                this.mapped_files = new ConcurrentDictionary<string, IFileStreamBroker>(StringComparer.OrdinalIgnoreCase);
                this.cached_map_fileId_filename = reader.cached_map_fileId_filename.ToImmutableDictionary();
            }

            public override bool TryGetImageContent(in Guid fileId, [NotNullWhen(true)] out Stream? contentStream)
            {
                if (fileId == Guid.Empty) throw new ArgumentException(null, nameof(fileId));
                
                if (!this.cached_map_fileId_filename.TryGetValue(fileId, out var filename))
                {
                    throw new KeyNotFoundException();
                }

                var fullPath = Path.Join(this.currentDirectory, filename.Span);
                try
                {
                    var broker = this.mapped_files.GetOrAdd(fullPath, CreateNewBroker);
                    contentStream = broker.OpenRead();
                    return true;
                }
                catch (FileNotFoundException)
                {
                    contentStream = null;
                    return false;
                }
            }

            private static IFileStreamBroker CreateNewBroker(string filepath)
            {
                if (!File.Exists(filepath)) throw new FileNotFoundException(null, filepath);
                return OptimizedMultipleReaderFileStream.OpenOrCreateFile(filepath);
            }

            public override void Dispose()
            {
                var values = this.mapped_files.Values;
                var total = values.Count;
                var copy = new IFileStreamBroker[total];
                values.CopyTo(copy, 0);
                this.mapped_files.Clear();
                var span = new ReadOnlySpan<IFileStreamBroker>(copy);
                for (int i = 0; i < total; i++)
                {
                    span[i].Dispose();
                }
            }

            public override bool TryGetFilename(in Guid fileId, out ReadOnlyMemory<char> filename) => this.cached_map_fileId_filename.TryGetValue(fileId, out filename);

            public readonly struct DirectoryMangaInfoReader : IMangaInfoParser
            {
                private readonly string currentDir;
                public readonly Dictionary<Guid, ReadOnlyMemory<char>> cached_map_fileId_filename;

                public DirectoryMangaInfoReader(string directory)
                {
                    this.currentDir = directory;
                    this.cached_map_fileId_filename = new Dictionary<Guid, ReadOnlyMemory<char>>();
                }

                public readonly IEnumerable<IMangaChapterParser> BeginReadChapters()
                {
                    var chapterZeroPageWalker = Directory.EnumerateFiles(this.currentDir, "*", EnumMangaChaptersOrPages).GetEnumerator();
                    var len = this.currentDir.Length + 1;
                    while (chapterZeroPageWalker.MoveNext())
                    {
                        var currentPath = chapterZeroPageWalker.Current.AsMemory(len);
                        if (IsImageFile(currentPath.Span))
                        {
                            yield return new MangaChapterParser(this.currentDir, chapterZeroPageWalker, ReadOnlyMemory<char>.Empty, this.cached_map_fileId_filename);
                            break;
                        }
                    }

                    foreach (var chapterDirectory in Directory.EnumerateDirectories(this.currentDir, "*", EnumMangaChaptersOrPages))
                    {
                        var pageWalker = Directory.EnumerateFiles(chapterDirectory, "*", EnumMangaChaptersOrPages).GetEnumerator();

                        len = chapterDirectory.Length + 1;
                        while (pageWalker.MoveNext())
                        {
                            var currentPath = pageWalker.Current.AsMemory(len);
                            if (IsImageFile(currentPath.Span))
                            {
                                yield return new MangaChapterParser(chapterDirectory, pageWalker, this.cached_map_fileId_filename);
                                break;
                            }
                        }
                    }
                }

                public readonly void Dispose()
                {
                    // Do nothing
                }

                public readonly ReadOnlySpan<char> GetMangaName()
                    => Path.GetFileName(this.currentDir.AsSpan());
            }

            readonly struct MangaChapterParser : IMangaChapterParser
            {
                private readonly string currentDirectory;
                private readonly ReadOnlyMemory<char> chapterName;
                private readonly IEnumerator<string> pageWalker;
                private readonly Dictionary<Guid, ReadOnlyMemory<char>> cached_map_fileId_filename;

                public MangaChapterParser(string dir, IEnumerator<string> pageWalker, Dictionary<Guid, ReadOnlyMemory<char>> map_fileId_filename) : this(dir, pageWalker, GetFilenameAsMemory(dir.AsMemory()), map_fileId_filename) { }

                public MangaChapterParser(string dir, IEnumerator<string> pageWalker, ReadOnlyMemory<char> chapterName, Dictionary<Guid, ReadOnlyMemory<char>> map_fileId_filename)
                {
                    this.currentDirectory = dir;
                    this.chapterName = chapterName;
                    this.pageWalker = pageWalker;
                    this.cached_map_fileId_filename = map_fileId_filename;
                }

                public readonly IEnumerable<KeyValuePair<ReadOnlyMemory<char>, Guid>> BeginReadPages()
                {
                    var len = this.currentDirectory.Length + 1;
                    var fileId = Guid.NewGuid();
                    var memFilename = this.pageWalker.Current.AsMemory(len);
                    this.cached_map_fileId_filename.Add(fileId, memFilename);
                    yield return new KeyValuePair<ReadOnlyMemory<char>, Guid>(memFilename, fileId);
                    while (this.pageWalker.MoveNext())
                    {
                        var currentPath = this.pageWalker.Current.AsMemory(len);
                        if (IsImageFile(currentPath.Span))
                        {
                            fileId = Guid.NewGuid();
                            memFilename = this.pageWalker.Current.AsMemory(len);
                            this.cached_map_fileId_filename.Add(fileId, memFilename);
                            yield return new KeyValuePair<ReadOnlyMemory<char>, Guid>(memFilename, fileId);
                        }
                    }
                }

                public readonly void Dispose()
                {
                    // Do nothing as there's nothing to cleanup anyway.
                }

                public readonly ReadOnlyMemory<char> GetChapterName() => this.chapterName;
            }
        }
    }
}
