using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpcservices;
using Microsoft.Extensions.Logging;

namespace GrpcServer.Services
{
	public class ErrorService : Grpcservices.ErrorService.ErrorServiceBase
	{
		private readonly ILogger<ErrorService> logger;

		public ErrorService(
			ILogger<ErrorService> logger
		)
		{
			this.logger = logger;
		}

		public override Task<ErrorResponse> IDoNotHandleErrorCorrectly(ErrorRequest request, ServerCallContext context)
		{
			throw new Exception("I will result in a status code UNKNOWN. I also will not be logged I only will logged information under 'Grpc.AspNetCore.Server.ServerCallHandler'.");
		}

		public override Task<ErrorResponse> GiveMeADetailedError(ErrorRequest request, ServerCallContext context)
		{
			var innerException = new Exception("I am inside") {Data = {{"exceptionKey", "exceptionvalue"}}};
			var metaData = new Metadata();
			foreach (KeyValuePair<object, object> entry in innerException.Data)
			{
				var metaDataKey = entry.Key?.ToString();
				if (entry.Value is IMessage protobufMessage)
				{
					try
					{
						metaData.Add(metaDataKey, protobufMessage.ToByteArray());
					}
					catch (ArgumentException argumentException)
					{
						logger.LogWarning("Key {metaDataKey} is not a valid grpc meta data key.", argumentException, metaDataKey);
					}
				}
				else
				{
					try
					{
						metaData.Add(metaDataKey, entry.Value?.ToString());
					}
					catch (ArgumentException argumentException)
					{
						logger.LogWarning("Key {metaDataKey} is not a valid grpc meta data key.", argumentException, metaDataKey);
					}
				}
			}

			throw new RpcException(new Status(StatusCode.Internal, "I will be displayed to the client.", innerException), innerException.ToString());
		}
	}
}