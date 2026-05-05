using Serilog;
using AssistantApi.Infrastructure.OllamaClient;
using AssistantApi.Infrastructure.QdrantClient;
using AssistantApi.Services;
using AssistantApi.Services.DocumentValidation;
using AssistantApi.Services.Interfaces;

// Инициализация базового логгера Serilog до запуска хоста для отслеживания ошибок старта
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Настройка Serilog как основного провайдера логирования
    builder.Host.UseSerilog((ctx, _, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .WriteTo.Console());

    // Регистрация контроллеров и HTTP-клиента в DI-контейнере
    builder.Services.AddControllers();
    builder.Services.AddHttpClient();
    builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
    builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
    builder.Services.Configure<DocumentValidationOptions>(builder.Configuration.GetSection(DocumentValidationOptions.SectionName));
    // Ollama generation can take a long time on CPU; increase HttpClient timeout to avoid 502s
    builder.Services.AddHttpClient<ILlmService, OllamaHttpClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(300);
    });
    builder.Services.AddHttpClient<IRagService, QdrantHttpClient>();
    builder.Services.AddScoped<IDocumentValidationService, DocumentValidationService>();
    builder.Services.AddSingleton<IChatSessionStore, ChatSessionStore>();

    // Настройка CORS для взаимодействия с фронтендом (ChatUI)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowChatUI", policy =>
            policy.WithOrigins(
                      builder.Configuration["Cors:ChatUiOrigin"] ?? "http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    }); 

    var app = builder.Build();

    // Применение настроек middleware: CORS, авторизация и роутинг контроллеров
    app.UseCors("AllowChatUI");
    app.UseAuthorization();
    app.MapControllers();

    // Запуск приложения
    app.Run();
}
catch (Exception ex)
{
    // Фиксация критических ошибок при запуске
    Log.Fatal(ex, "Ошибка запуска приложения");
}
finally
{
    // Гарантированное закрытие логов и освобождение ресурсов
    Log.CloseAndFlush();
}
