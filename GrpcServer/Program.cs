using System;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace GrpcServer
{
	public class Program
	{
		public static int Main(string[] args)
		{
			SetSerilogDefaultLogger();

			try
			{
				Log.Information("Starting application: 'grpc_server'");
				CreateHostBuilder(args).Build().Run();
				return 0;
			}
			catch (Exception ex)
			{
				Log.Fatal(ex, "application 'grpc_server' terminated unexpectedly");
				return 1;
			}
			finally
			{
				Log.CloseAndFlush();
			}
		}

		/// <summary>
		///     Creates a default logger that is only used until the application configuration was loaded.
		/// </summary>
		private static void SetSerilogDefaultLogger()
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.CreateLogger();
		}

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder
						.UseStartup<Startup>()
						.UseUrls() // UseKestrel is used instead, you can not override the application url
						.UseKestrel(options =>
						{
							options.ListenAnyIP(5000, listenOptions =>
							{
								listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
							});
							options.ListenAnyIP(5001, listenOptions =>
							{
								listenOptions.Protocols = HttpProtocols.Http2;
								listenOptions.UseHttps();
							});
							options.ListenAnyIP(5002, listenOptions =>
							{
								listenOptions.Protocols = HttpProtocols.Http2;
							});
						})
						.UseSerilog((hostingContext, loggerConfiguration) =>
						{
							// format: "/GrpcPackageName.GrpcServiceName"
							string[] excludedRequestPaths = { "/TimeInformation.TimeInformation" };

							loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration)
								.Enrich.WithProperty("AssemblyVersion", Assembly.GetExecutingAssembly().GetName().Version)
								.Filter.ByExcluding(
									logEvent => FilterByRequestPath(
										logEvent,
										excludedRequestPaths,
										LogEventLevel.Warning
									)
								);
						});
				});
		}

		private static bool FilterByRequestPath(LogEvent logEvent, string[] requestPathStartsWith, LogEventLevel minimumLevel)
		{
			if (logEvent.Exception == null && logEvent.Level < minimumLevel)
			{
				if (logEvent.Properties.TryGetValue("RequestPath", out LogEventPropertyValue? requestPathValue))
				{
					string? requestPath = (requestPathValue as ScalarValue)?.Value?.ToString();
					if (requestPath != null)
					{
						foreach (string filter in requestPathStartsWith)
						{
							if (requestPath.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
							{
								return true;
							}
						}
					}
				}
			}

			return false;
		}
	}
}