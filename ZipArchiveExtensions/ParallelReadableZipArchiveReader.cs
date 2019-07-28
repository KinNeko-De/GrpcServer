using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZipArchivExtensions
{
    /// <summary>
    /// Wraps the zipArchive and ensure that it is being returned after using
    /// </summary>
    public class ParallelReadableZipArchiveReader : IDisposable
    {
        public ParallelReadableZipArchiveReader(ParallelReadableZipArchive parallelReadableZipArchive, ZipArchive zipArchive)
        {
            this.parallelReadableZipArchive = parallelReadableZipArchive;
            ZipArchive = zipArchive;
        }

        public ZipArchiveEntry GetEntry(string fullName)
        {
            return ZipArchive.GetEntry(fullName);
        }

        #region IDisposable Support
        private bool disposedValue = false;
        private readonly ParallelReadableZipArchive parallelReadableZipArchive;

        public ZipArchive ZipArchive { get; }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    parallelReadableZipArchive.ReturnZipArchiveReader(ZipArchive);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
