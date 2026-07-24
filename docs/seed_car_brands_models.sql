/* ============================================================
   RemSolution — reference data seed: car brands + models
   Idempotent / re-runnable: inserts only rows that are missing.
   Convention preserved: Name + CreatedBy + CreatedOn, Id auto-generated.
   ============================================================ */
SET NOCOUNT ON;

/* ---------------- Brands ---------------- */
INSERT INTO dbo.Brands (Name, CreatedBy, CreatedOn)
SELECT v.Name, 'System', SYSDATETIMEOFFSET()
FROM (VALUES
    (N'Toyota'),
    (N'Honda'),
    (N'Ford'),
    (N'BMW'),
    (N'Mercedes-Benz'),
    (N'Audi'),
    (N'Volkswagen'),
    (N'Nissan'),
    (N'Hyundai'),
    (N'Kia'),
    (N'Renault'),
    (N'Peugeot'),
    (N'Citroën'),
    (N'Dacia'),
    (N'Fiat'),
    (N'Volvo'),
    (N'Mazda'),
    (N'Škoda'),
    (N'SEAT'),
    (N'Opel'),
    (N'Suzuki'),
    (N'Mitsubishi'),
    (N'Jeep'),
    (N'Land Rover'),
    (N'Tesla'),
    (N'Chevrolet'),
    (N'MINI'),
    (N'Porsche')
) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brands b WHERE b.Name = v.Name);

/* ---------------- Models ---------------- */
INSERT INTO dbo.ModelCars (Name, BrandId, CreatedBy, CreatedOn)
SELECT v.ModelName, b.Id, 'System', SYSDATETIMEOFFSET()
FROM (VALUES
    -- Toyota
    (N'Toyota', N'Corolla'),
    (N'Toyota', N'Camry'),
    (N'Toyota', N'Yaris'),
    (N'Toyota', N'RAV4'),
    (N'Toyota', N'Hilux'),
    (N'Toyota', N'Land Cruiser'),
    (N'Toyota', N'C-HR'),
    -- Honda
    (N'Honda', N'Civic'),
    (N'Honda', N'Accord'),
    (N'Honda', N'CR-V'),
    (N'Honda', N'HR-V'),
    (N'Honda', N'Jazz'),
    -- Ford
    (N'Ford', N'Fiesta'),
    (N'Ford', N'Focus'),
    (N'Ford', N'Mondeo'),
    (N'Ford', N'Kuga'),
    (N'Ford', N'Puma'),
    (N'Ford', N'Ranger'),
    (N'Ford', N'Mustang'),
    -- BMW
    (N'BMW', N'1 Series'),
    (N'BMW', N'3 Series'),
    (N'BMW', N'5 Series'),
    (N'BMW', N'X1'),
    (N'BMW', N'X3'),
    (N'BMW', N'X5'),
    -- Mercedes-Benz
    (N'Mercedes-Benz', N'A-Class'),
    (N'Mercedes-Benz', N'C-Class'),
    (N'Mercedes-Benz', N'E-Class'),
    (N'Mercedes-Benz', N'GLA'),
    (N'Mercedes-Benz', N'GLC'),
    (N'Mercedes-Benz', N'GLE'),
    (N'Mercedes-Benz', N'Vito'),
    -- Audi
    (N'Audi', N'A1'),
    (N'Audi', N'A3'),
    (N'Audi', N'A4'),
    (N'Audi', N'A6'),
    (N'Audi', N'Q3'),
    (N'Audi', N'Q5'),
    (N'Audi', N'Q7'),
    -- Volkswagen
    (N'Volkswagen', N'Polo'),
    (N'Volkswagen', N'Golf'),
    (N'Volkswagen', N'Passat'),
    (N'Volkswagen', N'Tiguan'),
    (N'Volkswagen', N'T-Roc'),
    (N'Volkswagen', N'Touareg'),
    -- Nissan
    (N'Nissan', N'Micra'),
    (N'Nissan', N'Juke'),
    (N'Nissan', N'Qashqai'),
    (N'Nissan', N'X-Trail'),
    (N'Nissan', N'Navara'),
    -- Hyundai
    (N'Hyundai', N'i10'),
    (N'Hyundai', N'i20'),
    (N'Hyundai', N'i30'),
    (N'Hyundai', N'Tucson'),
    (N'Hyundai', N'Santa Fe'),
    (N'Hyundai', N'Kona'),
    -- Kia
    (N'Kia', N'Picanto'),
    (N'Kia', N'Rio'),
    (N'Kia', N'Ceed'),
    (N'Kia', N'Sportage'),
    (N'Kia', N'Sorento'),
    (N'Kia', N'Stonic'),
    -- Renault
    (N'Renault', N'Clio'),
    (N'Renault', N'Mégane'),
    (N'Renault', N'Captur'),
    (N'Renault', N'Kadjar'),
    (N'Renault', N'Scénic'),
    (N'Renault', N'Talisman'),
    (N'Renault', N'Kangoo'),
    -- Peugeot
    (N'Peugeot', N'108'),
    (N'Peugeot', N'208'),
    (N'Peugeot', N'308'),
    (N'Peugeot', N'2008'),
    (N'Peugeot', N'3008'),
    (N'Peugeot', N'5008'),
    (N'Peugeot', N'508'),
    -- Citroën
    (N'Citroën', N'C1'),
    (N'Citroën', N'C3'),
    (N'Citroën', N'C4'),
    (N'Citroën', N'C5 Aircross'),
    (N'Citroën', N'Berlingo'),
    -- Dacia
    (N'Dacia', N'Sandero'),
    (N'Dacia', N'Logan'),
    (N'Dacia', N'Duster'),
    (N'Dacia', N'Lodgy'),
    (N'Dacia', N'Dokker'),
    (N'Dacia', N'Jogger'),
    -- Fiat
    (N'Fiat', N'500'),
    (N'Fiat', N'Panda'),
    (N'Fiat', N'Tipo'),
    (N'Fiat', N'Punto'),
    (N'Fiat', N'Doblò'),
    -- Volvo
    (N'Volvo', N'XC40'),
    (N'Volvo', N'XC60'),
    (N'Volvo', N'XC90'),
    (N'Volvo', N'S60'),
    (N'Volvo', N'V40'),
    -- Mazda
    (N'Mazda', N'Mazda2'),
    (N'Mazda', N'Mazda3'),
    (N'Mazda', N'Mazda6'),
    (N'Mazda', N'CX-3'),
    (N'Mazda', N'CX-5'),
    (N'Mazda', N'CX-30'),
    -- Škoda
    (N'Škoda', N'Fabia'),
    (N'Škoda', N'Octavia'),
    (N'Škoda', N'Superb'),
    (N'Škoda', N'Kamiq'),
    (N'Škoda', N'Karoq'),
    (N'Škoda', N'Kodiaq'),
    -- SEAT
    (N'SEAT', N'Ibiza'),
    (N'SEAT', N'Leon'),
    (N'SEAT', N'Arona'),
    (N'SEAT', N'Ateca'),
    (N'SEAT', N'Tarraco'),
    -- Opel
    (N'Opel', N'Corsa'),
    (N'Opel', N'Astra'),
    (N'Opel', N'Insignia'),
    (N'Opel', N'Crossland'),
    (N'Opel', N'Grandland'),
    (N'Opel', N'Mokka'),
    -- Suzuki
    (N'Suzuki', N'Swift'),
    (N'Suzuki', N'Baleno'),
    (N'Suzuki', N'Vitara'),
    (N'Suzuki', N'S-Cross'),
    (N'Suzuki', N'Jimny'),
    -- Mitsubishi
    (N'Mitsubishi', N'Space Star'),
    (N'Mitsubishi', N'ASX'),
    (N'Mitsubishi', N'Outlander'),
    (N'Mitsubishi', N'Eclipse Cross'),
    (N'Mitsubishi', N'L200'),
    -- Jeep
    (N'Jeep', N'Renegade'),
    (N'Jeep', N'Compass'),
    (N'Jeep', N'Cherokee'),
    (N'Jeep', N'Grand Cherokee'),
    (N'Jeep', N'Wrangler'),
    -- Land Rover
    (N'Land Rover', N'Defender'),
    (N'Land Rover', N'Discovery'),
    (N'Land Rover', N'Range Rover'),
    (N'Land Rover', N'Range Rover Sport'),
    (N'Land Rover', N'Range Rover Evoque'),
    -- Tesla
    (N'Tesla', N'Model 3'),
    (N'Tesla', N'Model Y'),
    (N'Tesla', N'Model S'),
    (N'Tesla', N'Model X'),
    -- Chevrolet
    (N'Chevrolet', N'Spark'),
    (N'Chevrolet', N'Aveo'),
    (N'Chevrolet', N'Cruze'),
    (N'Chevrolet', N'Malibu'),
    (N'Chevrolet', N'Captiva'),
    -- MINI
    (N'MINI', N'Cooper'),
    (N'MINI', N'Countryman'),
    (N'MINI', N'Clubman'),
    -- Porsche
    (N'Porsche', N'911'),
    (N'Porsche', N'Cayenne'),
    (N'Porsche', N'Macan'),
    (N'Porsche', N'Panamera')
) v(BrandName, ModelName)
JOIN dbo.Brands b ON b.Name = v.BrandName
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ModelCars m
    WHERE m.BrandId = b.Id AND m.Name = v.ModelName
);
