using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZipArchivExtensions
{
    public class ParallelReadableZipArchive : IDisposable
    {
        private readonly ConcurrentQueue<ZipArchive> zipArchiveReaders = new ConcurrentQueue<ZipArchive>();
        private readonly string zipArchivePathAndFileName;

        public ParallelReadableZipArchive(string zipArchivePathAndFileName)
        {
            this.zipArchivePathAndFileName = zipArchivePathAndFileName;
            zipArchiveReaders.Enqueue(CreateNewZipArchiveReader(this.zipArchivePathAndFileName));
        }

        internal ParallelReadableZipArchiveReader GetFreeZipArchiveReader()
        {
			if (!zipArchiveReaders.TryDequeue(out ZipArchive freeZipArchiveReader))
			{
				freeZipArchiveReader = CreateNewZipArchiveReader(zipArchivePathAndFileName);
			}

			return new ParallelReadableZipArchiveReader(this, freeZipArchiveReader);
        }

        internal void ReturnZipArchiveReader(ZipArchive zipArchive)
        {
            zipArchiveReaders.Enqueue(zipArchive);
        }

        private ZipArchive CreateNewZipArchiveReader(string zipArchivePathAndFileName)
        {
            return ZipFile.Open(zipArchivePathAndFileName, ZipArchiveMode.Read);
        }

        public ReadOnlyCollection<ParallelReadableZipArchiveEntry> Entries
        {
            get
            {
                List<ParallelReadableZipArchiveEntry> list;
                using (var zipArchivReader = GetFreeZipArchiveReader())
                {
                    var entries = zipArchivReader.ZipArchive.Entries;
                    list = new List<ParallelReadableZipArchiveEntry>(entries.Count);
                    foreach (var entry in entries)
                    {
                        list.Add(new ParallelReadableZipArchiveEntry(entry, this));
                    }
                }
 
                return new ReadOnlyCollection<ParallelReadableZipArchiveEntry>(list);
            }
        }

        public void Dispose()
        {
            foreach (var archive in zipArchiveReaders)
            {
                archive.Dispose();
            }
        }
    }
}
