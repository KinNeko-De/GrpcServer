using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpcservices;

namespace GrpcServer.Services
{
	public class TimeInformationService : Grpcservices.TimeInformationService.TimeInformationServiceBase
	{
		public override async Task TimePing(TimePingRequest request, IServerStreamWriter<TimePingResponse> responseStream, ServerCallContext context)
		{
			await responseStream.WriteAsync(new TimePingResponse() {Message = $"Hello {request.ClientName}. You are connected and will get a time information every 5 seconds from now", TimeNow = Timestamp.FromDateTimeOffset(DateTimeOffset.Now)});

			while (!context.CancellationToken.IsCancellationRequested)
			{
				await responseStream.WriteAsync(new TimePingResponse() {Message = $"Its now:", TimeNow = Timestamp.FromDateTimeOffset(DateTimeOffset.Now)}).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
			}
		}

		public override async Task TimePingWithGoodBye(IAsyncStreamReader<TimePingRequest> requestStream, IServerStreamWriter<TimePingResponse> responseStream, ServerCallContext context)
		{
			if (!await requestStream.MoveNext(CancellationToken.None))
			{
				return;
			}

			;

			var request = requestStream.Current;
			await responseStream.WriteAsync(new TimePingResponse() {Message = $"Hello {request.ClientName}. You are connected and will get a time information when ever you ask", TimeNow = Timestamp.FromDateTimeOffset(DateTimeOffset.Now)});

			while (await requestStream.MoveNext(CancellationToken.None))
			{
				request = requestStream.Current;
				if (!request.GoodBye)
				{
					await SendTime(request, responseStream);
				}
				else
				{
					await SendGoodBye(request, responseStream);
				}
			}
		}

		private static async Task SendGoodBye(TimePingRequest request, IServerStreamWriter<TimePingResponse> responseStream)
		{
			await responseStream.WriteAsync(new TimePingResponse() {Message = $"Bye {request.ClientName}!", TimeNow = Timestamp.FromDateTimeOffset(DateTimeOffset.Now)}).ConfigureAwait(false);
		}

		private static async Task SendTime(TimePingRequest request, IServerStreamWriter<TimePingResponse> responseStream)
		{
			await responseStream.WriteAsync(new TimePingResponse() {Message = $"Its now:", TimeNow = Timestamp.FromDateTimeOffset(DateTimeOffset.Now)}).ConfigureAwait(false);
		}
	}
}