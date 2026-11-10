SELECT json_build_object(
  'payments', COALESCE((SELECT json_agg(row_to_json(p)) FROM "Payments" p), '[]'::json),
  'pendingOutbox', (SELECT count(*) FROM "PaymentResultOutboxMessages" WHERE "DeliveredAtUtc" IS NULL)
);
