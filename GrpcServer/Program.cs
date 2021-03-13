using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
// ReSharper disable once RedundantUsingDirective
using Serilog.Formatting.Compact;
// ReSharper disable once RedundantUsingDirective
using Serilog.Sinks.SystemConsole.Themes;

namespace GrpcServer
{
	public class Program
	{
		public static async Task<int> Main(string[] args)
		{

			SetSerilogDefaultLogger();
			try
			{
				Log.Information($"Starting application: '{AppConstants.Application}'.");
				await CreateHostBuilder(args).Build().RunAsync();
				return 0;
			}
			catch (Exception ex)
			{
				Log.Fatal(ex, $"Application '{AppConstants.Application}' terminated unexpectedly.");
				return 1;
			}
			finally
			{
				Log.Information($"Stopping application: '{AppConstants.Application}'");
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
				.Enrich.WithProperty("Application", AppConstants.Application)
				.Enrich.WithProperty("AssemblyVersion", Assembly.GetExecutingAssembly().GetName().Version)
#if DEBUG
				.WriteTo.Console(
					theme: AnsiConsoleTheme.Code,
					outputTemplate: "[{Timestamp:o}] [{Level:u3}] [{Application}] [{Message}] [{Exception}] [{Properties:j}] {NewLine}"
				)
#else
				.WriteTo.Console(new RenderedCompactJsonFormatter())
#endif
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
							// TODO Replace with diagnostic services
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