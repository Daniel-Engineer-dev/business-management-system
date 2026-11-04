using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;
using QTKD.Api.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QTKD")
    ?? throw new InvalidOperationException("Connection string 'QTKD' was not configured.");

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<QtkdDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ContractSubmissionService>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
