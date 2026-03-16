#!/usr/bin/env python3
"""
RAG ingestion pipeline: PDF/DOCX/TXT/MD → chunks → embeddings → Qdrant.

Usage:
    python ingest.py --source ../raw --collection knowledge_base

Phase 4 implementation.
"""
import argparse
import sys


def main() -> None:
    parser = argparse.ArgumentParser(description="Ingest knowledge base documents into Qdrant")
    parser.add_argument("--source", required=True, help="Path to source documents directory")
    parser.add_argument("--collection", default="knowledge_base", help="Qdrant collection name")
    parser.add_argument("--ollama-url", default="http://localhost:11434")
    parser.add_argument("--qdrant-url", default="http://localhost:6333")
    args = parser.parse_args()

    # TODO Phase 4: implement the full pipeline
    #   1. loader.py    — walk source dir, load PDF/DOCX/TXT/MD
    #   2. chunker.py   — split into ~400-token chunks with 50-token overlap
    #   3. embedder.py  — POST /api/embeddings to Ollama (nomic-embed-text)
    #   4. uploader.py  — upsert vectors + payload into Qdrant collection

    print(f"[Phase 4 TODO] Source: {args.source}")
    print(f"[Phase 4 TODO] Collection: {args.collection}")
    print("Ingestion pipeline not yet implemented — see Phase 4.")
    sys.exit(0)


if __name__ == "__main__":
    main()
