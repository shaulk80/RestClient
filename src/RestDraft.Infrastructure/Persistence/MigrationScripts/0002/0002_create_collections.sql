CREATE TABLE collections (
    id       TEXT    NOT NULL PRIMARY KEY,
    name     TEXT    NOT NULL,
    position INTEGER NOT NULL
);

CREATE TABLE folders (
    id               TEXT    NOT NULL PRIMARY KEY,
    collection_id    TEXT    NOT NULL REFERENCES collections (id),
    parent_folder_id TEXT             REFERENCES folders (id),
    name             TEXT    NOT NULL,
    position         INTEGER NOT NULL
);

CREATE INDEX ix_folders_collection_id ON folders (collection_id);

CREATE TABLE requests (
    id        TEXT    NOT NULL PRIMARY KEY,
    folder_id TEXT    NOT NULL REFERENCES folders (id),
    data      TEXT    NOT NULL,
    position  INTEGER NOT NULL
);

CREATE INDEX ix_requests_folder_id ON requests (folder_id);
