#!/usr/bin/env python3
"""
Evaluate RAG retrieval and response latency through the Assistant API.

The script calculates the metrics used in the defense presentation:
Recall@K, MRR, Latency_avg, and Score_avg when manual expert scores exist.
"""

from __future__ import annotations

import argparse
import logging
import csv
import json
import re
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_API_URL = "http://localhost:5000"
DEFAULT_DATASET = Path(__file__).resolve().parents[1] / "eval-dataset" / "rag_questions.json"
DEFAULT_RESULTS_DIR = Path(__file__).resolve().parent / "results"

# module logger
logger = logging.getLogger(__name__)


@dataclass(slots=True)
class EvaluationRow:
    question_id: str
    question: str
    expected_sources: list[str]
    response_text: str
    rag_sources: list[dict[str, Any]]
    matched_rank: int | None
    reciprocal_rank: float
    recall_hit: bool
    latency_ms: int
    error: str = ""


def normalize(value: str) -> str:
    value = value.lower()
    value = value.replace("_", " ").replace("-", " ")
    value = re.sub(r"[^a-zа-яё0-9]+", " ", value, flags=re.IGNORECASE)
    return re.sub(r"\s+", " ", value).strip()


def compact(value: str) -> str:
    return normalize(value).replace(" ", "")


def source_matches(source: dict[str, Any], expected_sources: list[str]) -> bool:
    candidates = [
        str(source.get("title") or ""),
        str(source.get("section") or ""),
        str(source.get("sourceFile") or ""),
        str(source.get("source_file") or ""),
    ]

    candidate_norms = [normalize(candidate) for candidate in candidates if candidate]
    candidate_compacts = [compact(candidate) for candidate in candidates if candidate]

    for expected in expected_sources:
        expected_norm = normalize(expected)
        expected_compact = compact(expected)
        if any(expected_norm and expected_norm in candidate for candidate in candidate_norms):
            return True
        if any(expected_compact and expected_compact in candidate for candidate in candidate_compacts):
            return True

    return False


def find_first_relevant_rank(rag_sources: list[dict[str, Any]], expected_sources: list[str], top_k: int) -> int | None:
    for index, source in enumerate(rag_sources[:top_k], start=1):
        if source_matches(source, expected_sources):
            return index
    return None


def post_chat(api_url: str, question_id: str, question: str, timeout: float) -> tuple[dict[str, Any], int]:
    endpoint = f"{api_url.rstrip('/')}/api/chat"
    payload = {
        "sessionId": f"eval-{question_id}-{int(time.time() * 1000)}",
        "message": question,
        "conversationHistory": [],
    }
    data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        endpoint,
        data=data,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )

    started = time.perf_counter()
    with urllib.request.urlopen(request, timeout=timeout) as response:
        raw = response.read()
    latency_ms = int((time.perf_counter() - started) * 1000)
    return json.loads(raw.decode("utf-8")), latency_ms


def load_dataset(path: Path) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8") as file:
        data = json.load(file)
    if not isinstance(data, list) or not data:
        raise ValueError(f"Dataset must be a non-empty JSON array: {path}")
    return data


def read_existing_scores(path: Path) -> dict[str, dict[str, str]]:
    if not path.exists():
        return {}

    with path.open("r", encoding="utf-8-sig", newline="") as file:
        reader = csv.DictReader(file)
        return {
            row.get("id", ""): row
            for row in reader
            if row.get("id")
        }


def calculate_score_avg(existing_scores: dict[str, dict[str, str]]) -> tuple[float | None, int]:
    values: list[float] = []
    for row in existing_scores.values():
        raw = (row.get("score") or "").replace(",", ".").strip()
        if not raw:
            continue
        try:
            score = float(raw)
        except ValueError:
            continue
        if 1 <= score <= 5:
            values.append(score)

    if not values:
        return None, 0
    return sum(values) / len(values), len(values)


def serialize_sources(rag_sources: list[dict[str, Any]]) -> str:
    compact_sources = [
        {
            "title": source.get("title", ""),
            "sourceFile": source.get("sourceFile", ""),
            "section": source.get("section", ""),
            "score": source.get("score", 0),
        }
        for source in rag_sources
    ]
    return json.dumps(compact_sources, ensure_ascii=False)


def write_details(path: Path, rows: list[EvaluationRow]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(
            file,
            fieldnames=[
                "id",
                "question",
                "expected_sources",
                "matched_rank",
                "reciprocal_rank",
                "recall_hit",
                "latency_ms",
                "rag_sources",
                "assistant_response",
                "error",
            ],
        )
        writer.writeheader()
        for row in rows:
            writer.writerow(
                {
                    "id": row.question_id,
                    "question": row.question,
                    "expected_sources": "; ".join(row.expected_sources),
                    "matched_rank": row.matched_rank or "",
                    "reciprocal_rank": f"{row.reciprocal_rank:.4f}",
                    "recall_hit": int(row.recall_hit),
                    "latency_ms": row.latency_ms,
                    "rag_sources": serialize_sources(row.rag_sources),
                    "assistant_response": row.response_text,
                    "error": row.error,
                }
            )


def write_expert_template(path: Path, rows: list[EvaluationRow], existing_scores: dict[str, dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(
            file,
            fieldnames=[
                "id",
                "question",
                "assistant_response",
                "rag_sources",
                "score",
                "comment",
            ],
        )
        writer.writeheader()
        for row in rows:
            previous = existing_scores.get(row.question_id, {})
            writer.writerow(
                {
                    "id": row.question_id,
                    "question": row.question,
                    "assistant_response": row.response_text,
                    "rag_sources": serialize_sources(row.rag_sources),
                    "score": previous.get("score", ""),
                    "comment": previous.get("comment", ""),
                }
            )


def write_metrics(
    path: Path,
    rows: list[EvaluationRow],
    top_k: int,
    score_avg: float | None,
    scored_count: int,
) -> None:
    total = len(rows)
    successful = [row for row in rows if not row.error]
    recall_hits = sum(1 for row in rows if row.recall_hit)
    mrr = sum(row.reciprocal_rank for row in rows) / total if total else 0
    latency_avg_ms = sum(row.latency_ms for row in successful) / len(successful) if successful else 0

    payload = {
        "created_at": datetime.now(timezone.utc).isoformat(),
        "top_k": top_k,
        "questions_total": total,
        "questions_successful": len(successful),
        "recall_at_k": recall_hits / total if total else 0,
        "mrr": mrr,
        "latency_avg_ms": latency_avg_ms,
        "score_avg": score_avg,
        "score_count": scored_count,
    }

    with path.open("w", encoding="utf-8") as file:
        json.dump(payload, file, ensure_ascii=False, indent=2)


def evaluate(api_url: str, dataset_path: Path, results_dir: Path, top_k: int, timeout: float) -> None:
    dataset = load_dataset(dataset_path)
    results_dir.mkdir(parents=True, exist_ok=True)

    # Configure file logging inside results directory so we can inspect failures
    try:
        log_file = results_dir / "evaluate.log"
        fh = logging.FileHandler(log_file, encoding="utf-8")
        fh.setLevel(logging.DEBUG)
        fh.setFormatter(logging.Formatter("%(asctime)s [%(levelname)s] %(message)s"))
        logging.getLogger().addHandler(fh)
        logger.info("Logging evaluation run to %s", log_file)
    except Exception as ex:
        logger.exception("Failed to create log file %s: %s", results_dir, ex)

    expert_scores_path = results_dir / "expert_scores_template.csv"
    existing_scores = read_existing_scores(expert_scores_path)
    rows: list[EvaluationRow] = []

    for item in dataset:
        question_id = str(item["id"])
        question = str(item["question"])
        expected_sources = [str(source) for source in item.get("expected_sources", [])]

        try:
            response, latency_ms = post_chat(api_url, question_id, question, timeout)
            rag_sources = response.get("ragSources") or []
            matched_rank = find_first_relevant_rank(rag_sources, expected_sources, top_k)
            reciprocal_rank = 1 / matched_rank if matched_rank else 0
            rows.append(
                EvaluationRow(
                    question_id=question_id,
                    question=question,
                    expected_sources=expected_sources,
                    response_text=str(response.get("response") or ""),
                    rag_sources=rag_sources,
                    matched_rank=matched_rank,
                    reciprocal_rank=reciprocal_rank,
                    recall_hit=matched_rank is not None,
                    latency_ms=latency_ms,
                )
            )
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError, KeyError) as error:
            rows.append(
                EvaluationRow(
                    question_id=question_id,
                    question=question,
                    expected_sources=expected_sources,
                    response_text="",
                    rag_sources=[],
                    matched_rank=None,
                    reciprocal_rank=0,
                    recall_hit=False,
                    latency_ms=0,
                    error=str(error),
                )
            )

    score_avg, scored_count = calculate_score_avg(existing_scores)
    try:
        details_path = results_dir / "details.csv"
        logger.info("Writing details to %s", details_path)
        write_details(details_path, rows)
    except Exception as ex:
        logger.exception("Failed to write details.csv: %s", ex)

    try:
        logger.info("Writing expert template to %s", expert_scores_path)
        write_expert_template(expert_scores_path, rows, existing_scores)
    except Exception as ex:
        logger.exception("Failed to write expert_scores_template.csv: %s", ex)

    try:
        metrics_path = results_dir / "metrics.json"
        logger.info("Writing metrics to %s", metrics_path)
        write_metrics(metrics_path, rows, top_k, score_avg, scored_count)
    except Exception as ex:
        logger.exception("Failed to write metrics.json: %s", ex)

    print(f"[OK] Questions: {len(rows)}")
    print(f"[OK] Results: {results_dir}")
    print(f"[OK] Recall@{top_k}, MRR, Latency_avg saved to metrics.json")
    if score_avg is None:
        print("[INFO] Fill score values in expert_scores_template.csv and run again to calculate Score_avg.")
    else:
        print(f"[OK] Score_avg: {score_avg:.2f} based on {scored_count} expert scores")


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate Assistant RAG metrics through /api/chat")
    parser.add_argument("--api-url", default=DEFAULT_API_URL, help="Assistant API base URL")
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET, help="Path to rag_questions.json")
    parser.add_argument("--results-dir", type=Path, default=DEFAULT_RESULTS_DIR, help="Directory for metrics output")
    parser.add_argument("--top-k", type=int, default=3, help="Number of RAG sources considered for Recall@K and MRR")
    parser.add_argument("--timeout", type=float, default=180, help="HTTP timeout per question in seconds")
    args = parser.parse_args()

    evaluate(
        api_url=args.api_url,
        dataset_path=args.dataset,
        results_dir=args.results_dir,
        top_k=args.top_k,
        timeout=args.timeout,
    )


if __name__ == "__main__":
    main()
