-- One statement keeps each database snapshot internally consistent.
SELECT json_build_object(
  'inventory', (SELECT row_to_json(i) FROM "EventInventory" i
    WHERE "InventoryId" = '74000000-0000-0000-0000-000000000001'),
  'bookings', COALESCE((SELECT json_agg(row_to_json(b)) FROM (
    SELECT b."BookingId", b."Status", b."CustomerEmail", b."PaymentRequested", b."PaymentSettled",
      b."ExpiresAt", i."BookingItemId", i."Quantity", i."UnitPrice", i."Currency"
    FROM "Bookings" b JOIN "BookingItems" i USING ("BookingId")
    WHERE i."InventoryId" = '74000000-0000-0000-0000-000000000001'
  ) b), '[]'::json),
  'warmupBookingIds', COALESCE((SELECT json_agg("BookingId") FROM "BookingItems"
    WHERE "InventoryId" = '74000000-0000-0000-0000-000000000002'), '[]'::json),
  'pendingOutbox',
    (SELECT count(*) FROM "PaymentRequestOutboxMessages" WHERE "DeliveredAtUtc" IS NULL AND "SuppressedAtUtc" IS NULL)
    + (SELECT count(*) FROM "TicketIssueOutboxMessages" WHERE "DeliveredAtUtc" IS NULL),
  'pendingInbox', (SELECT count(*) FROM "PaymentResultInboxMessages" WHERE "ProcessedAtUtc" IS NULL AND "RejectedAtUtc" IS NULL),
  'rejectedInbox', (SELECT count(*) FROM "PaymentResultInboxMessages" WHERE "RejectedAtUtc" IS NOT NULL)
);
