# Personal Agentic OS — Memory Layer Architecture

## Goal

Build the Memory Layer first because memory is the foundation of:
- continuity
- personalization
- trust
- leverage
- contextual intelligence

The first milestone is:

> “The system can save a memory, link it to a person/project/commitment, embed it, retrieve it semantically, and show its source.”

---

# Docker Infrastructure

## 1. PostgreSQL + pgvector

Primary structured memory database.

### Purpose
- stores structured memory
- relational querying
- vector embeddings
- semantic search
- auditability

### Docker

```yaml
postgres:
  image: pgvector/pgvector:pg16
  container_name: memory_postgres
  environment:
    POSTGRES_DB: agentic_os
    POSTGRES_USER: agent
    POSTGRES_PASSWORD: agent_password
  ports:
    - "5432:5432"
  volumes:
    - postgres_data:/var/lib/postgresql/data
```

### Responsibilities
- user identity
- people
- projects
- commitments
- decisions
- embeddings
- memory metadata

---

## 2. Redis

Temporary memory and queues.

### Purpose
- short-term memory
- job queues
- cache
- agent state
- rate limiting

### Docker

```yaml
redis:
  image: redis:7
  container_name: memory_redis
  ports:
    - "6379:6379"
  volumes:
    - redis_data:/data
```

---

## 3. MinIO

Object storage for raw files and evidence.

### Purpose
- PDFs
- screenshots
- transcripts
- raw emails
- recordings
- source evidence

### Docker

```yaml
minio:
  image: minio/minio
  container_name: memory_minio
  command: server /data --console-address ":9001"
  environment:
    MINIO_ROOT_USER: minio
    MINIO_ROOT_PASSWORD: minio_password
  ports:
    - "9000:9000"
    - "9001:9001"
  volumes:
    - minio_data:/data
```

---

## 4. Memory API (RestAPI)

Core backend service.

### Purpose
- memory ingestion
- retrieval
- embeddings
- orchestration
- linking entities

### Docker

```yaml
memory_api:
  build: ./services/memory-api
  container_name: memory_api
  depends_on:
    - postgres
    - redis
    - minio
  ports:
    - "8000:8000"
  env_file:
    - .env
```

---

## 5. Memory Worker

Background processing.

### Purpose
- embeddings
- summarization
- extraction
- deduplication
- classification
- indexing

### Docker

```yaml
memory_worker:
  build: ./services/memory-worker
  container_name: memory_worker
  depends_on:
    - postgres
    - redis
    - minio
  env_file:
    - .env
```

---

# Memory Features

# 1. Identity Memory

Stores who the user is.

## Data
- goals
- values
- preferences
- habits
- timezone
- routines
- communication style

## User Stories
- As a user, I want the system to remember my preferences so I do not repeat myself.
- As a user, I want the system to understand my priorities.
- As a user, I want to edit or delete memories about me.

## Tables
- user_profiles
- user_preferences
- user_goals
- user_values
- user_habits

---

# 2. People Memory

Relationship intelligence.

## Data
- people
- organizations
- interactions
- promises
- communication patterns
- relationship strength

## User Stories
- As a user, I want to know when I last spoke to someone.
- As a user, I want the system to remember promises I made.
- As a user, I want suggested follow-ups.

## Tables
- people
- organizations
- relationships
- interactions
- person_facts
- promises

---

# 3. Project Memory

Project-specific context.

## Data
- goals
- blockers
- stakeholders
- deadlines
- files
- updates
- decisions

## User Stories
- As a user, I want each project to maintain persistent context.
- As a user, I want to know what is blocked.
- As a user, I want automatic project summaries.

## Tables
- projects
- project_members
- project_events
- project_blockers
- project_files
- project_status_updates

---

# 4. Commitment Memory

Tracks promises and obligations.

## Data
- promise
- owner
- due date
- status
- source
- confidence

## User Stories
- As a user, I want the system to detect commitments automatically.
- As a user, I want reminders before commitments become overdue.
- As a user, I want to review all open commitments.

## Tables
- commitments
- commitment_sources
- commitment_status_history

---

# 5. Episodic Memory

Stores events and interactions.

## Data
- meetings
- chats
- uploads
- calls
- emails
- timestamps
- participants

## User Stories
- As a user, I want the system to remember what happened yesterday.
- As a user, I want timelines for projects and people.
- As a user, I want linked summaries and evidence.

## Tables
- events
- event_participants
- event_sources
- event_summaries

---

# 6. Semantic Memory

Stores factual knowledge.

## Data
- preferences
- deadlines
- relationships
- known facts
- system inferences

## User Stories
- As a user, I want factual recall across all workflows.
- As a user, I want confidence scoring for facts.
- As a user, I want conflicting facts detected.

## Tables
- facts
- fact_sources
- fact_conflicts
- fact_confidence_history

---

# 7. Procedural Memory

Learns repeated workflows.

## Data
- workflow steps
- repeated actions
- automation patterns
- execution history

## User Stories
- As a user, I want the OS to learn repeated tasks.
- As a user, I want reusable procedures.
- As a user, I want automation suggestions.

## Tables
- procedures
- procedure_steps
- procedure_runs
- procedure_feedback

---

# 8. Decision Memory

Tracks important decisions.

## Data
- assumptions
- options
- reasoning
- outcomes
- revisit dates

## User Stories
- As a user, I want to remember why I made decisions.
- As a user, I want future contradiction warnings.
- As a user, I want strategic review history.

## Tables
- decisions
- decision_options
- decision_assumptions
- decision_outcomes

---

# 9. Source & Evidence System

Every memory requires proof.

## Data
- raw source
- extraction method
- confidence
- timestamps
- audit logs

## User Stories
- As a user, I want transparent memory provenance.
- As a user, I want correction and rollback capability.
- As a user, I want hallucination-resistant memory.

## Tables
- sources
- source_chunks
- memory_sources
- audit_logs

---

# 10. Memory Search

Two search modes:

## Keyword Search
For exact recall:
- names
- dates
- project names
- emails

## Semantic Search
For conceptual recall:
- “What project is blocked?”
- “Who needs follow-up?”
- “What was discussed about taxes?”

## User Stories
- As a user, I want natural-language memory retrieval.
- As a user, I want grouped contextual results.
- As a user, I want linked evidence for every result.

## Tables
- memory_embeddings
- search_logs
- retrieval_feedback

---

# MVP Build Order

## Phase 1
1. PostgreSQL + pgvector
2. Redis
3. MinIO
4. Memory API
5. Memory Worker

## Phase 2
6. Identity Memory
7. People Memory
8. Project Memory
9. Commitment Memory
10. Source/Evidence System

## Phase 3
11. Semantic Search
12. Procedural Memory
13. Decision Journal

---

# First Production Goal

The system should be able to:

1. ingest memory
2. classify memory
3. link memory to entities
4. embed memory
5. retrieve semantically
6. show evidence
7. maintain audit history

This is the foundation of the Personal Agentic OS.
