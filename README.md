# AI-Assistant-SED

Прототип AI-ассистента для СЭД с двумя MVP-сценариями:
- Вопрос-ответ по базе знаний
- Проверка документа (обязательные реквизиты) для PDF и DOCX

Текущие контроллеры backend API:
- GET /api/health
- POST /api/chat
- POST /api/documents/validate

## Быстрый старт (5-10 минут)

Выполните команды по шагам:

1) Перейдите в корень проекта:

```powershell
cd "C:\Users\kosol\OneDrive\Рабочий стол\AI-Assistant-SED"
```

2) Поднимите контейнеры:

```powershell
docker compose up -d --build
```

3) Загрузите модели Ollama:

```powershell
docker exec ollama ollama pull qwen2.5:3b
docker exec ollama ollama pull bge-m3
```

4) Проверьте, что стек поднялся:

```powershell
docker compose ps
curl.exe -i http://localhost:5000/api/health
```

5) Откройте UI:

- http://localhost:5173

6) Проверьте ответ LLM напрямую через Ollama:

```powershell
$body = @{ model = "qwen2.5:3b"; prompt = "Привет! Ответь одной фразой."; stream = $false } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method Post -ContentType "application/json" -Body $body
```

Если что-то не запустилось, смотрите раздел "Устранение неполадок" ниже.

## Требования

- Docker Desktop (Engine running)
- PowerShell
- Опционально для локального (без Docker) запуска:
  - .NET 8 SDK
  - Node.js 18+ и npm

## Первый запуск (Docker, рекомендуется)

Выполняйте все команды из корня проекта:

```powershell
cd "C:\Users\kosol\OneDrive\Рабочий стол\AI-Assistant-SED"
```

### 1) Сборка и запуск контейнеров

```powershell
docker compose up -d --build
```

### 2) Загрузка необходимых моделей Ollama

```powershell
docker exec ollama ollama pull qwen2.5:3b
docker exec ollama ollama pull bge-m3
```

### 3) Проверка состояния контейнеров

```powershell
docker compose ps
docker ps
```

### 4) Проверка health сервиса

```powershell
curl.exe -i http://localhost:5000/api/health
```

Ожидается: HTTP 200 и healthy-статус сервисов.

## Команды для проверки

### Проверка LLM напрямую через Ollama

```powershell
$body = @{ model = "qwen2.5:3b"; prompt = "Привет! Ответь одной фразой."; stream = $false } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:11434/api/generate" -Method Post -ContentType "application/json" -Body $body
```

### Проверка документа через backend API

```powershell
curl.exe -X POST "http://localhost:5000/api/documents/validate" -F "file=@C:\path\to\document.docx" -F "documentTypeHint=договор"
```

Допустимые форматы файла: .docx и .pdf

Шаблоны проверки обязательных реквизитов настраиваются в `src/AssistantApi/AssistantApi/appsettings.json` в секции `DocumentValidation:Templates`.

## Команды Chat UI

### Режим Docker

```powershell
docker compose up -d chat-ui
```

Открыть в браузере:
- http://localhost:5173

### Локальный режим

```powershell
npm ci --prefix src/ChatUI
npm run --prefix src/ChatUI dev
```

## Локальный запуск backend (опционально)

```powershell
dotnet run --project src/AssistantApi/AssistantApi/AssistantApi.csproj
```

Локальный launch profile обычно поднимает:
- http://localhost:57112
- https://localhost:57111

## Полезные Docker-команды

### Логи

```powershell
docker compose logs -f
docker compose logs -f assistant-api
docker compose logs -f ollama
docker compose logs -f qdrant
docker compose logs -f chat-ui
```

### Перезапуск сервисов

```powershell
docker compose restart
docker compose restart assistant-api
```

### Пересоздание одного сервиса после изменения env/config

```powershell
docker compose up -d --force-recreate assistant-api
```

### Остановка и повторный запуск

```powershell
docker compose stop
docker compose start
```

### Полное выключение

```powershell
docker compose down
```

### Полное выключение с удалением томов (destructive)

```powershell
docker compose down -v
```

Внимание: команда удаляет сохраненные данные Qdrant и volume с кешем моделей Ollama.

## Порты

- 5000 -> assistant-api
- 5173 -> chat-ui
- 11434 -> ollama
- 6333, 6334 -> qdrant

## Устранение неполадок

### model not found в Ollama

```powershell
docker exec ollama ollama list
docker exec ollama ollama pull qwen2.5:3b
```

### Ошибка импорта ChatUI: Cannot find module react-dom/client

```powershell
npm ci --prefix src/ChatUI
```

После этого перезапустите TS Server в VS Code, если диагностика закеширована.

### Быстрая проверка деталей health

```powershell
curl.exe -i http://localhost:5000/api/health
```
