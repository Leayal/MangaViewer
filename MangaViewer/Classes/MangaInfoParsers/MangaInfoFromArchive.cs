using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Leayal.MultipleReaderFileStream;
using Microsoft.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Leayal.MangaViewer.Classes
{
    partial class MangaInfo
    {
        sealed class MangaInfoFromArchive : MangaInfo
        {
            public static MangaInfoFromArchive CreateFromArchive(FileStream fs)
            {
                var oldPos = fs.Position;
                IArchive currentArchive;
                if (SharpCompress.Archives.SevenZip.SevenZipArchive.IsSevenZipFile(fs))
                {
                    fs.Position = oldPos;
                    currentArchive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(fs, new ReaderOptions() { LeaveStreamOpen = false });
                }
                else
                {
                    fs.Position = oldPos;
                    currentArchive = ArchiveFactory.Open(fs, new ReaderOptions() { LeaveStreamOpen = false });
                }
                var filepath = fs.Name;
                var dirRecords = new DirectoryRecord(ReadOnlyMemory<char>.Empty);
                var reader = new ArchiveMangaInfoReader(filepath, currentArchive, dirRecords);
                return new MangaInfoFromArchive(filepath, currentArchive, reader);
            }

            private readonly string currentArchivePath;
            private readonly IArchive currentArchive;

            private readonly ImmutableDictionary<Guid, NotClosingStream<RecyclableMemoryStream>>? cached_entries;
            private readonly ImmutableDictionary<Guid, IArchiveEntry>? cached_entry_header;
            private readonly ImmutableDictionary<Guid, ReadOnlyMemory<char>> cached_map_fileId_filename;
            private readonly DirectoryRecord dirRecords;

            private MangaInfoFromArchive(string archivePath, IArchive archive, in ArchiveMangaInfoReader reader) : base(reader)
            {
                this.currentArchivePath = archivePath;
                this.currentArchive = archive;
                this.cached_entries = reader.cached_entries;
                this.cached_entry_header = reader.cached_entry_header;
                this.cached_map_fileId_filename = reader.cached_map_fileId_filename;
                this.dirRecords = reader.dirRecords;
            }

            public override bool TryGetImageContent(in Guid fileId, [NotNullWhen(true)] out Stream? contentStream)
            {
                if (fileId == Guid.Empty) throw new ArgumentException(null, nameof(fileId));
                
                if (this.cached_entries != null)
                {
                    if (this.cached_entries.TryGetValue(fileId, out var wrapperStream))
                    {
                        contentStream = wrapperStream;
                        return true;
                    }
                }
                else
                {
                    static bool MakeResult(IArchiveEntry entry, [NotNullWhen(true)] out Stream? contentStream)
                    {
                        var entryStream = entry.OpenEntryStream();
                        if (entryStream.CanSeek)
                        {
                            contentStream = entryStream;
                        }
                        else
                        {
                            using (entryStream)
                            {
                                var memStream = Form1.memMgr.GetStream(null, entry.Size);
                                entryStream.CopyTo(memStream, 4096 * 4);
                                memStream.Position = 0;
                                contentStream = memStream;
                            }
                        }
                        return true;
                    }

                    if (this.cached_entry_header != null)
                    {
                        if (this.cached_entry_header.TryGetValue(fileId, out var entryInfo))
                        {
                            return MakeResult(entryInfo, out contentStream);
                        }
                    }
                    else if (this.cached_map_fileId_filename.TryGetValue(fileId, out var filename))
                    {
                        var comparer = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                        var spanFilename = filename.Span;
                        foreach (var entry in this.currentArchive.Entries)
                        {
                            if (spanFilename.Equals(entry.Key, comparer))
                            {
                                return MakeResult(entry, out contentStream);
                            }
                        }
                    }
                }

                contentStream = null;
                return false;
            }

            public override void Dispose()
            {
                this.currentArchive?.Dispose();
                if (this.cached_entries != null)
                {
                    foreach (var streamwrapper in this.cached_entries.Values)
                    {
                        streamwrapper.BaseStream.Dispose();
                    }
                }
                this.dirRecords.ChildDirectories.Clear();
                this.dirRecords.ChildFiles.Clear();
            }

            public override bool TryGetFilename(in Guid fileId, out ReadOnlyMemory<char> filename) => this.cached_map_fileId_filename.TryGetValue(fileId, out filename);

            sealed class DirectoryRecord
            {
                public readonly ReadOnlyMemory<char> DirectoryName;
                public readonly ReadOnlyMemory<char> DirectoryPath;
                public readonly Dictionary<ReadOnlyMemory<char>, DirectoryRecord> ChildDirectories;
                public readonly Dictionary<ReadOnlyMemory<char>, Guid> ChildFiles;

                public bool HasChildDirectories => (this.ChildDirectories.Count != 0);
                public bool HasChildFiles => (this.ChildFiles.Count != 0);
                public bool HasAnyChildren => (this.HasChildDirectories || this.HasChildFiles);

                public DirectoryRecord(ReadOnlyMemory<char> directoryName) : this(directoryName, ReadOnlyMemory<char>.Empty) { }

                private DirectoryRecord(ReadOnlyMemory<char> directoryName, ReadOnlyMemory<char> directoryPath)
                {
                    this.DirectoryName = directoryName;
                    this.DirectoryPath = directoryPath;
                    this.ChildDirectories = new Dictionary<ReadOnlyMemory<char>, DirectoryRecord>(OperatingSystem.IsWindows() ? MemoryOfCharEqualityComparer.OrdinalIgnoreCase : MemoryOfCharEqualityComparer.Ordinal);
                    this.ChildFiles = new Dictionary<ReadOnlyMemory<char>, Guid>();
                }

                public DirectoryRecord GetOrSet(ReadOnlyMemory<char> directoryName)
                {
                    if (this.ChildDirectories.TryGetValue(directoryName, out var record)) return record;
                    record = new DirectoryRecord(directoryName, Path.Join(this.DirectoryPath.Span, directoryName.Span).AsMemory());
                    this.ChildDirectories.Add(directoryName, record);
                    return record;
                }

                public DirectoryRecord ParseFromPath(ReadOnlyMemory<char> path)
                {
                    var current = this;
                    foreach (var directoryName in WalkThroughDirectorySeparators(path))
                    {
                        current = current.GetOrSet(directoryName);
                    }
                    return current;
                }

                private static IEnumerable<ReadOnlyMemory<char>> WalkThroughDirectorySeparators(ReadOnlyMemory<char> mem_EntryName)
                {
                    if (mem_EntryName.IsEmpty) yield break;
                    int index;
                    while ((index = mem_EntryName.Span.IndexOfAny(Form1.directorySeparatorChars)) != -1)
                    {
                        yield return mem_EntryName.Slice(0, index);
                        if (index < (mem_EntryName.Length - 1))
                            mem_EntryName = mem_EntryName.Slice(index + 1);
                        else
                            break;
                    }
                    if (index < (mem_EntryName.Length - 1)) yield return mem_EntryName;
                }
            }

            readonly struct ArchiveMangaInfoReader : IMangaInfoParser
            {
                private readonly string archivePath;
                private readonly IArchive archive;
                private readonly bool isSeekable;

                public readonly ImmutableDictionary<Guid, NotClosingStream<RecyclableMemoryStream>>? cached_entries;
                public readonly ImmutableDictionary<Guid, IArchiveEntry>? cached_entry_header;
                public readonly ImmutableDictionary<Guid, ReadOnlyMemory<char>> cached_map_fileId_filename;
                public readonly DirectoryRecord dirRecords;

                static ReadOnlyMemory<char> GetDirectoryPathInArchive(ReadOnlyMemory<char> mem)
                {
                    var lastIndex = mem.Span.LastIndexOfAny(Form1.directorySeparatorChars);
                    if (lastIndex == -1) return ReadOnlyMemory<char>.Empty;
                    return mem.Slice(0, lastIndex);
                }

                public ArchiveMangaInfoReader(string archivePath, IArchive archive, DirectoryRecord dirRecords)
                {
                    this.archive = archive;
                    this.archivePath = archivePath;
                    this.isSeekable = false; // !archive.IsSolid;
                    this.dirRecords = dirRecords;

                    var map_fileId_filename = new Dictionary<Guid, ReadOnlyMemory<char>>();

                    if (this.isSeekable)
                    {
                        this.cached_entries = null;
                        var cached_entry_header_builder = new Dictionary<Guid, IArchiveEntry>();
                        foreach (var currentEntry in this.archive.Entries)
                        {
                            if (!currentEntry.IsDirectory)
                            {
                                var entryName = currentEntry.Key;
                                if (IsImageFile(entryName))
                                {
                                    var fileId = Guid.NewGuid();
                                    cached_entry_header_builder.Add(fileId, currentEntry);

                                    var mem_EntryName = entryName.AsMemory();
                                    map_fileId_filename.Add(fileId, mem_EntryName);

                                    var filename = GetFilenameAsMemory(mem_EntryName);
                                    var directoryPath = GetDirectoryPathInArchive(mem_EntryName);
                                    
                                    if (directoryPath.IsEmpty)
                                    {
                                        dirRecords.ChildFiles.Add(filename, fileId);
                                    }
                                    else
                                    {
                                        dirRecords.ParseFromPath(directoryPath).ChildFiles.Add(filename, fileId);
                                    }
                                }
                            }
                        }
                        this.cached_entry_header = cached_entry_header_builder.ToImmutableDictionary(cached_entry_header_builder.Comparer);
                    }
                    else
                    {
                        this.cached_entry_header = null;
                        var entries = new Dictionary<Guid, NotClosingStream<RecyclableMemoryStream>>();
                        using (var reader = archive.ExtractAllEntries())
                        {
                            while (reader.MoveToNextEntry())
                            {
                                var currentEntry = reader.Entry;
                                if (!currentEntry.IsDirectory)
                                {
                                    var entryName = currentEntry.Key;
                                    if (IsImageFile(entryName))
                                    {
                                        var fileId = Guid.NewGuid();
                                        var mem_EntryName = entryName.AsMemory();

                                        map_fileId_filename.Add(fileId, mem_EntryName);
                                        var filename = GetFilenameAsMemory(mem_EntryName);
                                        var directoryPath = GetDirectoryPathInArchive(mem_EntryName);

                                        var memStream = Form1.memMgr.GetStream(null, reader.Entry.Size);
                                        reader.WriteEntryTo(memStream);

                                        entries.Add(fileId, new NotClosingStream<RecyclableMemoryStream>(memStream));

                                        if (directoryPath.IsEmpty)
                                        {
                                            dirRecords.ChildFiles.Add(filename, fileId);
                                        }
                                        else
                                        {
                                            dirRecords.ParseFromPath(directoryPath).ChildFiles.Add(filename, fileId);
                                        }
                                    }
                                }
                            }
                        }
                        
                        this.cached_entries = entries.ToImmutableDictionary(entries.Comparer);
                    }

                    this.cached_map_fileId_filename = map_fileId_filename.ToImmutableDictionary();
                }

                static IEnumerable<IMangaChapterParser> RecursiveWithQuestionablePerf_Buffered(DirectoryRecord record)
                {
                    if (record.HasChildFiles)
                    {
                        yield return new BufferedMangaChapterParser(record, record.DirectoryPath);
                    }
                    if (record.HasChildDirectories)
                    {
                        foreach (var childChapter in record.ChildDirectories.Values)
                        {
                            foreach (var childChapterPageParser in RecursiveWithQuestionablePerf_Buffered(childChapter))
                            {
                                yield return childChapterPageParser;
                            }
                        }
                    }
                }

                public readonly IEnumerable<IMangaChapterParser> BeginReadChapters() => RecursiveWithQuestionablePerf_Buffered(this.dirRecords);

                public readonly void Dispose() { }

                public readonly ReadOnlySpan<char> GetMangaName()
                    => Path.GetFileName(this.archivePath.AsSpan());
            }

            readonly struct BufferedMangaChapterParser : IMangaChapterParser
            {
                private readonly ReadOnlyMemory<char> chapterName;
                private readonly DirectoryRecord dirRecord;

                public BufferedMangaChapterParser(DirectoryRecord dirRecord, in ReadOnlyMemory<char> chapterName)
                {
                    this.dirRecord = dirRecord;
                    this.chapterName = chapterName;
                }

                public IEnumerable<KeyValuePair<ReadOnlyMemory<char>, Guid>> BeginReadPages() => this.dirRecord.ChildFiles;

                public void Dispose() { }

                public ReadOnlyMemory<char> GetChapterName() => this.chapterName;
            }

        }
    }
}
