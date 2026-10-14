-- Testdata för EventCatalog (PostgreSQL), inlagd i beroendeordning:
-- Venues -> Sections -> Events -> EventSectionPrice, därefter Seats.
-- SeatingType: 0 = GeneralAdmission (fri placering), 1 = AssignedSeating (numrerade stolar).
--
-- Reglerna i API:t är uppfyllda:
--  * Kapacitet > 0 (även som CHECK-constraint i databasen)
--  * Summan av sektionernas kapacitet <= scenens kapacitet
--  * Stolar finns bara i AssignedSeating-sektioner och antalet = sektionens kapacitet
--  * Slut > start och OnSaleFrom <= start
--
-- Kör reset.sql först om databasen redan innehåller data.

BEGIN;

-- ---------------------------------------------------------------
-- 1. Scener (Venues)
-- ---------------------------------------------------------------
INSERT INTO "Venues" ("VenueId", "VenueName", "VenueAddress", "VenueCapacity") VALUES
  ('a1000000-0000-4000-8000-000000000001', 'Hälsinge Arena',  'Arenavägen 1, 824 30 Hudiksvall',   1200),
  ('a1000000-0000-4000-8000-000000000002', 'Stadsteatern',    'Teatergatan 5, 826 30 Söderhamn',    400),
  ('a1000000-0000-4000-8000-000000000003', 'Klubb Rosen',     'Storgatan 12, 824 40 Hudiksvall',    300),
  ('a1000000-0000-4000-8000-000000000004', 'Gamla Bryggan',   'Hamngatan 3, 824 31 Hudiksvall',     100);  -- inga event, för att testa radering

-- ---------------------------------------------------------------
-- 2. Sektioner (Sections)
-- ---------------------------------------------------------------
INSERT INTO "Sections" ("SectionId", "VenueId", "Name", "SectionCapacity", "SeatingType") VALUES
  -- Hälsinge Arena: 200 + 100 + 600 = 900 av 1200
  ('b1000000-0000-4000-8000-000000000001', 'a1000000-0000-4000-8000-000000000001', 'Parkett',   200, 1),
  ('b1000000-0000-4000-8000-000000000002', 'a1000000-0000-4000-8000-000000000001', 'Balkong',   100, 1),
  ('b1000000-0000-4000-8000-000000000003', 'a1000000-0000-4000-8000-000000000001', 'Ståplats',  600, 0),
  -- Stadsteatern: 250 + 100 = 350 av 400
  ('b1000000-0000-4000-8000-000000000004', 'a1000000-0000-4000-8000-000000000002', 'Parkett',   250, 1),
  ('b1000000-0000-4000-8000-000000000005', 'a1000000-0000-4000-8000-000000000002', 'Läktare',   100, 0),
  -- Klubb Rosen: 250 av 300
  ('b1000000-0000-4000-8000-000000000006', 'a1000000-0000-4000-8000-000000000003', 'Golv',      250, 0),
  -- Gamla Bryggan: 80 av 100
  ('b1000000-0000-4000-8000-000000000007', 'a1000000-0000-4000-8000-000000000004', 'Bryggan',    80, 0);

-- ---------------------------------------------------------------
-- 3. Event (Events)
-- ---------------------------------------------------------------
INSERT INTO "Events" ("EventId", "VenueId", "Name", "Description", "StartDate", "EndDate", "OnSaleFrom") VALUES
  ('c1000000-0000-4000-8000-000000000001', 'a1000000-0000-4000-8000-000000000001',
   'Vinterkonsert 2027', 'Årets stora vinterkonsert med lokala artister.',
   '2027-02-12 19:00:00+01', '2027-02-12 22:30:00+01', '2026-11-01 10:00:00+01'),
  ('c1000000-0000-4000-8000-000000000002', 'a1000000-0000-4000-8000-000000000001',
   'Rockkväll i Arenan', 'Tre band, ståplats på golvet och sittplats på läktaren.',
   '2027-04-03 20:00:00+02', '2027-04-04 00:30:00+02', '2026-12-01 10:00:00+01'),
  ('c1000000-0000-4000-8000-000000000003', 'a1000000-0000-4000-8000-000000000002',
   'Teaterföreställningen Hälsingland', 'En ny pjäs om livet i Hälsingland.',
   '2027-03-20 18:00:00+01', '2027-03-20 20:30:00+01', '2026-11-15 09:00:00+01'),
  ('c1000000-0000-4000-8000-000000000004', 'a1000000-0000-4000-8000-000000000003',
   'DJ-natt på Rosen', 'Klubbkväll med gästande DJ:s.',
   '2027-05-15 22:00:00+02', '2027-05-16 03:00:00+02', '2027-02-01 12:00:00+01'),
  ('c1000000-0000-4000-8000-000000000005', 'a1000000-0000-4000-8000-000000000002',
   'Stand up-turné', 'Humorkväll med flera komiker.',
   '2027-06-11 19:30:00+02', '2027-06-11 22:00:00+02', '2027-03-01 10:00:00+01');

-- ---------------------------------------------------------------
-- 4. Koppling mellan event, sektioner och pris (EventSectionPrice)
--    Varje sektion måste tillhöra eventets scen.
-- ---------------------------------------------------------------
INSERT INTO "EventSectionPrice" ("EventId", "SectionId", "Price") VALUES
  -- Vinterkonsert 2027 (Hälsinge Arena)
  ('c1000000-0000-4000-8000-000000000001', 'b1000000-0000-4000-8000-000000000001', 450.00),
  ('c1000000-0000-4000-8000-000000000001', 'b1000000-0000-4000-8000-000000000002', 350.00),
  ('c1000000-0000-4000-8000-000000000001', 'b1000000-0000-4000-8000-000000000003', 250.00),
  -- Rockkväll i Arenan (bara Balkong och Ståplats)
  ('c1000000-0000-4000-8000-000000000002', 'b1000000-0000-4000-8000-000000000002', 395.00),
  ('c1000000-0000-4000-8000-000000000002', 'b1000000-0000-4000-8000-000000000003', 295.00),
  -- Teaterföreställningen (Stadsteatern)
  ('c1000000-0000-4000-8000-000000000003', 'b1000000-0000-4000-8000-000000000004', 320.00),
  ('c1000000-0000-4000-8000-000000000003', 'b1000000-0000-4000-8000-000000000005', 220.00),
  -- DJ-natt på Rosen (Klubb Rosen)
  ('c1000000-0000-4000-8000-000000000004', 'b1000000-0000-4000-8000-000000000006', 180.00),
  -- Stand up-turné (Stadsteatern)
  ('c1000000-0000-4000-8000-000000000005', 'b1000000-0000-4000-8000-000000000004', 275.00),
  ('c1000000-0000-4000-8000-000000000005', 'b1000000-0000-4000-8000-000000000005', 195.00);

-- ---------------------------------------------------------------
-- 5. Stolar (Seats), bara för AssignedSeating-sektioner
--    Antal stolar = rader * stolar per rad = sektionens kapacitet.
--    Rader namnges A, B, C ... och stolnummer börjar på 1.
-- ---------------------------------------------------------------
INSERT INTO "Seats" ("SeatId", "SectionId", "Row", "Number")
SELECT gen_random_uuid(), s.section_id, chr(64 + r), n
FROM (VALUES
  ('b1000000-0000-4000-8000-000000000001'::uuid, 10, 20),  -- Parkett, Hälsinge Arena: 200
  ('b1000000-0000-4000-8000-000000000002'::uuid,  5, 20),  -- Balkong, Hälsinge Arena: 100
  ('b1000000-0000-4000-8000-000000000004'::uuid, 10, 25)   -- Parkett, Stadsteatern:   250
) AS s(section_id, row_count, seats_per_row)
CROSS JOIN LATERAL generate_series(1, s.row_count) AS r
CROSS JOIN LATERAL generate_series(1, s.seats_per_row) AS n;

COMMIT;

-- Kontroll: stolar per sektion ska matcha kapaciteten för AssignedSeating.
-- SELECT sec."Name", sec."SectionCapacity", sec."SeatingType", COUNT(se."SeatId") AS seats
-- FROM "Sections" sec LEFT JOIN "Seats" se ON se."SectionId" = sec."SectionId"
-- GROUP BY sec."SectionId" ORDER BY sec."VenueId", sec."Name";