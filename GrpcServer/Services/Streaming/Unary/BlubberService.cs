using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Grpc.Core;
using Streaming.Unary;

namespace GrpcServer.Services
{
	public class BlubberService : Streaming.Unary.BlubberService.BlubberServiceBase
	{
		public override Task<GiveMeABlubReply> GiveMeABlub(GiveMeABlubRequest request, ServerCallContext context)
		{
			return Task.FromResult(new GiveMeABlubReply
			{
				Message = "Hello " + request.NameOfClient
			});
		}
	}
}
