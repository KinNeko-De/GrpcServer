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
		static ConcurrentDictionary<Guid, IServerStreamWriter<ChatReply>> allChatClients = new ConcurrentDictionary<Guid, IServerStreamWriter<ChatReply>>();

		public override async Task Chat(IAsyncStreamReader<ChatRequest> requestStream, IServerStreamWriter<ChatReply> responseStream, ServerCallContext context)
		{
			var clientId = Guid.NewGuid();
			allChatClients.GetOrAdd(clientId, responseStream);

			while (await requestStream.MoveNext())
			{
				var chatRequest = requestStream.Current;
				string message = $"({DateTime.Now:hh:mm}){chatRequest.ClientName}: {chatRequest.Message}";

				// remember what was said to eventual consistency.. await that the sender see his message
				await responseStream.WriteAsync(new ChatReply() { Message = message });

				BroadCastMessage(message, clientId);
			}

			allChatClients.TryRemove(clientId, out IServerStreamWriter<ChatReply> finishedClient);
		}

		private void BroadCastMessage(string message, Guid clientId)
		{
			// Discard Task.. "new" Feature https://blogs.msdn.microsoft.com/mazhou/2017/06/27/c-7-series-part-4-discards/
			// Ignores a nullreference exception if value is null
			// this happpens when a client disconnects without complete the request
			// https://docs.microsoft.com/de-de/dotnet/csharp/discards
			_ = Task.Run(async () =>
		  {
			  List<Task> allTasks = new List<Task>();
			  foreach (var client in allChatClients.Where(x => x.Key != clientId))
			  {
				
				allTasks.Add(client.Value.WriteAsync(new ChatReply() { Message = message }));
			  }
			  // nicht waitall!
			  await Task.WhenAll(allTasks);
		  });

		}
	}
}
