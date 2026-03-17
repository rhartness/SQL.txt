# Documentation Standards

## Overview

As code is generated and built, documentation must be kept current. All application-specific documentation must follow these standards.

## Documentation Structure

### 1. Getting Started

Every user-facing document (application docs, sample guides) must include a **Getting Started** section that covers:

- Prerequisites (e.g., .NET 8 SDK, writable directory)
- Installation or build steps
- Minimal "hello world" example (create database, create table, insert, select)
- Next steps or links to deeper documentation

### 2. Public API Documentation

All public API functionality must be documented with:

- **Purpose** — What the type or member does
- **Parameters** — Meaning, constraints, and valid values
- **Return value** — Structure and meaning
- **Exceptions** — Conditions that cause failure
- **Examples** — Minimal usage examples where helpful

Use XML documentation comments (`///`) in C# for API docs. Generated docs (e.g., DocFX) should reflect these.

### 3. CLI Usage

CLI documentation must include:

- **Command reference** — Each command with syntax, options, and examples
- **Common workflows** — Create db, run script, query, inspect
- **Exit codes** — Success and error conditions
- **Environment variables** — If any affect behavior

### 4. Feature Documentation

Documentation must be organized by implemented features:

- **Per-feature sections** — Each feature (e.g., CREATE TABLE, INSERT, indexes) has its own section
- **Usage of related features** — How features work together (e.g., FK with tables, indexes with SELECT)
- **Examples** — End-to-end examples showing feature combinations

## When to Update

- **On feature completion** — Update Getting Started, API docs, CLI reference, and feature docs
- **On API change** — Update XML comments and generated docs
- **On CLI change** — Update CLI reference and examples

## Documentation Locations

| Document Type | Location |
|---------------|----------|
| Getting Started | `docs/getting-started.md` |
| API Reference | XML in source; generated output in `docs/api/` (when tooling added) |
| CLI Reference | `docs/cli-reference.md` |
| Feature Docs | `docs/features/` or inline in specifications |
| Sample Database | `docs/samples/wiki-database.md` |
