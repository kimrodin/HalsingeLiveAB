-- Rensar all testdata i EventCatalog, i ordning efter främmande nycklar.
-- Tabellen "__EFMigrationsHistory" lämnas orörd: den beskriver schemat, inte datan.
-- Kör mot databasen eventcatalog_dev.

BEGIN;

-- 1. Kopplingen mellan event och sektioner (refererar Events och Sections)
DELETE FROM "EventSectionPrice";

-- 2. Event (refererar Venues)
DELETE FROM "Events";

-- 3. Stolar (refererar Sections)
DELETE FROM "Seats";

-- 4. Platskartor (refererar Venues)
DELETE FROM "VenueSeatingMaps";

-- 5. Sektioner (refererar Venues)
DELETE FROM "Sections";

-- 6. Scener
DELETE FROM "Venues";

COMMIT;