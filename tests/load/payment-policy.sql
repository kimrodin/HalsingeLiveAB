\connect payments
INSERT INTO "SimulationSettings" ("Id", "Outcome", "DelayMs", "TimeoutMs")
VALUES (1, :'payment_outcome', :payment_delay, :payment_timeout)
ON CONFLICT ("Id") DO UPDATE SET "Outcome" = EXCLUDED."Outcome",
  "DelayMs" = EXCLUDED."DelayMs", "TimeoutMs" = EXCLUDED."TimeoutMs";
