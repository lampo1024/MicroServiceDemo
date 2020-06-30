/**************************************
 * FileName:	ConsulStartupExtension.cs
 * Author:		Rector
 * Date:		2020-6-29	
 * Copyright (c) 2020
 * Desc:		
***************************************/
using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace MicroServiceDemo.ProductService.Extensions
{
    public static class ServiceDiscoveryExtension
    {
        public static IServiceCollection AddConsulConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
            {
                var address = configuration.GetValue<string>("ConsulConfig:Host");
                consulConfig.Address = new Uri(address);
            }));

            return services;
        }

        public static IApplicationBuilder UseConsul(this IApplicationBuilder app, IConfiguration configuration)
        {
            var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();
            var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("AppExtensions");
            var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();

            if (!(app.Properties["server.Features"] is FeatureCollection features))
            {
                return app;
            }

            //var addresses = features.Get<IServerAddressesFeature>();
            var addresses = app.ServerFeatures.Get<IServerAddressesFeature>().Addresses;
            //if (addresses.Addresses.Count == 0)
            //{
            //    ListenOn(DefaultAddress); // Start the server on the default address
            //    addresses.Addresses.Add(DefaultAddress) // Add the default address to the IServerAddressesFeature
            //}
            //var address = "http://localhost:5000"; //addresses.First();

            var ip = configuration["ip"];
            var port = configuration["port"];

            var address = $"http://{ip}:{port}"; //addresses.First();

            Console.WriteLine($"address={address}");

            var serviceName = configuration.GetValue<string>("ConsulConfig:ServiceName");
            var serviceId = configuration.GetValue<string>("ConsulConfig:ServiceId");
            var uri = new Uri(address);

            var registration = new AgentServiceRegistration()
            {
                ID = $"{serviceName}-{port}",
                Name = serviceName,
                Address = $"{uri.Host}",
                Port = uri.Port,
                Check = new AgentCheckRegistration
                {
                    HTTP = $"{uri.Scheme}://{uri.Host}:{uri.Port}/api/health",
                    Notes = "Checks /health on localhost",
                    Timeout = TimeSpan.FromSeconds(3),
                    Interval = TimeSpan.FromSeconds(10)
                },
                Tags = new[] { "Product" }
            };

            logger.LogInformation("Registering with Consul");
            consulClient.Agent.ServiceDeregister(registration.ID).ConfigureAwait(true);
            consulClient.Agent.ServiceRegister(registration).ConfigureAwait(true);

            lifetime.ApplicationStopping.Register(() =>
            {
                logger.LogInformation("Unregistering from Consul");
                consulClient.Agent.ServiceDeregister(registration.ID).ConfigureAwait(true);
            });

            return app;
        }
    }
}
