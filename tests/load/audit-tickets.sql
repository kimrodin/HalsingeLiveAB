SELECT COALESCE(json_agg(row_to_json(t)), '[]'::json) FROM "Tickets" t;
