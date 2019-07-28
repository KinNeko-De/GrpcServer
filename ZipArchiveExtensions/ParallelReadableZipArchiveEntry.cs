using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZipArchivExtensions
{
    public class ParallelReadableZipArchiveEntry
    {
        private ParallelReadableZipArchive zipArchive;
        public string FullName { get; private set; }
        public DateTimeOffset LastWriteTime { get; private set; }

        public ParallelReadableZipArchiveEntry(ZipArchiveEntry zipArchiveEntry, ParallelReadableZipArchive parallelReadableZipArchive)
        {
            this.zipArchive = parallelReadableZipArchive;
            this.FullName = zipArchiveEntry.FullName;
            this.LastWriteTime = zipArchiveEntry.LastWriteTime;
        }

        public void ExtractToFile(string path)
        {
            DoOnEntry((zipArchiveEntry) => { zipArchiveEntry.ExtractToFile(path); });
        }

        public void ExtractToFile(string path, bool overwrite)
        {
            DoOnEntry((zipArchiveEntry) => { zipArchiveEntry.ExtractToFile(path, overwrite); });
        }

        public void DoOnEntry(Action<ZipArchiveEntry> action)
        {
            using (ParallelReadableZipArchiveReader freeReader = zipArchive.GetFreeZipArchiveReader())
            {
                ZipArchiveEntry zipArchiveEntry = freeReader.GetEntry(FullName);
                action(zipArchiveEntry);
            }
        }
    }
}
