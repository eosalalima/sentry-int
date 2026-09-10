using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SentryIntegrated.Application.Gate;
using SentryIntegrated.Application.Messaging;
using SentryIntegrated.Application.Personnel;
using SentryIntegrated.Application.Turnstile;
using SentryIntegrated.Components;
using SentryIntegrated.Configuration;
using SentryIntegrated.Domain;
using SentryIntegrated.Infrastructure.Health;
using SentryIntegrated.Infrastructure.Persistence;
using SentryIntegrated.Infrastructure.Polling;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddOptions<SentryOptions>().BindConfiguration(SentryOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
var access = builder.Configuration.GetConnectionString("AccessControl") ?? throw new InvalidOperationException("ConnectionStrings:AccessControl is required.");
var staff = builder.Configuration.GetConnectionString("Staff") ?? throw new InvalidOperationException("ConnectionStrings:Staff is required.");
var students = builder.Configuration.GetConnectionString("Students") ?? throw new InvalidOperationException("ConnectionStrings:Students is required.");
builder.Services.AddPooledDbContextFactory<SentryDbContext>(o => o.UseSqlServer(access, x => x.EnableRetryOnFailure(3)));
builder.Services.AddPooledDbContextFactory<StaffDbContext>(o => o.UseSqlServer(staff, x => x.EnableRetryOnFailure(3)));
builder.Services.AddPooledDbContextFactory<StudentDbContext>(o => o.UseSqlServer(students, x => x.EnableRetryOnFailure(3)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DashboardState>(); builder.Services.AddSingleton<WorkerStatus>();
builder.Services.AddSingleton<IPersonnelResolver, PersonnelResolver>(); builder.Services.AddSingleton<IPhotoResolver, PhotoResolver>();
builder.Services.AddSingleton<IGateService, GateService>(); builder.Services.AddSingleton<SmsTemplateService>();
builder.Services.AddSingleton<SmsQueue>(); builder.Services.AddSingleton<ISmsQueue>(sp => sp.GetRequiredService<SmsQueue>());
builder.Services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SentryOptions>>().Value.Sms.Provider switch
{
    SmsProviderKind.Recording => ActivatorUtilities.CreateInstance<RecordingSmsSender>(sp),
    SmsProviderKind.GsmModem => ActivatorUtilities.CreateInstance<GsmModemSmsSender>(sp),
    _ => ActivatorUtilities.CreateInstance<DisabledSmsSender>(sp)
});
builder.Services.AddHostedService<TurnstilePollingWorker>(); builder.Services.AddHostedService<DemoEventWorker>(); builder.Services.AddHostedService<SmsWorker>();
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessCheck>("databases", tags: ["ready"]).AddCheck<PollingReadinessCheck>("polling", tags: ["ready"])
    .AddCheck<SmsReadinessCheck>("sms", tags: ["ready"]);

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error", createScopeForErrors: true); app.UseHsts(); }
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseAntiforgery(); app.UseAuthentication(); app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = x => x.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });
app.MapPost("/api/gate/events", async (GateEventRequest request, IGateService service, CancellationToken ct) =>
{
    try { return Results.Created($"/api/gate/events/{await service.InsertAsync(request, ct)}", null); }
    catch (GateValidationException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [ex.Message] }); }
}).RequireAuthorization();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Logger.LogInformation("Sentry Integrated starting in {Mode} mode", builder.Configuration["Sentry:Mode"] ?? "Live");
app.Run();

public partial class Program;
