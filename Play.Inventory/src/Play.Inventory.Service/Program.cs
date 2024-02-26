using MassTransit;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var services = builder.Services;

// AGREGO SERVICIOS DE MONGO Y REPOSITORIO
services.addMongo()
        .AddMongoRepository<InventoryItem>("inventoryItems")
        .AddMongoRepository<CatalogItem>("catalogItems")
        .AddMassTransitWithRabbitMq();

AddCatalogClient(services);

// AGREGO CONTROLADORES
services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});

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

static void AddCatalogClient(IServiceCollection services)
{
    // JITTER, ALEATOREIDAD PARA LOS REINTENTOS PARA QUE NO SE SATURAN LOS SERVICIOS
    Random jitterer = new Random();

    // SERVICIOS INTERNOS
    services.AddHttpClient<CatalogClient>(client =>
    {
        client.BaseAddress = new Uri("https://localhost:5001");
    }) // POLITICA DE TIMEOUT Y REINTENTO CON TIEMPO EXPONENCIAL
    .AddTransientHttpErrorPolicy(builder => builder.Or<TimeoutRejectedException>().WaitAndRetryAsync(
        5,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                        + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)),
        onRetry: (outcome, timespan, retryAttempt) =>
        {
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<ILogger<CatalogClient>>()?
                .LogWarning($"Delayinh for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}");
        }
    )) // POLITICA DE CIRCUIT BREAKER, 3 INTENTOS
    .AddTransientHttpErrorPolicy(builder => builder.Or<TimeoutRejectedException>().CircuitBreakerAsync(
        3,
        TimeSpan.FromSeconds(15),
        onBreak: (outcome, timespan) =>
        {
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<ILogger<CatalogClient>>()?
                .LogWarning($"Circuit breaker opened for {timespan.TotalSeconds} seconds");
        },
        onHalfOpen: () =>
        {
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<ILogger<CatalogClient>>()?
                .LogWarning($"Circuit breaker half-opened");
        },
        onReset: () =>
        {
            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetService<ILogger<CatalogClient>>()?
                .LogWarning($"Circuit breaker reset");
        }
    ))
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1)); // POLITICA DE TIMEOUT, CUANDO FALLA, GENERA UN TimeOutRejectedException Y SE CONTINUA CON EL REINTENTO
}