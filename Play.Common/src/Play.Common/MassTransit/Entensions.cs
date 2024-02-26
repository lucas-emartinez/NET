using System;
using System.Reflection;
using GreenPipes;
using MassTransit;
using MassTransit.Definition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Play.Common.Settings;

namespace Play.Common.MassTransit
{
    public static class Extensions
    {
        public static IServiceCollection AddMassTransitWithRabbitMq(this IServiceCollection services)
        {
            var configuration = services.BuildServiceProvider().GetService<IConfiguration>(); // Obtiene la configuración del servicio

            // MASS TRANSIT CONFIGURATION 
            services.AddMassTransit(configure =>
            {

                // AGREGO CONSUMIDORES 
                // Agrega todos los consumidores en el ensamblado de entrada
                // Los consumidores son clases que implementan la interfaz IConsumer
                // UN ASSEMBLY ES UNA COLECCIÓN DE CLASES Y TIPOS EN UNA APLICACIÓN .NET
                // Assembly.GetEntryAssembly() devuelve el ensamblado de entrada de la aplicación
                configure.AddConsumers(Assembly.GetEntryAssembly());

                // Configuración de RabbitMQ para el servicio 
                configure.UsingRabbitMq((context, configurator) =>
                {

                    var configuration = context.GetService<IConfiguration>();
                    var serviceSettings = configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();


                    var rabbitMQSettings = configuration.GetSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>();
                    configurator.Host(rabbitMQSettings.Host); // Configuración del host de RabbitMQ

                    // Configuración de los intentos de reenvío de mensajes y el intervalo entre intentos
                    configurator.UseMessageRetry(retryConfigurator =>
                    {
                        retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                    });

                    // Configuración de los endpoints de RabbitMQ para el servicio
                    // Se usa el nombre del servicio para el nombre del endpoint
                    // Se usa el formato KebabCase para el nombre del endpoint
                    configurator.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(serviceSettings.ServiceName, false));
                });
            });

            services.AddMassTransitHostedService(); // Habilita RabbitMQ para ser usado como servicio

            return services;
        }
    }
}