using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpcservices;

namespace GrpcServer.Services
{
	public class BlubberService : Grpcservices.BlubberService.BlubberServiceBase
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
