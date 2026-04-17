#!/usr/bin/env python3
"""
RAG ingestion pipeline: PDF/DOCX/TXT/MD -> chunks -> embeddings -> Qdrant.

Usage:
    python ingest.py --source ../raw/pharmacy --collection knowledge_base
"""

import argparse
import hashlib
import json
import re
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import httpx
from docx import Document
from PyPDF2 import PdfReader
from tqdm import tqdm


SUPPORTED_EXTENSIONS = {".docx", ".md", ".txt", ".pdf"}
DEFAULT_SECTION = "General"


@dataclass(slots=True)
class Segment:
    section: str
    text: str


@dataclass(slots=True)
class LoadedDocument:
    source_file: str
    category: str
    file_type: str
    document_title: str
    sections: list[Segment]


@dataclass(slots=True)
class Chunk:
    id: str
    content: str
    source_file: str
    document_title: str
    section: str
    category: str
    file_type: str
    chunk_index: int
    total_chunks: int


def normalize_text(text: str) -> str:
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    text = text.replace("\u00a0", " ")
    text = re.sub(r"[ \t]+", " ", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def infer_category(source: Path, root: Path) -> str:
    rel_parts = source.relative_to(root).parts
    return rel_parts[0] if rel_parts else "unknown"


def infer_title(source: Path) -> str:
    return source.stem.replace("_", " ").strip()


def parse_markdown_sections(text: str) -> list[Segment]:
    segments: list[Segment] = []
    current_section = DEFAULT_SECTION
    buffer: list[str] = []

    def flush() -> None:
        if not buffer:
            return
        chunk_text = normalize_text("\n".join(buffer))
        if chunk_text:
            segments.append(Segment(section=current_section, text=chunk_text))
        buffer.clear()

    for line in text.splitlines():
        heading_match = re.match(r"^#{1,6}\s+(.*)$", line.strip())
        if heading_match:
            flush()
            current_section = heading_match.group(1).strip() or DEFAULT_SECTION
            continue
        buffer.append(line)

    flush()
    if not segments:
        normalized = normalize_text(text)
        if normalized:
            segments.append(Segment(section=DEFAULT_SECTION, text=normalized))
    return segments


def parse_plain_sections(text: str) -> list[Segment]:
    normalized = normalize_text(text)
    if not normalized:
        return []
    return [Segment(section=DEFAULT_SECTION, text=normalized)]


def parse_docx_sections(path: Path) -> list[Segment]:
    document = Document(path)
    segments: list[Segment] = []
    current_section = DEFAULT_SECTION
    buffer: list[str] = []

    def flush() -> None:
        if not buffer:
            return
        text_block = normalize_text("\n".join(buffer))
        if text_block:
            segments.append(Segment(section=current_section, text=text_block))
        buffer.clear()

    for paragraph in document.paragraphs:
        text = paragraph.text.strip()
        if not text:
            continue

        style_name = (paragraph.style.name if paragraph.style else "").lower()
        is_heading_style = style_name.startswith("heading")
        is_heading_text = bool(re.match(r"^(\d+(\.\d+)*|§)\s+", text))
        if is_heading_style or is_heading_text:
            flush()
            current_section = text
            continue

        buffer.append(text)

    for table in document.tables:
        for row in table.rows:
            row_values = [cell.text.strip() for cell in row.cells if cell.text and cell.text.strip()]
            if row_values:
                buffer.append(" | ".join(row_values))

    flush()
    return segments


def parse_pdf_sections(path: Path) -> list[Segment]:
    reader = PdfReader(str(path))
    pages: list[str] = []
    for page in reader.pages:
        page_text = page.extract_text() or ""
        if page_text.strip():
            pages.append(page_text)
    return parse_plain_sections("\n\n".join(pages))


def load_document(path: Path, source_root: Path) -> LoadedDocument | None:
    ext = path.suffix.lower()
    raw_text = ""
    sections: list[Segment] = []

    if ext == ".md":
        raw_text = path.read_text(encoding="utf-8", errors="ignore")
        sections = parse_markdown_sections(raw_text)
    elif ext == ".txt":
        raw_text = path.read_text(encoding="utf-8", errors="ignore")
        sections = parse_plain_sections(raw_text)
    elif ext == ".docx":
        sections = parse_docx_sections(path)
    elif ext == ".pdf":
        sections = parse_pdf_sections(path)
    else:
        return None

    if not sections:
        return None

    source_file = str(path.relative_to(source_root)).replace("\\", "/")
    return LoadedDocument(
        source_file=source_file,
        category=infer_category(path, source_root),
        file_type=ext.lstrip("."),
        document_title=infer_title(path),
        sections=sections,
    )


def split_with_overlap(text: str, chunk_size: int, overlap: int) -> list[str]:
    if chunk_size <= 0:
        raise ValueError("chunk_size must be > 0")
    if overlap < 0:
        raise ValueError("overlap must be >= 0")
    if overlap >= chunk_size:
        raise ValueError("overlap must be less than chunk_size")

    normalized = normalize_text(text)
    if not normalized:
        return []

    chunks: list[str] = []
    start = 0
    length = len(normalized)

    while start < length:
        end = min(start + chunk_size, length)
        piece = normalized[start:end]
        if end < length:
            split_point = piece.rfind(" ")
            if split_point > int(chunk_size * 0.6):
                end = start + split_point
                piece = normalized[start:end]

        piece = piece.strip()
        if piece:
            chunks.append(piece)

        if end >= length:
            break
        start = max(end - overlap, start + 1)

    return chunks


def build_chunk_id(source_file: str, section: str, chunk_index: int) -> str:
    base = f"{source_file}::{section}::{chunk_index}"
    digest = hashlib.sha1(base.encode("utf-8")).hexdigest()
    return str(uuid.uuid5(uuid.NAMESPACE_URL, digest))


def build_chunks(documents: list[LoadedDocument], chunk_size: int, overlap: int) -> list[Chunk]:
    chunks: list[Chunk] = []

    for doc in documents:
        doc_chunks: list[Chunk] = []
        chunk_counter = 0
        for segment in doc.sections:
            split_parts = split_with_overlap(segment.text, chunk_size=chunk_size, overlap=overlap)
            for content in split_parts:
                chunk_counter += 1
                doc_chunks.append(
                    Chunk(
                        id=build_chunk_id(doc.source_file, segment.section, chunk_counter),
                        content=content,
                        source_file=doc.source_file,
                        document_title=doc.document_title,
                        section=segment.section,
                        category=doc.category,
                        file_type=doc.file_type,
                        chunk_index=chunk_counter,
                        total_chunks=0,
                    )
                )

        total = len(doc_chunks)
        for chunk in doc_chunks:
            chunk.total_chunks = total
        chunks.extend(doc_chunks)

    return chunks


def ensure_collection(
    client: httpx.Client,
    qdrant_url: str,
    collection: str,
    vector_size: int,
    recreate: bool,
) -> None:
    base = qdrant_url.rstrip("/")
    collection_url = f"{base}/collections/{collection}"

    if recreate:
        client.delete(collection_url, timeout=30.0)

    response = client.get(collection_url, timeout=30.0)
    if response.status_code == 200:
        return
    if response.status_code not in (404,):
        raise RuntimeError(f"Failed to check Qdrant collection: {response.status_code} {response.text}")

    payload = {
        "vectors": {
            "size": vector_size,
            "distance": "Cosine",
        }
    }
    create_response = client.put(collection_url, json=payload, timeout=30.0)
    if create_response.status_code not in (200, 201):
        raise RuntimeError(
            f"Failed to create collection '{collection}': "
            f"{create_response.status_code} {create_response.text}"
        )


def embed_text(client: httpx.Client, ollama_url: str, model: str, text: str) -> list[float]:
    endpoint = f"{ollama_url.rstrip('/')}/api/embeddings"
    payload = {
        "model": model,
        "prompt": text,
    }
    response = client.post(endpoint, json=payload, timeout=60.0)
    if response.status_code != 200:
        raise RuntimeError(f"Embedding request failed: {response.status_code} {response.text}")

    data = response.json()
    embedding = data.get("embedding", [])
    if not isinstance(embedding, list) or not embedding:
        raise RuntimeError("Embedding response does not contain a valid vector")
    return embedding


def to_qdrant_point(chunk: Chunk, vector: list[float], now_iso: str) -> dict[str, Any]:
    payload = {
        "content": chunk.content,
        "source_file": chunk.source_file,
        "document_title": chunk.document_title,
        "section": chunk.section,
        "category": chunk.category,
        "file_type": chunk.file_type,
        "chunk_index": str(chunk.chunk_index),
        "total_chunks": str(chunk.total_chunks),
        "language": "ru",
        "updated_at": now_iso,
        "version": "v1",
    }
    return {
        "id": chunk.id,
        "vector": vector,
        "payload": payload,
    }


def upsert_points(
    client: httpx.Client,
    qdrant_url: str,
    collection: str,
    points: list[dict[str, Any]],
    batch_size: int,
) -> None:
    endpoint = f"{qdrant_url.rstrip('/')}/collections/{collection}/points?wait=true"
    for i in range(0, len(points), batch_size):
        batch = points[i : i + batch_size]
        payload = {"points": batch}
        response = client.put(endpoint, json=payload, timeout=120.0)
        if response.status_code not in (200, 201):
            raise RuntimeError(f"Qdrant upsert failed: {response.status_code} {response.text}")


def discover_files(source_root: Path) -> list[Path]:
    files: list[Path] = []
    for path in source_root.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in SUPPORTED_EXTENSIONS:
            continue
        files.append(path)
    files.sort()
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description="Ingest knowledge base documents into Qdrant")
    parser.add_argument("--source", default="../raw/pharmacy", help="Path to source documents directory")
    parser.add_argument("--collection", default="knowledge_base", help="Qdrant collection name")
    parser.add_argument("--ollama-url", default="http://localhost:11434")
    parser.add_argument("--qdrant-url", default="http://localhost:6333")
    parser.add_argument("--embedding-model", default="bge-m3")
    parser.add_argument("--chunk-size", type=int, default=1400)
    parser.add_argument("--chunk-overlap", type=int, default=200)
    parser.add_argument("--batch-size", type=int, default=64)
    parser.add_argument("--recreate", action="store_true", help="Recreate collection before upload")
    parser.add_argument("--dry-run", action="store_true", help="Process files without uploading to Qdrant")
    args = parser.parse_args()

    source_root = Path(args.source).resolve()
    if not source_root.exists() or not source_root.is_dir():
        print(f"[ERROR] Source directory does not exist: {source_root}")
        sys.exit(1)

    discovered = discover_files(source_root)
    if not discovered:
        print(f"[WARN] No supported files found in: {source_root}")
        print(f"Supported extensions: {', '.join(sorted(SUPPORTED_EXTENSIONS))}")
        sys.exit(0)

    documents: list[LoadedDocument] = []
    skipped_no_text = 0
    failed_files: list[tuple[Path, str]] = []

    print(f"[INFO] Source: {source_root}")
    print(f"[INFO] Files discovered: {len(discovered)}")

    for file_path in tqdm(discovered, desc="Loading documents", unit="file"):
        try:
            loaded = load_document(file_path, source_root)
            if loaded is None:
                skipped_no_text += 1
                continue
            documents.append(loaded)
        except Exception as ex:  # noqa: BLE001
            failed_files.append((file_path, str(ex)))

    chunks = build_chunks(documents, chunk_size=args.chunk_size, overlap=args.chunk_overlap)
    if not chunks:
        print("[WARN] No chunks generated. Nothing to ingest.")
        if failed_files:
            print("[INFO] Failed files:")
            for path, reason in failed_files:
                print(f"  - {path}: {reason}")
        sys.exit(0)

    print(f"[INFO] Documents loaded: {len(documents)}")
    print(f"[INFO] Chunks generated: {len(chunks)}")
    print(f"[INFO] Skipped (no text): {skipped_no_text}")
    print(f"[INFO] Failed files: {len(failed_files)}")

    if args.dry_run:
        preview = {
            "chunk_id": chunks[0].id,
            "source_file": chunks[0].source_file,
            "section": chunks[0].section,
            "content_preview": chunks[0].content[:220],
        }
        print("[DRY RUN] First chunk preview:")
        print(json.dumps(preview, ensure_ascii=False, indent=2))
        sys.exit(0)

    now_iso = datetime.now(timezone.utc).isoformat()

    with httpx.Client() as client:
        points: list[dict[str, Any]] = []
        vector_size: int | None = None

        for chunk in tqdm(chunks, desc="Embedding chunks", unit="chunk"):
            vector = embed_text(client, args.ollama_url, args.embedding_model, chunk.content)
            if vector_size is None:
                vector_size = len(vector)
                ensure_collection(
                    client=client,
                    qdrant_url=args.qdrant_url,
                    collection=args.collection,
                    vector_size=vector_size,
                    recreate=args.recreate,
                )

            if len(vector) != vector_size:
                raise RuntimeError(
                    f"Vector size mismatch for {chunk.source_file}: "
                    f"expected {vector_size}, got {len(vector)}"
                )

            points.append(to_qdrant_point(chunk, vector, now_iso))

        upsert_points(
            client=client,
            qdrant_url=args.qdrant_url,
            collection=args.collection,
            points=points,
            batch_size=args.batch_size,
        )

    print(f"[OK] Ingestion completed. Uploaded points: {len(chunks)}")
    if failed_files:
        print("[INFO] Failed files:")
        for path, reason in failed_files:
            print(f"  - {path}: {reason}")


if __name__ == "__main__":
    main()
