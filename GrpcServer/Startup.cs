﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrpcServer
{
	public class Startup
	{
		private readonly IConfiguration configuration;

		public Startup(IConfiguration configuration)
		{
			this.configuration = configuration;
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddGrpc(options =>
			{
				// Displays the message of non rpc exceptions and newly also the message of inner exceptions to the client.
				// rpc exceptions details are not displayed
				// no stacktrace is displayed, so for production issues and debugging in containers without logging not useful
				// options.EnableDetailedErrors = true;
			});

			services.Configure<Services.GrpcConfig>(configuration.GetSection(nameof(Services.GrpcConfig)));
			services.AddTransient<Services.GrpcExceptionHandler>();
			services.AddTransient<Domain.Errors.IWantToDoSomething>();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseRouting();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapGrpcService<Services.BlubberService>();
				endpoints.MapGrpcService<Services.ChatService>();
				endpoints.MapGrpcService<Services.ErrorService>(); 
				endpoints.MapGrpcService<Services.TransferService>();
				endpoints.MapGrpcService<Services.TimeInformationService>();
				endpoints.MapGrpcService<Services.ZipExtractorService>();
				
			});
		}
	}
}
