-- 0002_guest_sounds: the Guest sounds setting (the countdown tick and the shutter sound on the Guest Screen).
-- Every Event already saved gets the setting on, as a new Event does; the runner applies this inside one transaction.

ALTER TABLE events ADD COLUMN guest_sounds_enabled INTEGER NOT NULL DEFAULT 1;
