using Consul;
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

WebApplication app = builder.Build();

var lifetime = app.Lifetime;
var logger = app.Logger;
var consulClient = new ConsulClient();


var result = await consulClient.Agent.Checks();
var checks = result.Response;
checks.Values.ToList().FindAll(u => u.Status == HealthStatus.Critical).ForEach(async item =>
{
    await consulClient.Agent.ServiceDeregister(item.ServiceID);
});


var hostName = Dns.GetHostName();
string instanceId = hostName + ":" + Guid.NewGuid().ToString();
lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation("ApplicationStarted");

    var ip = Dns.GetHostEntry(hostName).AddressList.First(u => u.AddressFamily == AddressFamily.InterNetwork);

    int port = new Uri(app.Urls.First()).Port;

    var service = new AgentServiceRegistration()
    {
        ID = instanceId,
        Name = "AspNetCoreWebApplication",
        Address = $"http://{ip}",
        Port = port,
        Check = new AgentServiceCheck
        {
            HTTP = $"http://{ip}:{port}/WeatherForecast",
            Interval = new TimeSpan(0, 0, 2)
        },
    };
    consulClient.Agent.ServiceRegister(service).GetAwaiter().GetResult();
});

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("ApplicationStopping");
    consulClient.Agent.ServiceDeregister(instanceId).GetAwaiter().GetResult();
});

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();
