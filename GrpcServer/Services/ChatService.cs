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
			if (firstRequest.MessagesCase != ChatMessagesRequest.MessagesOneofCase.UserLogin)
			{
				throw new RpcException(new Status(StatusCode.InvalidArgument, "Login first."));
			}

			myResponseStream = responseStream;
			var userLoginRequest = firstRequest.UserLogin;

			using var subscription = Pusher.Subscribe(OnNext);
			var sendingTask = Task.Run(() => SendOutgoingMessage(userLoginRequest.Id, context.CancellationToken));

			Pusher.OnNext(CreateUserLoginMessage(userLoginRequest));

			try
			{
				while (await requestStream.MoveNext(context.CancellationToken))
				{
					var request = requestStream.Current;
					switch (request.MessagesCase)
					{
						case ChatMessagesRequest.MessagesOneofCase.ChatMessage:
							NewOutgoingMessage(CreateChatMessage(request.ChatMessage, userLoginRequest));
							break;
						default:
							// DoNothing
							break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// It is ok
			}
			finally
			{
				NewOutgoingMessage(CreateUserLogoutMessage(userLoginRequest));

				await EndSendingMessages(subscription, sendingTask);
			}
		}

		private async Task EndSendingMessages(IDisposable subscription, Task sendingTask)
		{
			// dispose so that no new messages are added
			subscription.Dispose();

			// mark adding complete so that sending task finished
			outgoingMessages.CompleteAdding();
			await sendingTask;
		}

		private void OnNext(ChatMessagesResponse obj)
		{
			outgoingMessages.TryAdd(obj);
		}

		private ChatMessagesResponse CreateChatMessage(ChatMessage chatMessage, UserLogin userLogin)
		{
			ChatMessagesResponse response = new ChatMessagesResponse()
			{
				SendFromUserId = userLogin.Id,
				SendFromUserName = userLogin.Name,
				ChatMessage = new ChatMessage()
				{
					Id = chatMessage.Id,
					Message = chatMessage.Message
				}
			};
			return response;
		}

		private ChatMessagesResponse CreateUserLogoutMessage(UserLogin userLogin)
		{
			ChatMessagesResponse response = new ChatMessagesResponse()
			{
				SendFromUserId = userLogin.Id,
				UserLogout = new UserLogout()
				{
					Id = userLogin.Id,
					Name = userLogin.Name
				}
			};
			return response;
		}

		private ChatMessagesResponse CreateUserLoginMessage(UserLogin userLogin)
		{
			ChatMessagesResponse response = new ChatMessagesResponse()
			{
				SendFromUserId = userLogin.Id,
				UserLogin = new UserLogin()
				{
					Id = userLogin.Id,
					Name = userLogin.Name
				}
			};
			return response;
		}

		private void NewOutgoingMessage(ChatMessagesResponse chatMessagesResponse)
		{
			Pusher.OnNext(chatMessagesResponse);
		}

		private async Task SendOutgoingMessage(string myUserId, CancellationToken cancellationToken)
		{
			var outgoing = outgoingMessages.GetConsumingEnumerable(cancellationToken);
			foreach (var response in outgoing)
			{
				if(response.SendFromUserId != myUserId)
				{
					await myResponseStream.WriteAsync(response);
				}
			}
		}
	}
}
