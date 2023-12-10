using Hangfire;
using Hangfire.Initialization;
using System.ComponentModel.DataAnnotations;
using Hangfire.Initialization;
using Hangfire.AspNetCore;
using Hangfire.Client;
using Hangfire.Server;
using Hangfire.Common;

namespace Jobs.Api;

public static class Extn
{
    public static IEndpointConventionBuilder MapHangfireTenantDashboard(this IEndpointRouteBuilder endpoints, 
        string route = "/hangfire", 
        Func<HttpContext, Task<DashboardOptions>> options = null,
        Func<HttpContext, Task<JobStorage>> storage = null)
    {
        var requestHandler = endpoints.CreateApplicationBuilder().UseHangfireTenantDashboard(route, options, storage).Build();
        return endpoints.Map(route + "/{**path}", requestHandler);
    }

    public static IApplicationBuilder UseHangfireTenantDashboard(
        this IApplicationBuilder app,
        string pathMatch = "/hangfire",
        Func<HttpContext, Task<DashboardOptions>> options = null,
        Func<HttpContext, Task<JobStorage>> storage = null)
    {
        return app.Use(async (context, next) =>
        {
            options = options ?? (context2 => Task.FromResult(context2.RequestServices.GetService<DashboardOptions>()));
            storage = storage ?? (context2 => Task.FromResult(context2.RequestServices.GetService<JobStorage>()));

            var jobStorage = await storage(context);
            var dashboardOptions = await options(context);

            var middleware = app.New();

            if (jobStorage != null && jobStorage.GetType() != typeof(NoopJobStorage))
            {
                middleware.UseHangfireDashboard(pathMatch, dashboardOptions, jobStorage);
            }
            middleware.Run(async context2 => await next());

            await middleware.Build().Invoke(context);
        });
    }

}

public class WorkerService : BackgroundService
{
    Dictionary<string, TenantOptions> _tenants;
    IServiceScopeFactory _serviceScopeFactory;
    public WorkerService(TenantContextManager contextManager, IServiceScopeFactory serviceScopeFactory)
    {
        _tenants = contextManager.Tenants;
        _serviceScopeFactory = serviceScopeFactory;
    }   
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();

        var srv1 = HangfireLauncher.StartHangfireServer(new BackgroundJobServerOptions() { ServerName = "hf1" }, _tenants["hf1"].JobStorage, opts => {
            opts.Activator = new JobContextActivator(_serviceScopeFactory, new ServiceCollection());
            opts.FilterProvider = new JobFilterProvider();
        });
        _tenants["hf1"].ServerDetails = srv1;
        
        tasks.Add(srv1.WaitForShutdownAsync(stoppingToken));

        var srv2 = HangfireLauncher.StartHangfireServer(new BackgroundJobServerOptions() { ServerName = "hf2" }, _tenants["hf2"].JobStorage, opts => {
            opts.Activator = new JobContextActivator(_serviceScopeFactory, new ServiceCollection());
            opts.FilterProvider = new JobFilterProvider();
        });
        _tenants["hf2"].ServerDetails = srv2;
        tasks.Add(srv2.WaitForShutdownAsync(stoppingToken));    
        
        return Task.WhenAll(tasks);
    }
}

public class TenantContextManager
{
    public Dictionary<string, TenantOptions> Tenants { get; } = new Dictionary<string, TenantOptions>();
    public TenantContextManager()
    {
        var strg1 = HangfireJobStorage.GetJobStorage("Data Source=172.16.50.4;User ID=sa;Initial Catalog=hf1db;Password=[PW4dev!!];Persist Security Info=True;TrustServerCertificate=true");
        var strg2 = HangfireJobStorage.GetJobStorage("Data Source=172.16.50.4;User ID=sa;Initial Catalog=hf2db;Password=[PW4dev!!];Persist Security Info=True;TrustServerCertificate=true");

        Tenants.Add("hf1", new TenantOptions() { DashboardOptions = new DashboardOptions() { DashboardTitle = "HF1" }, JobStorage = strg1.Item1 });
        Tenants.Add("hf2", new TenantOptions() { DashboardOptions = new DashboardOptions() { DashboardTitle = "HF2" }, JobStorage = strg2.Item1 });
    }
}

public class TenantOptions
{ 
    public DashboardOptions DashboardOptions { get; set; }
    public JobStorage JobStorage { get; set; }
    public HangfireServerDetails ServerDetails { get; set; }
}

public interface IJob
{
    string Id { get; }
    string Name { get; }
    Task Execute();
}

public class ConsoleJob : IJob
{
    public string Id => Guid.NewGuid().ToString();

    public string Name => "BackgroundJob";

    public Task Execute()
    {
        Console.WriteLine("{1}-{0}:{2}",this.Id, this.Name, DateTime.Now.ToString());
        return Task.CompletedTask;
    }
}

public class JobClientFilter : IClientFilter
{
    public void OnCreated(CreatedContext context)
    {
        Console.WriteLine("OnCreated", context.BackgroundJob.Id);
    }

    public void OnCreating(CreatingContext context)
    {
        Console.WriteLine("OnCreating");
    }
}

public class JobFilterProvider : IJobFilterProvider
{
    public IEnumerable<JobFilter> GetFilters(Job job)
    {
        return new List<JobFilter>() { 
            new JobFilter(new JobServerFilter(), JobFilterScope.Global, null),
            new JobFilter(new JobClientFilter(), JobFilterScope.Global, null)
        };
    }
}

public class JobServerFilter: IServerFilter
{
    public void OnPerforming(PerformingContext context)
    {
        Console.WriteLine("OnPerforming");
    }

    public void OnPerformed(PerformedContext context)
    {
        Console.WriteLine("OnPerformed");
    }
}

public class JobContextActivator : AspNetCoreJobActivator
{
    private IServiceScopeFactory _serviceScopeFactory;
    public JobContextActivator(IServiceScopeFactory serviceScopeFactory, IServiceCollection services)
        : base(serviceScopeFactory)
    { 
        _serviceScopeFactory = serviceScopeFactory;
    }

    public override JobActivatorScope BeginScope(JobActivatorContext context)
    {
        return base.BeginScope(context);
    }
}