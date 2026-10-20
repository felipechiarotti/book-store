using BookStore.Api.Extensions;
using BookStore.Api.Middlewares;
using BookStore.Api.Modules.Books;
using BookStore.Api.Services;
using BookStore.Application;
using BookStore.Application.Common.Interfaces;
using BookStore.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplicationServices();
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
app.Run();