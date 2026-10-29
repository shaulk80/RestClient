CREATE TABLE open_tabs (
    position    INTEGER NOT NULL PRIMARY KEY,
    request_id  TEXT    NOT NULL,
    is_linked   INTEGER NOT NULL,
    is_selected INTEGER NOT NULL,
    draft       TEXT
);
