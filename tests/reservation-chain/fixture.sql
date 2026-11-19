-- Applied only after the real Booking application has run its migrations.
INSERT INTO "EventInventory" ("InventoryId", "EventId", "SectionId", "TotalQuantity", "AvailableQuantity", "Price")
SELECT ('74000000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
       ('73000000-0000-0000-0000-' || lpad(i::text, 12, '0'))::uuid,
       '72000000-0000-0000-0000-000000000001'::uuid, 100, 100, 100
FROM generate_series(1, 5) AS i;
