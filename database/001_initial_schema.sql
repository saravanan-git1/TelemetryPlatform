CREATE TABLE IF NOT EXISTS telemetry_readings (
    id BIGSERIAL PRIMARY KEY,
    machine_id TEXT NOT NULL,
    observed_at TIMESTAMPTZ NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_telemetry_readings_machine_observed UNIQUE (machine_id, observed_at)
);

CREATE INDEX IF NOT EXISTS ix_telemetry_readings_machine_observed
    ON telemetry_readings (machine_id, observed_at DESC);

CREATE TABLE IF NOT EXISTS machine_poll_tracker (
    machine_id TEXT PRIMARY KEY,
    last_polled_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
