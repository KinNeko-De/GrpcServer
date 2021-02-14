using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcServer.Domain.Errors
{
	public class IWantToDoSomething
	{
		public const string Hacker = "Hacker";
		public const string Anonymous = "Anonymous";
		public const string YouCanComeIn = "YouCanComeIn";
		public const string YouCanComeInAlso = "YouCanComeInAlso";
		public const string Password = "Password";

		public Task Do(string user, string password)
		{
			switch (user)
			{
				case Hacker:
					throw new YouCanNotComeInException("Access denied.", user);
				case Anonymous:
					throw new YouCanNotComeInException("Not logged in.", user);
				case YouCanComeIn:
				case YouCanComeInAlso:
					if (password != Password)
					{
						throw new YouCanNotComeInException("Access denied.", user);
					}
					return Task.CompletedTask;
				default:
					throw new YouCanNotComeInException("Access denied.", user);
			}
		}

		public class YouCanNotComeInException : Exception
		{
			public string Reason { get; }
			public string User { get; }

			public YouCanNotComeInException(string reason, string user) : base(reason)
			{
				User = user;
				Reason = reason;
			}
		}
	}
}
