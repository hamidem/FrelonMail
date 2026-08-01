using System.Text.Json.Serialization;
using Frelon.Core;
using Frelon.Mail;
using Frelon.Storage;
using Frelon.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;

if (IsolatedEmailAnalysis.IsWorkerInvocation(args))
{
    Environment.ExitCode = await IsolatedEmailAnalysis
        .RunWorkerAsync(Console.OpenStandardInput(), Console.OpenStandardOutput())
        .ConfigureAwait(false);
    return;
}

const long MaxEmailSize = EmailAnalysisLimits.DefaultMaxSourceBytes;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var applicationIdentity = ApplicationIdentity.FromAssembly(typeof(Program).Assembly);

var preferredPort = builder.Configuration.GetValue<int?>("Frelon:Port") ?? 5127;
var openBrowser = builder.Configuration.GetValue<bool?>("Frelon:OpenBrowser")
    ?? PackagedApplicationDefaults.OpenBrowser;
var dataDirectory = builder.Configuration["Frelon:DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frelon");
}
dataDirectory = Path.GetFullPath(dataDirectory);

using var applicationInstance = LocalApplicationInstance.TryAcquire(dataDirectory);
if (applicationInstance is null)
{
    var activeUrl = LocalApplicationInstance.TryReadActiveUrl(dataDirectory)
        ?? new Uri($"http://localhost:{preferredPort}");
    Console.WriteLine($"Frelon est déjà démarré : {activeUrl}");
    if (openBrowser)
    {
        LocalBrowserLauncher.TryOpen(activeUrl);
    }

    return;
}

var port = LocalPortSelector.SelectAvailable(preferredPort);
var localUrl = new Uri($"http://localhost:{port}");
applicationInstance.PublishActiveUrl(localUrl);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(port);
    options.Limits.MaxRequestBodySize = MaxEmailSize;
});

builder.Services.Configure<JsonOptions>(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(
        namingPolicy: null,
        allowIntegerValues: false)));

var databasePath = Path.Combine(dataDirectory, "incidents.db");
var sqliteStore = SqliteIncidentStore.FromFile(databasePath);
builder.Services.AddSingleton(sqliteStore);
builder.Services.AddSingleton<IIncidentStore>(services =>
    services.GetRequiredService<SqliteIncidentStore>());
builder.Services.AddSingleton<IIncidentReviewStore>(services =>
    services.GetRequiredService<SqliteIncidentStore>());
builder.Services.AddSingleton<ICampaignReviewStore>(services =>
    services.GetRequiredService<SqliteIncidentStore>());
builder.Services.AddSingleton<IIncidentCorrelator, BasicIncidentCorrelator>();
builder.Services.AddSingleton<ICampaignCorrelationService, LocalCampaignCorrelationService>();
builder.Services.AddSingleton<ICampaignConsultationService, LocalCampaignConsultationService>();
builder.Services.AddSingleton<ICampaignReviewService, LocalCampaignReviewService>();
builder.Services.AddSingleton(IsolatedEmailAnalysis.CreateAnalyzer());
builder.Services.AddSingleton<LocalIncidentWorkspace>();
builder.Services.AddSingleton<LocalCampaignWorkspace>();
builder.Services.AddSingleton(IncidentExportService.CreateDefault());
builder.Services.AddSingleton<LocalApplicationControl>();
builder.Services.AddSingleton(applicationIdentity);

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine($"{applicationIdentity.ProductName} {applicationIdentity.Version} est prêt : {localUrl}");
    Console.WriteLine("Fermez cette fenêtre ou utilisez Ctrl+C pour arrêter Frelon.");
    if (port != preferredPort)
    {
        Console.WriteLine($"Le port {preferredPort} était occupé ; le port local {port} a été choisi.");
    }

    if (openBrowser && !LocalBrowserLauncher.TryOpen(localUrl))
    {
        Console.WriteLine("Le navigateur n'a pas pu être ouvert automatiquement. Utilisez l'adresse ci-dessus.");
    }
});

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode =
        exception is BadHttpRequestException
            or InvalidDataException
            or EmailAnalysisLimitException
            or EmailAnalysisTimeoutException
            ? 400
            : 500;
    await Results.Problem(
            statusCode: context.Response.StatusCode,
            title: context.Response.StatusCode == 400
                ? "Le fichier transmis est invalide."
                : "L'opération locale a échoué.")
        .ExecuteAsync(context);
}));

app.Use(async (context, next) =>
{
    LocalHttpSecurityPolicy.ApplyResponseHeaders(context.Response.Headers);
    if (!LocalHttpSecurityPolicy.IsAllowedRequest(context, port))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new
            {
                message = "Cette interface est accessible uniquement depuis l'application locale Frelon."
            },
            context.RequestAborted);
        return;
    }

    await next(context);
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/application/info", (
    HttpResponse response,
    ApplicationIdentity identity) =>
{
    response.Headers.CacheControl = "no-store";
    return Results.Ok(identity);
});

app.MapGet("/api/application/session", (
    HttpResponse response,
    LocalApplicationControl applicationControl) =>
{
    response.Headers.CacheControl = "no-store";
    return Results.Ok(new
    {
        shutdownToken = applicationControl.ShutdownToken
    });
});

app.MapPost("/api/application/shutdown", (
    HttpRequest request,
    HttpResponse response,
    LocalApplicationControl applicationControl,
    IHostApplicationLifetime applicationLifetime) =>
{
    var candidate = request.Headers["X-Frelon-Shutdown-Token"].ToString();
    if (!applicationControl.IsShutdownAuthorized(candidate))
    {
        return Results.Unauthorized();
    }

    response.OnCompleted(() =>
    {
        applicationLifetime.StopApplication();
        return Task.CompletedTask;
    });

    return Results.Ok(new
    {
        message = "Frelon s'arrête proprement."
    });
});

app.MapGet("/api/incidents", async (
    LocalIncidentWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    var incidents = await workspace.ListRecentAsync(25, cancellationToken);
    return Results.Ok(incidents);
});

app.MapGet("/api/incidents/{incidentId:guid}", async (
    Guid incidentId,
    LocalIncidentWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    var incident = await workspace.GetByIdAsync(incidentId, cancellationToken);
    return incident is null
        ? Results.NotFound()
        : Results.Ok(IncidentPresentation.FromIncident(incident));
});

app.MapGet("/api/incidents/{incidentId:guid}/exports/{format}", async (
    Guid incidentId,
    string format,
    LocalIncidentWorkspace workspace,
    IIncidentReviewStore reviewStore,
    IncidentExportService exportService,
    CancellationToken cancellationToken) =>
{
    var incident = await workspace.GetByIdAsync(incidentId, cancellationToken);
    if (incident is null)
    {
        return Results.NotFound();
    }

    IncidentExportArtifact? artifact;
    if (string.Equals(format, "bundle", StringComparison.Ordinal))
    {
        var review = await reviewStore.GetLatestReviewAsync(incidentId, cancellationToken);
        artifact = exportService.CreateBundle(incident, review);
    }
    else if (string.Equals(format, "review-json", StringComparison.Ordinal))
    {
        var review = await reviewStore.GetLatestReviewAsync(incidentId, cancellationToken);
        if (review is null)
        {
            return Results.NotFound();
        }

        artifact = exportService.CreateReview(review);
    }
    else if (string.Equals(format, "validated-report-markdown", StringComparison.Ordinal))
    {
        var review = await reviewStore.GetLatestReviewAsync(incidentId, cancellationToken);
        if (review is null || !exportService.TryCreateValidatedReport(incident, review, out artifact))
        {
            return Results.Conflict(new
            {
                message = "Le signalement exige une dernière décision humaine confirmant et catégorisant la fraude."
            });
        }
    }
    else if (!exportService.TryCreate(incident, format, out artifact))
    {
        return Results.BadRequest(new { message = "Ce format d'export n'est pas pris en charge." });
    }

    return Results.File(
        artifact.Content,
        artifact.ContentType,
        artifact.FileName,
        enableRangeProcessing: false);
});

app.MapGet("/api/incidents/{incidentId:guid}/reviews/latest", async (
    Guid incidentId,
    LocalIncidentWorkspace workspace,
    IIncidentReviewStore reviewStore,
    CancellationToken cancellationToken) =>
{
    if (await workspace.GetByIdAsync(incidentId, cancellationToken) is null)
    {
        return Results.NotFound();
    }

    var decision = await reviewStore.GetLatestReviewAsync(incidentId, cancellationToken);
    return decision is null ? Results.NoContent() : Results.Ok(decision);
});

app.MapGet("/api/incidents/{incidentId:guid}/reviews", async (
    Guid incidentId,
    int? limit,
    LocalIncidentWorkspace workspace,
    IIncidentReviewStore reviewStore,
    CancellationToken cancellationToken) =>
{
    if (limit is < 1 or > 100)
    {
        return Results.BadRequest(new { message = "La limite doit être comprise entre 1 et 100." });
    }

    if (await workspace.GetByIdAsync(incidentId, cancellationToken) is null)
    {
        return Results.NotFound();
    }

    var decisions = await reviewStore.ListReviewsAsync(
        incidentId,
        limit ?? 50,
        cancellationToken);
    return Results.Ok(decisions);
});

app.MapPost("/api/incidents/{incidentId:guid}/reviews", async (
    Guid incidentId,
    IncidentReviewRequest request,
    LocalIncidentWorkspace workspace,
    IIncidentReviewStore reviewStore,
    CancellationToken cancellationToken) =>
{
    if (await workspace.GetByIdAsync(incidentId, cancellationToken) is null)
    {
        return Results.NotFound();
    }

    if (request.Verdict is null)
    {
        return Results.BadRequest(new { message = "Une conclusion humaine explicite est obligatoire." });
    }

    IncidentReviewDecision decision;
    try
    {
        decision = new IncidentReviewDecision(
            Guid.NewGuid(),
            incidentId,
            request.Verdict.Value,
            request.Classification,
            DateTimeOffset.UtcNow,
            request.Notes);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }

    await reviewStore.SaveReviewAsync(decision, cancellationToken);
    return Results.Created(
        $"/api/incidents/{incidentId:D}/reviews/{decision.ReviewId:D}",
        decision);
});

app.MapGet("/api/campaigns", async (
    int? incidentLimit,
    LocalCampaignWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (incidentLimit is < 1 or > LocalCampaignConsultationService.MaximumLimit)
    {
        return Results.BadRequest(new
        {
            message = $"La limite doit être comprise entre 1 et {LocalCampaignConsultationService.MaximumLimit}."
        });
    }

    var campaigns = await workspace.ListCurrentAsync(
        incidentLimit ?? 100,
        cancellationToken);
    return Results.Ok(campaigns);
});

app.MapGet("/api/campaigns/{fingerprint}", async (
    string fingerprint,
    int? incidentLimit,
    int? reviewLimit,
    LocalCampaignWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!CampaignCandidate.IsValidFingerprint(fingerprint) ||
        incidentLimit is < 1 or > LocalCampaignConsultationService.MaximumLimit ||
        reviewLimit is < 1 or > LocalCampaignConsultationService.MaximumLimit)
    {
        return Results.BadRequest(new
        {
            message = "L'empreinte ou les limites de consultation sont invalides."
        });
    }

    var details = await workspace.GetDetailsAsync(
        fingerprint,
        incidentLimit ?? 100,
        reviewLimit ?? 50,
        cancellationToken);
    return details is null ? Results.NotFound() : Results.Ok(details);
});

app.MapPost("/api/campaigns/{fingerprint}/reviews", async (
    string fingerprint,
    CampaignReviewRequest request,
    LocalCampaignWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!CampaignCandidate.IsValidFingerprint(fingerprint) ||
        request.CandidateSnapshot is null ||
        request.Verdict is null ||
        !string.Equals(
            fingerprint,
            request.CandidateSnapshot.Fingerprint,
            StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new
        {
            message = "La campagne examinée et la décision transmise sont incomplètes ou incohérentes."
        });
    }

    CampaignReviewDecision decision;
    try
    {
        decision = new CampaignReviewDecision(
            Guid.NewGuid(),
            request.CandidateSnapshot,
            request.Verdict.Value,
            DateTimeOffset.UtcNow,
            request.Notes);

        await workspace.RecordCurrentAsync(decision, 100, cancellationToken);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { message = exception.Message });
    }

    return Results.Created(
        $"/api/campaigns/{decision.CandidateFingerprint}/reviews/{decision.ReviewId:D}",
        decision);
});

app.MapPost("/api/incidents/analyze", async (
    HttpRequest request,
    LocalIncidentWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (request.ContentLength is 0 or > MaxEmailSize)
    {
        return Results.BadRequest(new { message = "Le fichier doit faire entre 1 octet et 25 Mo." });
    }

    var fileValidation = EmailEvidenceFilePolicy.ValidateEncodedFileName(
        request.Headers["X-Frelon-Filename"].ToString());
    if (!fileValidation.IsAccepted)
    {
        return Results.BadRequest(new { message = fileValidation.Message });
    }

    var incident = await workspace.AnalyzeAndSaveAsync(
        request.Body,
        fileValidation.FileName!,
        cancellationToken);

    return Results.Ok(IncidentPresentation.FromIncident(incident));
});

app.MapFallbackToFile("index.html");
await app.RunAsync();

/// <summary>Point d'entrée de l'application locale Frelon.</summary>
public partial class Program;

/// <summary>Décision humaine transmise par le cockpit local.</summary>
public sealed record IncidentReviewRequest(
    ReviewVerdict? Verdict,
    FraudClassification? Classification,
    string? Notes);

/// <summary>Décision humaine portant sur le snapshot exact affiché dans le cockpit.</summary>
public sealed record CampaignReviewRequest(
    CampaignCandidate? CandidateSnapshot,
    CampaignReviewVerdict? Verdict,
    string? Notes);
