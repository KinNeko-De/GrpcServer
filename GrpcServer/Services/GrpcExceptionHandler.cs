using System;
using System.Globalization;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrpcServer.Services
{
	public class GrpcExceptionHandler
	{
		private readonly GrpcConfig grpcOptions;

		public GrpcExceptionHandler(IOptions<GrpcConfig> grpcOptions)
		{
			this.grpcOptions = grpcOptions.Value;
		}

		/// <summary>
		///     Ensures that every exception is logged.
		///     Also adds an unique error id.
		/// </summary>
		/// <remarks>If you want to add additional property to the log use Microsoft.Extensions.Logging.BeginScope().
		/// If you would use the grpc logging interceptor you would lose the possible to log with scope.</remarks>
		/// <typeparam name="T">The responseType</typeparam>
		/// <param name="action">service code to execute</param>
		/// <param name="context">context to identify the method</param>
		/// <param name="logger">logger of the service class</param>
		/// <returns></returns>
		public async Task<T> HandleExceptions<T>(Func<Task<T>> action, ServerCallContext context, ILogger logger)
		{
			try
			{
				return await action();
			}
			catch (Exception exception)
			{
				var errorMessageExtension = grpcOptions.EnabledDetailedErrors ? $" {exception}" : string.Empty;
				var errorId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

				var errorMessage = $"Error when executing service method {{Method}}. ErrorId is '{{ErrorId}}'.{errorMessageExtension}";
				logger.LogError(exception, errorMessage, context.Method, errorId);

				RpcException rpcExceptionToThrow;
				switch (exception)
				{
					case RpcException rpcException:
						rpcExceptionToThrow = 
							grpcOptions.EnabledDetailedErrors 
								? new RpcException(
									new Status(rpcException.StatusCode, $"{rpcException.Status.Detail}{errorMessageExtension}", rpcException.Status.DebugException),
									rpcException.Trailers,
									$"{rpcException}{errorMessageExtension}") 
								: rpcException;
						break;
					default:
						rpcExceptionToThrow = new RpcException(
							new Status(StatusCode.Internal, $"Error when executing service method '{context.Method}'. ErrorId is '{errorId}'.{errorMessageExtension}", exception),
							exception.ToString());
						break;
				}

				throw rpcExceptionToThrow;
			}
		}

		public Metadata CreateTrailer<T>(T error) where T : IMessage
		{
			var metaData = new Metadata();
			var metaDataKey = nameof(T).ToLower();
			try
			{
				metaData.Add(metaDataKey, error.ToByteArray());
			}
			catch (ArgumentException argumentException)
			{
				throw new InvalidOperationException($"Key '{metaDataKey}' is not a valid grpc meta data key.", argumentException);
			}

			return metaData;
		}
	}
}