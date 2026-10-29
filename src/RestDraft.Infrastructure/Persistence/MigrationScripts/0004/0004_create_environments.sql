CREATE TABLE environments (
    id       TEXT    NOT NULL PRIMARY KEY,
    name     TEXT    NOT NULL,
    position INTEGER NOT NULL
);

CREATE TABLE environment_variables (
    environment_id TEXT    NOT NULL REFERENCES environments (id),
    position       INTEGER NOT NULL,
    name           TEXT    NOT NULL,
    is_secret      INTEGER NOT NULL,
    value          TEXT,
    secret_value   TEXT,
    PRIMARY KEY (environment_id, position)
);
