using BookStore.Api.Extensions;
using BookStore.Api.Middlewares;
using BookStore.Api.Modules.Books;
using BookStore.Api.Modules.AuditEvents;
using BookStore.Api.Modules.Loans;
using BookStore.Api.Modules.Users;
using BookStore.Api.Services;
using BookStore.Application;
using BookStore.Application.Common.Interfaces;
using BookStore.Application.Common.Options;
using BookStore.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplicationServices();
builder.Services.AddOptions<CatalogCacheOptions>()
    .Bind(builder.Configuration.GetSection(CatalogCacheOptions.SectionName))
    .Validate(options => options.TimeToLiveSeconds > 0, "O TTL do catálogo deve ser positivo.")
    .Validate(options => options.OperationTimeoutMilliseconds > 0, "O timeout do cache deve ser positivo.")
    .Validate(_ => !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Redis")),
        "Configure ConnectionStrings:Redis.")
    .ValidateOnStart();
builder.Services.AddOptions<PaginationOptions>()
    .Bind(builder.Configuration.GetSection(PaginationOptions.SectionName))
    .Validate(options => options.DefaultPageSize is >= 1 and <= 100,
        "O tamanho padrão da página deve estar entre 1 e 100.")
    .ValidateOnStart();
builder.Services.AddOptions<LoanOptions>()
    .Bind(builder.Configuration.GetSection(LoanOptions.SectionName))
    .Validate(options => options.DurationDays > 0, "O prazo de empréstimo deve ser maior que zero.")
    .ValidateOnStart();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddStructuredLogging(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, RequestContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.Title = "BookStore API";
        options.Theme = ScalarTheme.Alternate;
        options.DefaultHttpClient = new KeyValuePair<ScalarTarget, ScalarClient>(
            ScalarTarget.CSharp,
            ScalarClient.HttpClient);
    }).AllowAnonymous();
}

app.UseStructuredRequestLogging();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseStatusCodePages();

await app.ApplyDatabaseMigrationsAsync();

app.MapHealthChecks("/health");
app.MapBookEndpoints();
app.MapUserEndpoints();
app.MapLoanEndpoints();
app.MapAuditEventEndpoints();
app.Run();
