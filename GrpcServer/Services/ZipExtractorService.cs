using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpcservices;
using ZipArchivExtensions;

namespace GrpcServer.Services
{
	public class ZipExtractorService : Grpcservices.ZipExtractorService.ZipExtractorServiceBase
	{
		public async override Task Extract(IAsyncStreamReader<ExtractFileRequest> requestStream, IServerStreamWriter<ExtractFileResponse> responseStream, ServerCallContext context)
		{
			while (await requestStream.MoveNext())
			{
				var request = requestStream.Current;
				var zipArchive = new ParallelReadableZipArchive(request.FilePathAndName);
				var zipArchiveEntries = zipArchive.Entries;
				var items = zipArchiveEntries.Count;

				ConcurrentStack<int> progressStack = new ConcurrentStack<int>();
				IProgress<int> progress = CreateProgress(progressStack);


				int itemFinished = 0;
				var responseTask = Task.Run(() => ReportProgress(request, responseStream, progressStack, items));

				await Task.Run(() => Parallel.ForEach(zipArchiveEntries, zipArchiveEntry =>
				{
					var targetFile = Path.Combine(request.TargetPath, zipArchiveEntry.FullName);
					string? targetDirectory = Path.GetDirectoryName(targetFile);
					if (targetDirectory != null)
					{
						Directory.CreateDirectory(targetDirectory);
					}
					zipArchiveEntry.ExtractToFile(targetFile, true);
					
					var result = Interlocked.Increment(ref itemFinished);
					progress.Report(result);
				}));

				await responseTask;

				await responseStream.WriteAsync(new ExtractFileResponse
				{
					Id = request.Id,
					Finished = true,
					Items = items,
					ItemsInstalled = items
				});
			}

			Console.WriteLine("Ended correctly");
		}

		private IProgress<int> CreateProgress(ConcurrentStack<int> progressStack)
		{
			Progress<int> progress = new Progress<int>((itemFinished) => progressStack.Push(itemFinished));
			return progress;
		}
		
		private async Task ReportProgress(ExtractFileRequest request, IServerStreamWriter<ExtractFileResponse> responseStream, ConcurrentStack<int> progressStack, int items)
		{
			int lastReport = 0;
			int lastReported = -1;
			while(lastReport < items)
			{
				progressStack.TryPeek(out lastReport);
				
				if(lastReport != lastReported)
				{
					await responseStream.WriteAsync(new ExtractFileResponse
					{
						Id = request.Id,
						Finished = false,
						Items = items,
						ItemsInstalled = lastReport
					});
					lastReported = lastReport;
				}

				await Task.Delay(1000);
			}
		}
	}
}
