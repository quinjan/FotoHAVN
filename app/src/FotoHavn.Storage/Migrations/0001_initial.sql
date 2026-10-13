-- 0001_initial: the first schema (implementation plan, section 10). The migration runner applies this file inside one
-- transaction and records the new schema version in the same transaction, so the file holds table and index
-- statements only.
-- Times are UTC text written by StorageTimestamp (fixed width, so text order is time order); dates are yyyy-MM-dd.

CREATE TABLE events (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  event_date TEXT NOT NULL,
  venue TEXT, customer_name TEXT, notes TEXT,
  camera_device_id TEXT, camera_name TEXT,
  guest_display_id TEXT, guest_display_name TEXT,
  printer TEXT NOT NULL DEFAULT 'none' CHECK (printer = 'none'),
  mirror INTEGER NOT NULL DEFAULT 1,
  countdown_seconds INTEGER NOT NULL DEFAULT 5 CHECK (countdown_seconds IN (3, 5, 10)),
  default_template_id TEXT NOT NULL,
  default_look_id TEXT NOT NULL,
  guest_gallery_enabled INTEGER NOT NULL DEFAULT 0,
  pin_hash BLOB, pin_salt BLOB, pin_iterations INTEGER,
  created_at TEXT NOT NULL, updated_at TEXT NOT NULL,
  deleting_at TEXT
);

CREATE TABLE event_templates (
  event_id TEXT NOT NULL REFERENCES events(id),
  template_id TEXT NOT NULL,
  PRIMARY KEY (event_id, template_id)
);

CREATE TABLE event_looks (
  event_id TEXT NOT NULL REFERENCES events(id),
  look_id TEXT NOT NULL,
  PRIMARY KEY (event_id, look_id)
);

CREATE TABLE event_phase_timers (
  event_id TEXT NOT NULL REFERENCES events(id),
  phase TEXT NOT NULL CHECK (phase IN ('template', 'photographs_capture', 'photographs_review', 'filters', 'keepsake', 'guest_gallery')),
  seconds INTEGER NOT NULL CHECK (seconds >= 0),
  PRIMARY KEY (event_id, phase)
);

CREATE TABLE guest_cycles (
  id TEXT PRIMARY KEY,
  event_id TEXT NOT NULL REFERENCES events(id),
  started_at TEXT NOT NULL,
  ended_at TEXT,
  end_reason TEXT CHECK (end_reason IN ('completed', 'started_over', 'operator_exit', 'app_closed', 'unrecoverable')),
  deleting_at TEXT
);

CREATE TABLE captures (
  id TEXT PRIMARY KEY,
  guest_cycle_id TEXT NOT NULL REFERENCES guest_cycles(id),
  sequence INTEGER NOT NULL CHECK (sequence >= 1),
  file_name TEXT NOT NULL,
  byte_length INTEGER NOT NULL, sha256 TEXT NOT NULL,
  width INTEGER NOT NULL, height INTEGER NOT NULL,
  mirrored INTEGER NOT NULL,
  taken_at TEXT NOT NULL,
  state TEXT NOT NULL CHECK (state IN ('kept', 'discarded')),
  UNIQUE (guest_cycle_id, sequence)
);

CREATE TABLE photo_strips (
  guest_cycle_id TEXT PRIMARY KEY REFERENCES guest_cycles(id),
  template_id TEXT NOT NULL, look_id TEXT NOT NULL,
  arrangement TEXT NOT NULL,          -- JSON array of capture ids, one per window
  file_name TEXT NOT NULL,
  byte_length INTEGER NOT NULL, sha256 TEXT NOT NULL,
  width INTEGER NOT NULL, height INTEGER NOT NULL,
  committed_at TEXT NOT NULL
);

-- Indexes for the store's queries: the Event list order, Guest Cycle paging (keyset on started_at, id), and the
-- Captures of a Guest Cycle.
CREATE INDEX ix_events_list ON events (event_date DESC, created_at DESC);
CREATE INDEX ix_guest_cycles_paging ON guest_cycles (event_id, started_at DESC, id DESC);
CREATE INDEX ix_captures_guest_cycle ON captures (guest_cycle_id);
