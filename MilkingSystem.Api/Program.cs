using Autofac;
using Autofac.Extensions.DependencyInjection;
using MilkingSystem.Core.Services;
using MilkingSystem.Core.Notifications;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Autofac
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection connection string is missing.");

    // DataService is shared because it is stateless and uses the database connection string.
    containerBuilder.Register(_ => new DataService(connectionString))
        .AsSelf()
        .SingleInstance();

    // One shared notifier instance is required so that the in-memory
    // milking state and robot subscriptions are shared across requests.
    containerBuilder.RegisterType<InMemoryRobotNotifier>()
        .As<IRobotNotifier>()
        .SingleInstance();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }