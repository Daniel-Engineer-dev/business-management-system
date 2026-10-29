using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QTKD")
    ?? throw new InvalidOperationException("Connection string 'QTKD' was not configured.");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<QtkdDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
