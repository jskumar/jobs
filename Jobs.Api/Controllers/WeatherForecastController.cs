using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Jobs.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    IBackgroundJobClient backgroundJobClient;
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, TenantOptions tenantOptions)
    {
        _logger = logger;
        backgroundJobClient = tenantOptions.ServerDetails.BackgroundJobClient;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        //backgroundJobClient.Enqueue(() => Console.WriteLine("Hello world from the API!" + DateTime.Now.ToString()));
        //backgroundJobClient.Enqueue(() => Console.WriteLine("Hello world from the API!" + DateTime.Now.ToString()));


        backgroundJobClient.Enqueue<IJob>(job => job.Execute());
        backgroundJobClient.Schedule<IJob>(job => job.Execute(), new TimeSpan(0,1,0));

        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
