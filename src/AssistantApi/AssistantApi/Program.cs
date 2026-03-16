using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, _, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    builder.Services.AddControllers();
    builder.Services.AddHttpClient();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowChatUI", policy =>
            policy.WithOrigins(
                      builder.Configuration["Cors:ChatUiOrigin"] ?? "http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    var app = builder.Build();

    app.UseCors("AllowChatUI");
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
