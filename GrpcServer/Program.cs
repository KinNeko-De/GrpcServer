using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace GrpcServer
{
	public class Program
	{
		public static void Main(string[] args)
		{
			AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>()
					.UseUrls()
					.UseKestrel(options =>
					{				
						options.ListenAnyIP(5000, listenOptions => { listenOptions.Protocols = HttpProtocols.Http1AndHttp2; });
						options.ListenAnyIP(5001, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; listenOptions.UseHttps(); });
						options.ListenAnyIP(5002, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
					});
				});
		}
			
	}
}
