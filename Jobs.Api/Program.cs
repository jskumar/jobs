using Hangfire;
using Jobs.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddHangfire((serviceProvider, config) =>
{
    config.UseFilter(new JobClientFilter());
    config.UseFilter(new JobServerFilter());
    config.UseActivator(ActivatorUtilities.CreateInstance<JobContextActivator>(serviceProvider, builder.Services));
});

builder.Services.AddSingleton<TenantContextManager>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped(sp => {
    var ctxt = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var contextManager = sp.GetRequiredService<TenantContextManager>();

    var tid = ctxt.Request.Query["tid"].ToString();
    return contextManager.Tenants[tid];
});
builder.Services.AddScoped(sp => {
    var tOpts = sp.GetRequiredService<TenantOptions>();
    return tOpts.DashboardOptions;
});
builder.Services.AddScoped(sp => {
    var tOpts = sp.GetRequiredService<TenantOptions>();
    return tOpts.JobStorage;
});

builder.Services.AddHostedService<WorkerService>();
builder.Services.AddScoped<IJob, ConsoleJob>();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseEndpoints(ep =>
{
    ep.MapControllers();
    ep.MapHangfireTenantDashboard("/hangfire");
});
app.Run();
