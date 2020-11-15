using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileTransfer;
using Grpc.Core;
using System.IO;

namespace GrpcServer.Services
{
	public class FileTransferService : FileTransfer.Files.FilesBase 
	{
		public override async Task<StartUploadResponse> StartUpload(IAsyncStreamReader<StartUploadRequest> requestStream, ServerCallContext context)
		{
			await requestStream.MoveNext(context.CancellationToken);

			EnsureFileMetadaWasSent(requestStream);

			var downloadFolder = Path.Combine(Path.GetTempPath(), "FileTransfer", "FileUpload");

			var fileMetadata = requestStream.Current.FileMetadata;
			var fileName = fileMetadata.FileName;

			var downloadName = $"{fileName}.{Guid.NewGuid()}.part";
			Directory.CreateDirectory(downloadFolder);
			var downloadNameAndPath = Path.Combine(downloadFolder, downloadName);

			await CreateDownloadFile(requestStream, context, downloadNameAndPath);

			RenameFileToFileName(downloadFolder, fileName, downloadNameAndPath);

			CleanUpDownloadFilesFromPreviousTrys(downloadFolder, fileName);

			return new StartUploadResponse();
		}

		private async Task CreateDownloadFile(IAsyncStreamReader<StartUploadRequest> requestStream, ServerCallContext context, string downloadNameAndPath)
		{
			try
			{
				await using (FileStream output = File.Open(downloadNameAndPath, FileMode.CreateNew, FileAccess.Write, FileShare.Delete))
				{
					while (await requestStream.MoveNext(context.CancellationToken))
					{
						EnsureFilePayloadWasSent(requestStream);

						FilePayload payload = requestStream.Current.FilePayload;

						await output.WriteAsync(payload.Chunk.ToByteArray(), context.CancellationToken);
					}
				}
			}
			catch (Exception)
			{
				File.Delete(downloadNameAndPath);
			}
		}

		private void RenameFileToFileName(string downloadFolder, string fileName, string downloadNameAndPath)
		{
			try
			{
				var fileNameAndPath = Path.Combine(downloadFolder, fileName);
				File.Move(downloadNameAndPath, fileNameAndPath);
			}
			catch(Exception exception)
			{
				throw new RpcException(new Status(StatusCode.AlreadyExists, $"The file '{fileName}' was already uploaded"), exception.ToString());
			}
		}

		private void EnsureFilePayloadWasSent(IAsyncStreamReader<StartUploadRequest> requestStream)
		{
			StartUploadRequest.UploadRequestOneofCase requesttype = requestStream.Current.UploadRequestCase;

			if (requesttype == StartUploadRequest.UploadRequestOneofCase.FileMetadata)
			{
				throw new RpcException(new Status(StatusCode.InvalidArgument, "File metadata is was alread sent. Only payload is accepted"));
			}
		}

		private void EnsureFileMetadaWasSent(IAsyncStreamReader<StartUploadRequest> requestStream)
		{
			StartUploadRequest.UploadRequestOneofCase requestStartType = requestStream.Current.UploadRequestCase;

			if (requestStartType != StartUploadRequest.UploadRequestOneofCase.FileMetadata)
			{
				throw new RpcException(new Status(StatusCode.InvalidArgument, "File metadata is missing. Send message of type 'FileMetadata' first."));
			}
		}

		private void CleanUpDownloadFilesFromPreviousTrys(string downloadFolder, string fileName)
		{
			foreach (string file in Directory.GetFiles(downloadFolder, $"{fileName}.*.part"))
			{
				File.Delete(file);
			}
		}
	}
}
