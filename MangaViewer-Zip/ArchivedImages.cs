using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Readers;
using System.IO;
using Microsoft.IO;
using System.Collections.Concurrent;
using SharpCompress.Common;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Buffers;

namespace MangaViewer_Zip
{
    class ArchivedImages : IAsyncDisposable
    {
        const int BigSizeConst = 1024 * 8;
        private int _state;
        private readonly BlockingCollection<KeyValuePair<string, MemoryStream>> _entries;
        private Task? duh;

        private static readonly RecyclableMemoryStreamManager MemMgr = new RecyclableMemoryStreamManager();

        public string Filename { get; }

        public ArchivedImages(string filename, int concurrentLevel)
        {
            this.Filename = filename;
            this._state = 0;

            this._entries = new BlockingCollection<KeyValuePair<string, MemoryStream>>(concurrentLevel);
        }

        public void Read()
        {
            if (Interlocked.CompareExchange(ref this._state, 1, 0) == 0)
            {
                this.duh = Task.Factory.StartNew((obj) =>
                {
                    using (var fs = File.OpenRead((string)obj))
                    // using (var reader = ReaderFactory.Open(fs))
                    // using (var reader = ReaderFactory.Open(fs))
                    {
                        IReader reader = null;
                        SharpCompress.Archives.IArchive archive = null;
                        try
                        {
                            reader = ReaderFactory.Open(fs);
                        }
                        catch
                        {
                            bool isStillError = false;
                            try
                            {
                                archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(fs);
                                reader = archive.ExtractAllEntries();
                            }
                            catch
                            {
                                isStillError = true;
                                reader?.Dispose();
                                archive?.Dispose();
                            }
                            if (isStillError)
                            {
                                isStillError = false;
                                try
                                {
                                    archive = SharpCompress.Archives.Rar.RarArchive.Open(fs);
                                    reader = archive.ExtractAllEntries();
                                }
                                catch
                                {
                                    isStillError = true;
                                    reader?.Dispose();
                                    archive?.Dispose();
                                }
                            }
                            if (isStillError)
                            {
                                throw;
                            }
                        }
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(BigSizeConst);
                        try
                        {
                            while (reader.MoveToNextEntry())
                            {
                                if (this._state == 1)
                                {
                                    if (!reader.Entry.IsDirectory)
                                    {
                                        try
                                        {
                                            var uncompressedSize = reader.Entry.Size;

                                            MemoryStream mem;
                                            if (uncompressedSize <= 0)
                                            {
                                                mem = MemMgr.GetStream(reader.Entry.Key);
                                            }
                                            else if (uncompressedSize <= int.MaxValue)
                                            {
                                                mem = MemMgr.GetStream(reader.Entry.Key, (int)uncompressedSize);
                                            }
                                            else
                                            {
                                                mem = MemMgr.GetStream(reader.Entry.Key);
                                                mem.SetLength(uncompressedSize);
                                            }
                                            mem.Position = 0;
                                            using (var entryStream = reader.OpenEntryStream())
                                            {
                                                var len = entryStream.Read(buffer, 0, buffer.Length);
                                                while (len > 0)
                                                {
                                                    mem.Write(buffer, 0, len);
                                                    len = entryStream.Read(buffer, 0, buffer.Length);
                                                }
                                                if (mem.Length == 0)
                                                {
                                                    mem.Dispose();
                                                }
                                                else
                                                {
                                                    mem.Position = 0;
                                                    this._entries.TryAdd(new KeyValuePair<string, MemoryStream>(reader.Entry.Key, mem), -1);
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                else
                                {
                                    reader.Cancel();
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                            if (!this._entries.IsAddingCompleted)
                            {
                                this._entries.CompleteAdding();
                            }
                            reader?.Dispose();
                            archive?.Dispose();
                        }
                    }
                }, this.Filename, TaskCreationOptions.LongRunning);
            }
        }

        public IEnumerable<KeyValuePair<string, MemoryStream>> GetConsumer() => this._entries.GetConsumingEnumerable();

        public async ValueTask DisposeAsync()
        {
            var oldState = Interlocked.Exchange(ref this._state, -1);
            if (oldState == 1)
            {
                if (this.duh != null)
                {
                    await this.duh;
                }
            }
            this._entries.Dispose();
        }
    }
}
