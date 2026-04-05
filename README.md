# AI-ассистент для СЭД

> Интеллектуальный ассистент для поддержки пользователей систем электронного документооборота на основе LLM

## О проекте

Этот проект является MVP для магистерской диссертации по теме:

**«Разработка интеллектуального ассистента для поддержки пользователей систем электронного документооборота на основе LLM»**

Ассистент работает в формате чата и помогает сотрудникам разбираться с задачами в системе электронного документооборота (СЭД). Он не выполняет действия в системе, а отвечает на вопросы, объясняет бизнес-процессы и помогает находить документы.

### Что умеет ассистент

| Сценарий | Пример вопроса |
|---|---|
| Ответы по инструкциям | «Как создать новый договор?» |
| Помощь по бизнес-процессу | «Почему мой документ вернулся?» |
| Поиск документов | «Найди все счета от января» |
| Анализ ошибок | «Почему я не могу подписать документ?» |

### Научная новизна

Предложен метод **контекстно-ориентированной поддержки пользователей СЭД на основе LLM**, который при формировании ответа учитывает:

- роль текущего пользователя;
- состояние и тип конкретного документа;
- текущий этап бизнес-процесса;
- результаты семантического поиска по базе знаний (RAG).

## Архитектура

```
Chat UI (React + Vite)  :5173
        │
Assistant API (ASP.NET Core)  :5000
        │
┌───────┼──────────┐
│       │          │
Ollama  Qdrant   SED Mock
(LLM)  (Vector DB) (JSON)
:11434  :6333
```

### Сервисы

| Сервис | Технология | Порт | Описание |
|---|---|---|---|
| **Chat UI** | React + Vite + TypeScript + TailwindCSS | 5173 | Интерфейс чата с отображением контекста документа |
| **Assistant API** | C# ASP.NET Core (.NET 8) | 5000 | Основной backend, pipeline обработки запросов |
| **Ollama** | Docker | 11434 | Локальная LLM (qwen2.5:7b) и эмбеддинги (nomic-embed-text) |
| **Qdrant** | Docker | 6333 | Векторная БД для RAG-поиска по базе знаний |
| **SED Mock** | JSON-файлы в `/data/` | — | Заглушка СЭД: документы, пользователи, бизнес-процессы |

### Pipeline обработки запроса

```
Запрос пользователя
        │
1. ClassificationStep      → тип запроса (4 вида)
        │
2. ContextExtractionStep   → контекст документа и пользователя из СЭД
        │
3. RagSearchStep           → релевантные фрагменты из базы знаний
        │
4. PromptBuildStep         → расширенный промпт (научная новизна)
        │
5. LlmGenerationStep       → ответ от LLM
        │
6. ExperimentLoggingStep   → метрики для A/B сравнения (тезис)
        │
Ответ пользователю
```

## Быстрый старт

### Требования

- [Docker](https://www.docker.com/) и Docker Compose
- ~10 ГБ свободного места (под модели Ollama)

### Запуск

```bash
# 1. Клонировать репозиторий
git clone https://github.com/keyloud/AI-Assistant-SED.git
cd AI-Assistant-SED

# 2. Скопировать конфигурацию
cp .env.example .env

# 3. Запустить все сервисы
docker compose up -d

# 4. Загрузить LLM-модели (первый раз — занимает время)
docker exec ollama ollama pull qwen2.5:7b
docker exec ollama ollama pull nomic-embed-text

# 5. Открыть чат
# http://localhost:5173
```

### Проверка здоровья сервисов

```bash
# Assistant API
curl http://localhost:5000/health

# Qdrant
curl http://localhost:6333/healthz

# Ollama
curl http://localhost:11434/api/version
```

## Структура репозитория

```
├── src/
│   ├── AssistantApi/       # C# ASP.NET Core backend
│   └── ChatUI/             # React frontend
├── data/                   # JSON-файлы заглушки СЭД
├── knowledge-base/
│   ├── raw/                # Исходные документы (PDF, DOCX)
│   └── ingestion/          # Скрипты загрузки в Qdrant
├── experiments/            # A/B эксперименты и датасет оценки
├── docker-compose.yml
├── project_context.md      # Детальный контекст проекта
└── MEMORY.md               # Ключевые решения и факты
```

## Технологии

- **Backend:** .NET 8, C# ASP.NET Core
- **Frontend:** React, Vite, TypeScript, TailwindCSS
- **LLM:** Ollama — `qwen2.5:7b`
- **Эмбеддинги:** Ollama — `nomic-embed-text`
- **Vector DB:** Qdrant 1.8.4
- **Ingestion:** Python (PyPDF2, python-docx)
- **Тесты:** xUnit + Moq
- **Оркестрация:** Docker Compose
