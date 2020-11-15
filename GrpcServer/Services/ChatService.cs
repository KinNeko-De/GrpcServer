using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcServer.Services
{
	public class ChatService : Chat.ChatBase
	{
		// low budget singleton
		static ConcurrentDictionary<Guid, IServerStreamWriter<ChatMessagesResponse>> allChatClients = new ConcurrentDictionary<Guid, IServerStreamWriter<ChatMessagesResponse>>();

		public override async Task SendMessages(IAsyncStreamReader<ChatMessagesRequest> requestStream, IServerStreamWriter<ChatMessagesResponse> responseStream, ServerCallContext context)
		{
			await requestStream.MoveNext(context.CancellationToken);
			var firstRequest = requestStream.Current;
			if (firstRequest.MessagesCase != ChatMessagesRequest.MessagesOneofCase.NewUser)
			{
				throw new RpcException(new Status(StatusCode.InvalidArgument, "Login first."));
			}

			var newUserRequest = firstRequest.NewUser;
			var clientId = Guid.Parse(newUserRequest.Id);

			allChatClients.GetOrAdd(clientId, responseStream);

			try
			{
				while (await requestStream.MoveNext(context.CancellationToken))
				{
					var request = requestStream.Current;
					if (request.MessagesCase == ChatMessagesRequest.MessagesOneofCase.ChatMessage)
					{
						// make this responsive so that you can not only chat with yourself :)
						var chatRequest = request.ChatMessage;
						var response = new ChatMessagesResponse()
						{
							ChatMessage = new ChatMessageResponse()
							{
								Id = chatRequest.Id,
								Message = chatRequest.Message,
								UserName = newUserRequest.Name
							}
						};
						await responseStream.WriteAsync(response);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// TODO send user logged out message
			}
			finally
			{
				allChatClients.TryRemove(clientId, out _);
			}
		}
	}
}
