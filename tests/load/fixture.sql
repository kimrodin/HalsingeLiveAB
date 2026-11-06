-- Runs only against this run's disposable PostgreSQL, after migrations.
\connect eventcatalog
BEGIN;
INSERT INTO "Venues" ("VenueId", "VenueName", "VenueCapacity", "VenueStreetAddress", "VenuePostalCode", "VenueCity")
VALUES ('71000000-0000-0000-0000-000000000001', 'Load test hall', 1000, 'Test street 1', '82400', 'Hudiksvall');
INSERT INTO "Sections" ("SectionId", "VenueId", "Name", "SectionCapacity", "SeatingType")
VALUES ('72000000-0000-0000-0000-000000000001', '71000000-0000-0000-0000-000000000001', 'Standing', 1000, 0);
INSERT INTO "Events" ("EventId", "VenueId", "Name", "Description", "StartDate", "EndDate", "OnSaleFrom")
VALUES ('73000000-0000-0000-0000-000000000001', '71000000-0000-0000-0000-000000000001',
        'Load test concert', 'Disposable checkout fixture',
        CURRENT_TIMESTAMP + INTERVAL '1 year', CURRENT_TIMESTAMP + INTERVAL '1 year 2 hours', CURRENT_TIMESTAMP - INTERVAL '1 day');
INSERT INTO "EventSectionPrice" ("EventId", "SectionId", "Price")
VALUES ('73000000-0000-0000-0000-000000000001', '72000000-0000-0000-0000-000000000001', 100);
COMMIT;

\connect booking
INSERT INTO "EventInventory" ("InventoryId", "EventId", "SectionId", "TotalQuantity", "AvailableQuantity", "Price")
VALUES ('74000000-0000-0000-0000-000000000001', '73000000-0000-0000-0000-000000000001',
        '72000000-0000-0000-0000-000000000001', 1000, 1000, 100);
