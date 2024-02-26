
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Play.Catalog.Service.Entities;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Common.Settings;



var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

// Configuración de la base de datos MongoDB para el servicio
// Se usa el nombre de la colección para el nombre de la base de datos
// Agrego MassTransit con RabbitMQ
services.addMongo()
        .AddMongoRepository<Item>("items")
        .AddMassTransitWithRabbitMq();

services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});


services.AddEndpointsApiExplorer();
services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
