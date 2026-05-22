CREATE TABLE IF NOT EXISTS notify_outbox (
    id              TEXT PRIMARY KEY,
    idempotency_key TEXT UNIQUE,
    channel         TEXT NOT NULL,
    template        TEXT NOT NULL,
    recipient_json  TEXT NOT NULL,
    model_json      TEXT NOT NULL,
    model_type      TEXT NOT NULL,
    queued_at       TEXT NOT NULL,
    next_retry_at   TEXT,
    claimed_at      TEXT,
    attempts        INTEGER NOT NULL DEFAULT 0,
    last_error      TEXT,
    state           TEXT NOT NULL DEFAULT 'queued'
);

CREATE INDEX IF NOT EXISTS notify_outbox_drain_idx
    ON notify_outbox(state, next_retry_at);

CREATE TABLE IF NOT EXISTS notify_dead_letter (
    id              TEXT PRIMARY KEY,
    original_id     TEXT NOT NULL,
    payload         TEXT NOT NULL,
    final_error     TEXT NOT NULL,
    dead_lettered_at TEXT NOT NULL
);
