using AssistantApi.Infrastructure.OllamaClient;
using AssistantApi.Infrastructure.QdrantClient;
using AssistantApi.Pipeline;
using AssistantApi.Pipeline.Steps;
using AssistantApi.Services;
using AssistantApi.Services.Interfaces;
using Serilog;

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

    // Настройка размера загружаемых файлов (10 МБ)
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
    });

    // Настройка CORS для взаимодействия с фронтендом (ChatUI)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowChatUI", policy =>
            policy.WithOrigins(
                      builder.Configuration["Cors:ChatUiOrigin"] ?? "http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    // Конфигурация Ollama и Qdrant
    builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
    builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));

    // Регистрация HTTP-клиентов для внешних сервисов
    builder.Services.AddHttpClient<ILlmService, OllamaHttpClient>();
    builder.Services.AddHttpClient<IRagService, QdrantHttpClient>();

    // Регистрация сервиса парсинга документов
    builder.Services.AddScoped<IDocumentParserService, DocumentParserService>();

    // Регистрация шагов пайплайна
    builder.Services.AddScoped<ClassificationStep>();
    builder.Services.AddScoped<ContextExtractionStep>();
    builder.Services.AddScoped<RagSearchStep>();
    builder.Services.AddScoped<PromptBuildStep>();
    builder.Services.AddScoped<LlmGenerationStep>();
    builder.Services.AddScoped<ExperimentLoggingStep>();

    // Регистрация пайплайна с упорядоченными шагами
    builder.Services.AddScoped<IEnumerable<IPipelineStep>>(sp => new IPipelineStep[]
    {
        sp.GetRequiredService<ClassificationStep>(),
        sp.GetRequiredService<ContextExtractionStep>(),
        sp.GetRequiredService<RagSearchStep>(),
        sp.GetRequiredService<PromptBuildStep>(),
        sp.GetRequiredService<LlmGenerationStep>(),
        sp.GetRequiredService<ExperimentLoggingStep>()
    });
    builder.Services.AddScoped<AssistantPipeline>();

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
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    // Гарантированное закрытие логов и освобождение ресурсов
    Log.CloseAndFlush();
}
