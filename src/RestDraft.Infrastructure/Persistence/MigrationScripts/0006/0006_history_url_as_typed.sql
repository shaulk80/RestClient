-- History used to store the URL as sent, which could contain secret values. It now stores the URL as typed,
-- with {{placeholders}} left in place (see HistoryUrl). Existing rows are rebuilt from the stored request,
-- so resolved URLs already on disk are overwritten. The request JSON uses the DTO property names.
DROP INDEX ix_history_entries_resolved_url;

ALTER TABLE history_entries RENAME COLUMN resolved_url TO url;

UPDATE history_entries
SET url = COALESCE(json_extract(request_json, '$.Url'), '')
        || COALESCE(
            (SELECT '?' || group_concat(json_extract(parameter.value, '$.Key') || '=' || json_extract(parameter.value, '$.Value'), '&')
             FROM json_each(request_json, '$.QueryParams') AS parameter
             WHERE json_extract(parameter.value, '$.IsEnabled') = 1),
            '');

CREATE INDEX ix_history_entries_url ON history_entries (url);
