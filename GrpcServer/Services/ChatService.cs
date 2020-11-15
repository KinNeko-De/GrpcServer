using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace GrpcServer.Services
{
	public class ChatService : Chat.ChatBase
	{
		// low budget singleton
		static readonly Subject<ChatMessagesResponse> Pusher = new Subject<ChatMessagesResponse>();

		private readonly BlockingCollection<ChatMessagesResponse> outgoingMessages = new BlockingCollection<ChatMessagesResponse>();
		private IServerStreamWriter<ChatMessagesResponse> myResponseStream;

		public override async Task SendMessages(IAsyncStreamReader<ChatMessagesRequest> requestStream, IServerStreamWriter<ChatMessagesResponse> responseStream, ServerCallContext context)
		{
			await requestStream.MoveNext(context.CancellationToken);
			var firstRequest = requestStream.Current;
			if (firstRequest.MessagesCase != ChatMessagesRequest.MessagesOneofCase.NewUser)
			{
				throw new RpcException(new Status(StatusCode.InvalidArgument, "Login first."));
			}

			myResponseStream = responseStream;
			using var subscription = Pusher.Subscribe(OnNext);
			var _ = Task.Run(() => SendOutgoingMessage(context.CancellationToken));

			var newUserRequest = firstRequest.NewUser;

			try
			{
				while (await requestStream.MoveNext(context.CancellationToken))
				{
					var request = requestStream.Current;
					switch (request.MessagesCase)
					{
						case ChatMessagesRequest.MessagesOneofCase.ChatMessage:
							NewOutgoingMessage(request.ChatMessage, newUserRequest);
							break;
						default:
							// DoNothing
							break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// TODO send user logged out message
			}
			finally
			{
			}
		}

		private void OnNext(ChatMessagesResponse obj)
		{
			outgoingMessages.TryAdd(obj);
		}

		private void NewOutgoingMessage(ChatMessageRequest chatRequest, NewUserRequest newUserRequest)
		{
			ChatMessagesResponse response = new ChatMessagesResponse()
			{
				ChatMessage = new ChatMessageResponse()
				{
					Id = chatRequest.Id,
					Message = chatRequest.Message,
					UserName = newUserRequest.Name
				}
			};

			Pusher.OnNext(response);

			// outgoingMessages.TryAdd(response);
		}

		private async Task SendOutgoingMessage(CancellationToken cancellationToken)
		{
			var outgoing = outgoingMessages.GetConsumingEnumerable(cancellationToken);
			foreach (var response in outgoing)
			{
				await myResponseStream.WriteAsync(response);
			}
		}
	}
}
