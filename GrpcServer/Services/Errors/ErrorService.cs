using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Errors;
using Google.Protobuf;
using Grpc.Core;
using GrpcServer.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace GrpcServer.Services
{
	public class ErrorService : Errors.ErrorService.ErrorServiceBase
	{
		private readonly ILogger<ErrorService> logger;
		private readonly GrpcExceptionHandler grpcExceptionHandler;
		private readonly IWantToDoSomething iWantToDoSomething;

		public ErrorService(
			ILogger<ErrorService> logger,
			GrpcExceptionHandler grpcExceptionHandler,
			IWantToDoSomething iWantToDoSomething
		)
		{
			this.logger = logger;
			this.grpcExceptionHandler = grpcExceptionHandler;
			this.iWantToDoSomething = iWantToDoSomething;
		}

		public override Task<ErrorResponse> IDoNotHandleErrorCorrectly(ErrorRequest request, ServerCallContext context)
		{
			throw new Exception("I will result in a status code UNKNOWN. I also will be logged as error under 'Grpc.AspNetCore.Server.ServerCallHandler'.");
		}

		public override Task<ErrorResponse> IDoNotLogRpcExceptionCorrectly(ErrorRequest request, ServerCallContext context)
		{
			var rpcStatus = new Status(StatusCode.Internal, "I will result in the right rpc status code. But i only will only be logged as information under 'Grpc.AspNetCore.Server.ServerCallHandler'.");
			throw new RpcException(rpcStatus);
		}

		public override async Task<GiveMeADetailedErrorResponse> GiveMeADetailedError(GiveMeADetailedErrorRequest request, ServerCallContext context)
		{
			return await grpcExceptionHandler.HandleExceptions(async () =>
			{
				try
				{
					await iWantToDoSomething.Do(IWantToDoSomething.Anonymous, string.Empty);
				}
				// If you want to map exceptions to grpc status code then prefer tp do it in your service method
				catch (IWantToDoSomething.YouCanNotComeInException youCanNotComeInException)
				{
					throw new RpcException(
						new Status(StatusCode.PermissionDenied, youCanNotComeInException.Message, youCanNotComeInException),
						CreateErrorTrailers(youCanNotComeInException),
						youCanNotComeInException.ToString()
					);
				}

				return new GiveMeADetailedErrorResponse();
			}, context, logger);
		}

		private Metadata CreateErrorTrailers(IWantToDoSomething.YouCanNotComeInException youCanNotComeInException)
		{
			
			var error = new GiveMeADetailedErrorError()
			{
				PermissionDenied = new PermissionDeniedError()
				{
					User = youCanNotComeInException.User,
					Reason = youCanNotComeInException.Reason
				}
			};
			var metaData = grpcExceptionHandler.CreateTrailer(error);

			return metaData;
		}
	}
}