using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcServer.Services
{
	public class GrpcConfig
	{
		/// <summary>
		///     Prints out the exception that occurred with innerException and full stacktrace
		/// </summary>
		/// <remarks>Do not use this in production</remarks>
		/// <remarks>
		///     We do not use the grpc option 'EnabledDetailedErrors' because it only prints the message out.
		///     Newer versions also prints out the message of the inner exception.
		///     This does not include the stacktrace and for exceptions you should use ToString() in general.
		/// </remarks>
		public bool EnabledDetailedErrors { get; set; }
	}
}
