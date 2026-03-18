# AI Assistant for Electronic Document Management Systems (SED)

[🇷🇺 Русская версия](#русская-версия) | [🇬🇧 English Version](#english-version)

---

## Русская версия

### 📋 Описание проекта

Интеллектуальный ассистент для поддержки пользователей систем электронного документооборота (СЭД) на основе LLM. Ассистент помогает пользователям работать с документами, предоставляя контекстно-ориентированные подсказки и рекомендации.

**Научная новизна:** Метод расширения пользовательского запроса контекстом системы, включая данные документа, состояние бизнес-процесса и результаты поиска по нормативной документации.

### 🎯 Основные возможности

- Ответы на вопросы о документообороте на естественном языке
- Объяснение необходимых действий в конкретных ситуациях
- Поиск документов с использованием базы знаний (RAG)
- Анализ состояния бизнес-процессов
- Помощь в понимании ошибок и возвратов документов

### 🏗️ Архитектура

Проект состоит из 5 сервисов:

- **ChatUI** - веб-интерфейс (React + Vite + TypeScript + TailwindCSS) на порту `:5173`
- **AssistantApi** - основной API (C# ASP.NET Core) на порту `:5000`
- **Ollama** - LLM сервер (qwen2.5:7b + nomic-embed-text) на порту `:11434`
- **Qdrant** - векторная база данных для RAG на порту `:6333`
- **SED Mock** - имитация СЭД через JSON-файлы в `/data/`

### 📦 Как клонировать проект через Git Bash

#### Предварительные требования

1. **Git** - система контроля версий
   - Скачайте с [git-scm.com](https://git-scm.com/downloads)
   - Проверьте установку: `git --version`

2. **Docker Desktop** - для запуска контейнеров
   - Скачайте с [docker.com](https://www.docker.com/products/docker-desktop)
   - Проверьте установку: `docker --version` и `docker-compose --version`

#### Шаги клонирования

1. **Откройте Git Bash** (на Windows: правый клик → "Git Bash Here" в нужной папке)

2. **Клонируйте репозиторий:**
   ```bash
   git clone https://github.com/keyloud/AI-Assistant-SED.git
   ```

3. **Перейдите в папку проекта:**
   ```bash
   cd AI-Assistant-SED
   ```

4. **Проверьте содержимое:**
   ```bash
   ls -la
   ```

### 🚀 Запуск проекта

#### Подготовка к запуску

1. **Создайте файл окружения** (опционально):
   ```bash
   cp .env.example .env
   ```

2. **Запустите все сервисы через Docker Compose:**
   ```bash
   docker-compose up -d
   ```

   Это запустит все необходимые сервисы в фоновом режиме.

3. **Первый запуск: загрузка моделей Ollama**

   При первом запуске необходимо загрузить модели LLM:
   ```bash
   # Подключитесь к контейнеру Ollama
   docker exec -it ollama bash

   # Загрузите модели
   ollama pull qwen2.5:7b
   ollama pull nomic-embed-text

   # Выйдите из контейнера
   exit
   ```

4. **Проверьте статус сервисов:**
   ```bash
   docker-compose ps
   ```

#### Доступ к приложению

После запуска приложение будет доступно по адресам:

- **ChatUI (веб-интерфейс):** http://localhost:5173
- **AssistantApi (API):** http://localhost:5000
- **Qdrant (векторная БД):** http://localhost:6333
- **Ollama (LLM):** http://localhost:11434

#### Остановка проекта

```bash
docker-compose down
```

Для удаления всех данных (volumes):
```bash
docker-compose down -v
```

### 📚 Структура проекта

```
AI-Assistant-SED/
├── src/
│   ├── AssistantApi/      # Backend API (C# ASP.NET Core)
│   └── ChatUI/            # Frontend UI (React + TypeScript)
├── data/                  # Mock данные СЭД (JSON)
├── knowledge-base/        # База знаний и скрипты для RAG
├── experiments/           # Эксперименты и оценка модели
├── docker-compose.yml     # Конфигурация Docker
└── README.md             # Этот файл
```

### 🛠️ Технологический стек

- **Backend:** .NET 8, C# ASP.NET Core
- **Frontend:** React + Vite + TypeScript + TailwindCSS
- **LLM:** Ollama (qwen2.5:7b)
- **Embeddings:** nomic-embed-text
- **Vector DB:** Qdrant 1.8.4
- **Контейнеризация:** Docker Compose

### 🔧 Разработка

#### Разработка Backend (AssistantApi)

```bash
cd src/AssistantApi/AssistantApi
dotnet restore
dotnet build
dotnet run
```

#### Разработка Frontend (ChatUI)

```bash
cd src/ChatUI
npm install
npm run dev
```

### 📖 Дополнительная документация

- `MEMORY.md` - детальная информация о проекте и архитектуре
- `project_context.md` - контекст диссертации и научная новизна
- `experiments/README.md` - информация об экспериментах и оценке

### 🤝 Участие в разработке

1. Форкните репозиторий
2. Создайте ветку для новой функции: `git checkout -b feature/your-feature`
3. Внесите изменения и закоммитьте: `git commit -am 'Add some feature'`
4. Отправьте в ветку: `git push origin feature/your-feature`
5. Создайте Pull Request

### 📝 Лицензия

Проект разработан в рамках диссертационного исследования.

---

## English Version

### 📋 Project Description

An intelligent assistant for supporting users of Electronic Document Management Systems (EDMS) based on LLM. The assistant helps users work with documents by providing context-oriented hints and recommendations.

**Scientific Novelty:** A method for expanding user queries with system context, including document data, business process state, and search results from regulatory documentation.

### 🎯 Key Features

- Answering questions about document workflow in natural language
- Explaining required actions in specific situations
- Document search using knowledge base (RAG)
- Business process state analysis
- Help in understanding errors and document returns

### 🏗️ Architecture

The project consists of 5 services:

- **ChatUI** - web interface (React + Vite + TypeScript + TailwindCSS) on port `:5173`
- **AssistantApi** - main API (C# ASP.NET Core) on port `:5000`
- **Ollama** - LLM server (qwen2.5:7b + nomic-embed-text) on port `:11434`
- **Qdrant** - vector database for RAG on port `:6333`
- **SED Mock** - EDMS simulation via JSON files in `/data/`

### 📦 How to Clone the Project via Git Bash

#### Prerequisites

1. **Git** - version control system
   - Download from [git-scm.com](https://git-scm.com/downloads)
   - Verify installation: `git --version`

2. **Docker Desktop** - for running containers
   - Download from [docker.com](https://www.docker.com/products/docker-desktop)
   - Verify installation: `docker --version` and `docker-compose --version`

#### Cloning Steps

1. **Open Git Bash** (on Windows: right-click → "Git Bash Here" in the desired folder)

2. **Clone the repository:**
   ```bash
   git clone https://github.com/keyloud/AI-Assistant-SED.git
   ```

3. **Navigate to the project folder:**
   ```bash
   cd AI-Assistant-SED
   ```

4. **Check the contents:**
   ```bash
   ls -la
   ```

### 🚀 Running the Project

#### Preparation

1. **Create environment file** (optional):
   ```bash
   cp .env.example .env
   ```

2. **Start all services via Docker Compose:**
   ```bash
   docker-compose up -d
   ```

   This will start all necessary services in background mode.

3. **First run: Download Ollama models**

   On first run, you need to download LLM models:
   ```bash
   # Connect to Ollama container
   docker exec -it ollama bash

   # Download models
   ollama pull qwen2.5:7b
   ollama pull nomic-embed-text

   # Exit container
   exit
   ```

4. **Check service status:**
   ```bash
   docker-compose ps
   ```

#### Accessing the Application

After startup, the application will be available at:

- **ChatUI (web interface):** http://localhost:5173
- **AssistantApi (API):** http://localhost:5000
- **Qdrant (vector DB):** http://localhost:6333
- **Ollama (LLM):** http://localhost:11434

#### Stopping the Project

```bash
docker-compose down
```

To remove all data (volumes):
```bash
docker-compose down -v
```

### 📚 Project Structure

```
AI-Assistant-SED/
├── src/
│   ├── AssistantApi/      # Backend API (C# ASP.NET Core)
│   └── ChatUI/            # Frontend UI (React + TypeScript)
├── data/                  # EDMS mock data (JSON)
├── knowledge-base/        # Knowledge base and RAG scripts
├── experiments/           # Experiments and model evaluation
├── docker-compose.yml     # Docker configuration
└── README.md             # This file
```

### 🛠️ Technology Stack

- **Backend:** .NET 8, C# ASP.NET Core
- **Frontend:** React + Vite + TypeScript + TailwindCSS
- **LLM:** Ollama (qwen2.5:7b)
- **Embeddings:** nomic-embed-text
- **Vector DB:** Qdrant 1.8.4
- **Containerization:** Docker Compose

### 🔧 Development

#### Backend Development (AssistantApi)

```bash
cd src/AssistantApi/AssistantApi
dotnet restore
dotnet build
dotnet run
```

#### Frontend Development (ChatUI)

```bash
cd src/ChatUI
npm install
npm run dev
```

### 📖 Additional Documentation

- `MEMORY.md` - detailed project and architecture information
- `project_context.md` - dissertation context and scientific novelty
- `experiments/README.md` - information about experiments and evaluation

### 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -am 'Add some feature'`
4. Push to the branch: `git push origin feature/your-feature`
5. Create a Pull Request

### 📝 License

The project was developed as part of dissertation research.
