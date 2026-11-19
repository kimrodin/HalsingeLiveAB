SELECT json_build_object('bookingCount', (SELECT count(*) FROM "Bookings"),
 'bookings', (SELECT coalesce(json_agg(json_build_object('id', "BookingId", 'status', "Status", 'expiresAt', "ExpiresAt")), '[]') FROM "Bookings"),
 'inventories', (SELECT json_agg(json_build_object('id', i."InventoryId", 'available', i."AvailableQuantity",
    'bookings', (SELECT count(*) FROM "BookingItems" b WHERE b."InventoryId" = i."InventoryId"),
    'quantity', (SELECT coalesce(sum(b."Quantity"), 0) FROM "BookingItems" b WHERE b."InventoryId" = i."InventoryId")) ORDER BY i."InventoryId") FROM "EventInventory" i));
