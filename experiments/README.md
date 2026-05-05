# experiments/

Содержит материалы для оценки качества прототипа и подготовки результатов к защите.

## Структура

- `eval-dataset/` — тестовые вопросы и ожидаемые релевантные источники.
- `evaluation/` — скрипты расчета метрик.
- `evaluation/results/` — результаты запусков: сводные метрики, детализация, шаблон экспертной оценки.

## RAG evaluation

Минимальный расчет метрик для презентации:

```powershell
python experiments/evaluation/evaluate_rag.py --api-url http://localhost:5000 --top-k 3
```

Перед запуском должны быть доступны `assistant-api`, `ollama`, `qdrant`, а база знаний должна быть загружена в коллекцию `knowledge_base`.

Результаты сохраняются в `experiments/evaluation/results/`:

- `metrics.json` — `Recall@K`, `MRR`, `Latency_avg` и `Score_avg`, если заполнена экспертная оценка.
- `details.csv` — построчные результаты по каждому вопросу.
- `expert_scores_template.csv` — таблица для ручного заполнения оценки ответа по шкале 1-5.
