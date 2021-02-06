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
using Microsoft.Extensions.Logging;

namespace GrpcServer.Services
{
	public class ChatService : Chat.ChatBase
	{
		private readonly ILogger<ChatService> logger;

		// low budget singleton
		// In some tutorials this is called PublishSubject; it should do a broadcast to all current subscriber
		static readonly Subject<ChatMessagesResponse> Pusher = new Subject<ChatMessagesResponse>();

		public ChatService(ILogger<ChatService> logger)
		{
			this.logger = logger;
		}

		public override async Task SendMessages(IAsyncStreamReader<ChatMessagesRequest> requestStream, IServerStreamWriter<ChatMessagesResponse> responseStream, ServerCallContext context)
		{
			// throw new RpcException(new Status(StatusCode.Internal, "i test it")); // is displayed as info
			// throw new Exception("another test"); // is displayed as fail and mapped to status code unknown
			UserLogin userLoginRequest = await GetUserLogin(requestStream, context);

			var observer = new ChatObserver(responseStream, logger);
			using var subscription = Pusher.Subscribe(observer);
			observer.StartSendingOutgoingMessages(userLoginRequest.Id, context.CancellationToken);

			// do this after subscription to ensure to get all 'i love you' messages
			Pusher.OnNext(CreateUserLoginMessage(userLoginRequest));

			await Chatting(requestStream, context, userLoginRequest, observer, subscription);
		}

		private async Task Chatting(IAsyncStreamReader<ChatMessagesRequest> requestStream, ServerCallContext context, UserLogin userLoginRequest, ChatObserver observer, IDisposable subscription)
		{
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
							// DoNothing, changing name is not supported yet
							break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// It is ok.. i call it on client side even it it not needed
			}
			finally
			{
				EndReceivingMessages(subscription);

				// do this after because you should not see all the 'i hate him but i am too shy to tell him' messages
				NewOutgoingMessage(CreateUserLogoutMessage(userLoginRequest));

				await EndSendingMessages(observer);
			}
		}

		private static async Task<UserLogin> GetUserLogin(IAsyncStreamReader<ChatMessagesRequest> requestStream, ServerCallContext context)
		{
			await requestStream.MoveNext(context.CancellationToken);
			var firstRequest = requestStream.Current;
			if (firstRequest.MessagesCase != ChatMessagesRequest.MessagesOneofCase.UserLogin)
			{
				throw new RpcException(new Status(StatusCode.InvalidArgument, "Login first."));
			}
			var userLoginRequest = firstRequest.UserLogin;
			return userLoginRequest;
		}

		private void EndReceivingMessages(IDisposable subscription)
		{
			// dispose so that no new messages are added
			subscription.Dispose();
		}

		private async Task EndSendingMessages(ChatObserver observer)
		{
			await observer.StopSendingOutgoingMessages();
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

		private class ChatObserver : IObserver<ChatMessagesResponse>
		{
			private readonly BlockingCollection<ChatMessagesResponse> outgoingMessages = new BlockingCollection<ChatMessagesResponse>();
			private IServerStreamWriter<ChatMessagesResponse> myResponseStream;
			private readonly ILogger logger;
			Task? sendingTask;

			public ChatObserver(IServerStreamWriter<ChatMessagesResponse> responseStream, ILogger logger)
			{
				this.myResponseStream = responseStream;
				this.logger = logger;
			}

			public void StartSendingOutgoingMessages(string userId, CancellationToken cancellationToken) 
			{
				sendingTask = Task.Run(() => SendOutgoingMessage(userId, cancellationToken), cancellationToken);
			}

			private async Task SendOutgoingMessage(string myUserId, CancellationToken cancellationToken)
			{
				var outgoing = outgoingMessages.GetConsumingEnumerable(cancellationToken);
				foreach (var response in outgoing)
				{
					if (response.SendFromUserId != myUserId)
					{
						await myResponseStream.WriteAsync(response);
					}
				}
			}

			public async Task StopSendingOutgoingMessages()
			{
				outgoingMessages.CompleteAdding();
				if (sendingTask != null)
				{
					await sendingTask;
				}
			}

			public void OnCompleted()
			{
				// Never happen unless server gracefully shutdown which is not implemented yet.
			}

			public void OnError(Exception exception)
			{
				logger.LogError(exception, "Exception occurred.");
			}

			public void OnNext(ChatMessagesResponse value)
			{
				outgoingMessages.Add(value);
			}
		}
	}
}
