CREATE TABLE history_entries (
    id             TEXT    NOT NULL PRIMARY KEY,
    sent_at        INTEGER NOT NULL,
    method         TEXT    NOT NULL,
    resolved_url   TEXT    NOT NULL,
    request_json   TEXT    NOT NULL,
    status_code    INTEGER,
    duration_ms    REAL    NOT NULL,
    error_message  TEXT,
    body_file_path TEXT
);

CREATE INDEX ix_history_entries_sent_at ON history_entries (sent_at);

CREATE INDEX ix_history_entries_resolved_url ON history_entries (resolved_url);
