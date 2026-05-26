# 📚✂️ MangaBinder

[日本語READMEはこちら](README.ja.md)

> A Windows desktop application focused on preparing scanned manga archives for smartphone reading.

MangaBinder is a Windows desktop application focused on preparing scanned manga archives for comfortable smartphone reading.

Unlike manga servers or streaming-oriented library managers, MangaBinder focuses on the **pre-reading workflow**.

Examples:

- Organizing raw manga archives
- Reconstructing volume structures
- Preparing smartphone-friendly ZIP outputs
- Spread splitting and preprocessing
- Handling problematic archive structures
- Managing large personal manga collections

The project is heavily inspired by real-world self-scanned manga workflows and long-term archive management.

---

## ✨ Current Status

MangaBinder is currently under active development.

Implemented / in-progress features include:

- Manga library browsing
- Thumbnail generation
- Metadata-assisted browsing
- Volume reconstruction workflow
- Archive structure inspection
- Spread split target selection
- Spread splitting editor
- ZIP export workflow
- Background job processing
- Google Books metadata integration

---

## 🧩 Philosophy

MangaBinder is designed around practical personal workflows rather than generic media-server management.

The goal is not to build another streaming platform, but to improve the process of:

1. Collecting manga archives
2. Organizing inconsistent source materials
3. Preparing readable outputs
4. Managing large long-term manga libraries

The application is especially optimized for Japanese manga collections and Japanese filename conventions.

---

## 📦 Supported Workflow

MangaBinder is designed for workflows involving mixed archive formats such as:

- RAR
- ZIP
- EPUB
- Image folders

The application reorganizes and prepares these materials into reader-friendly outputs.

---

## ✨ Features

### 📚 Library Management

Browse manga collections with thumbnails, metadata, summaries, and author information.

### 🔍 Archive Inspection

Inspect complex archive structures before binding.

Examples:

- Nested archives
- Mixed folder structures
- Volume reconstruction issues
- Unsupported files

### ✂️ Spread Splitting Workflow

Select spread pages and configure split positions before export.

### 🧠 Metadata Support

Google Books metadata can be used to improve browsing and discoverability.

### ⚙️ Background Processing

Long-running operations such as thumbnail generation are handled through background jobs.

---

## 🛠 Technology Stack

- .NET 10
- WPF
- WpfUi
- SQLite
- Dapper
- ReactiveProperty / R3
- SharpCompress

---

## 🤖 Development Style

The project is developed through a workflow combining:

- Specification and architecture discussions with ChatGPT
- Implementation support using GitHub Copilot

The focus is on iterative UI-first development and practical usability.

---

## 🖼 Screenshots

Screenshots will be added later.

---

## 🚧 Project Status

This project is still experimental and under heavy development.

UI layouts, workflows, and internal structures may change significantly.

---

## 📄 License

This project is licensed under the Apache License 2.0.
