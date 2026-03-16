# Project Memory: AI Assistant for СЭД

## Project Overview
Thesis: "Development of an intelligent assistant to support users of electronic document management systems based on LLM"
Scientific novelty: Context-oriented user support via LLM — expands user query with document context + business process state + RAG results.

## Что делает ассистент (суть продукта)
- **Целевая аудитория:** сотрудники СЭД, особенно новички
- **НЕ делает:** не изменяет документы, не выполняет операции в СЭД
- **Делает:**
  - Отвечает на вопросы о документообороте на естественном языке
  - Объясняет, какие действия нужны в конкретной ситуации
  - Использует базу знаний: инструкции, регламенты, виды документов
  - Помогает находить документы пользователя + структурирует результаты
  - Объединяет разрозненные источники в контекстно-ориентированный совет
- **Ключевая ценность:** ускоряет обучение новых сотрудников, снижает риск ошибок
- **Отличие от скриптов:** понимает смысл запроса, а не выполняет жёсткий сценарий

## Architecture (5 services)
- **ChatUI**: Python/Streamlit :8501
- **AssistantApi**: C# ASP.NET Core :5000 — CORE component
- **Ollama**: qwen2.5:7b (LLM) + nomic-embed-text (embeddings) :11434
- **Qdrant**: Vector DB for RAG :6333
- **SED Mock**: JSON files in /data/

## Key Files (planned)
- `src/AssistantApi/AssistantApi/Pipeline/AssistantPipeline.cs` — core orchestrator (scientific novelty)
- `src/AssistantApi/AssistantApi/Services/PromptBuilderService.cs` — 4 prompt templates
- `src/AssistantApi/AssistantApi/Pipeline/PipelineContext.cs` — shared pipeline state
- `knowledge-base/ingestion/ingest.py` — RAG ingestion (PDF/DOCX → Qdrant)

## Pipeline Steps (in order)
1. ClassificationStep → RequestType (4 types)
2. ContextExtractionStep → DocumentContext + UserContext from SED mock
3. RagSearchStep → KnowledgeChunk[] from Qdrant
4. PromptBuildStep → AugmentedPrompt (the novel contribution)
5. LlmGenerationStep → LLM response
6. ExperimentLoggingStep → metrics for thesis A/B comparison

## 4 Request Types
- InstructionQuery — "как создать документ?"
- BusinessProcessQuery — "почему документ вернулся?"
- DocumentSearchQuery — "найди договоры от января"
- ErrorAnalysisQuery — "почему не могу подписать?"

## Development Phases
1. Infrastructure (docker-compose, health endpoint)
2. Basic chat pipeline skeleton
3. SED mock + context extraction
4. RAG system (ingestion + search)
5. Prompt engineering + all 4 scenarios
6. Evaluation framework (A/B comparison for thesis)

## Tech Stack
- Backend: .NET 8, C# ASP.NET Core
- Frontend: React + Vite + TypeScript + TailwindCSS
- LLM: Ollama (qwen2.5:7b), Embedding: nomic-embed-text
- Vector DB: Qdrant 1.8.4
- Ingestion: Python (PyPDF2, python-docx)
- Testing: xUnit + Moq
- Container: Docker Compose

## SED Mock — подход
- Сложность: НИЗКАЯ (~2-3 часа)
- Реализация: JSON-файлы в /data/ + JsonSedRepository.cs читает их
- Файлы: 4-5 документов, 3 пользователя, 3 бизнес-процесса
- Отдельный API-слой (ISedService) позволяет позже заменить мок на реальный Docsvision
- Модели уже определены: DocumentContext, UserContext, BusinessProcess

## Решения по UI
- **Выбран React + Vite + TypeScript + TailwindCSS** (2026-03-16)
- Стриминг токенов через нативный EventSource (SSE)
- Левая панель — контекст документа, правая — чат, опционально нижняя — RAG-источники
- Порт: :5173 (dev), :80 (prod в Docker)

## See Also
- `architecture.md` — detailed architecture notes
