-- 1. Κατηγορίες
CREATE TABLE IF NOT EXISTS Categories (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    ImageURL TEXT NULL,
    ParentId TEXT NULL,
    FOREIGN KEY (ParentId) REFERENCES Categories(Id) ON DELETE SET NULL
);

-- 2. Σούπερ Μάρκετ (το όνομα είναι το Primary Key, case-insensitive)
CREATE TABLE IF NOT EXISTS Supermarkets (
    Name TEXT PRIMARY KEY COLLATE NOCASE,
    LogoURL TEXT NULL
);

-- 3. Προϊόντα
CREATE TABLE IF NOT EXISTS Products (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    MinimumPrice DECIMAL(10, 2) NOT NULL,
    CategoryId TEXT NULL,
    ImageURL TEXT NULL,
    ProductBio TEXT NULL,
    Size DECIMAL(10, 2) NULL,
    FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
);

-- 4. Τιμές ανά Κατάστημα
CREATE TABLE IF NOT EXISTS ProductPrices (
    ProductId TEXT NOT NULL,
    RetailerName TEXT NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    UnitPrice DECIMAL(10, 2) NULL,
    UpdatedAt TEXT NULL,
    PRIMARY KEY (ProductId, RetailerName),
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    FOREIGN KEY (RetailerName) REFERENCES Supermarkets(Name) ON DELETE CASCADE
);

-- Indexes speed up SELECT queries and JOINs by using B-Trees instead of full table scans.
-- They target foreign keys (ParentId, CategoryId, RetailerName) for instant UI filtering.
-- Slight trade-off: minimal disk overhead and slower bulk inserts during scraping.
-- Ευρετήρια για άμεση απόκριση
CREATE INDEX IF NOT EXISTS IX_Categories_ParentId ON Categories(ParentId);
CREATE INDEX IF NOT EXISTS IX_Products_CategoryId ON Products(CategoryId);
CREATE INDEX IF NOT EXISTS IX_ProductPrices_RetailerName ON ProductPrices(RetailerName);

/* Update supermarket chains with verified local image paths */
INSERT OR REPLACE INTO Supermarkets (Name, LogoURL) VALUES
('Σκλαβενίτης', '/logos/sklavenitis.png'),
('ΑΒ Βασιλόπουλος', '/logos/ab.png'),
('Lidl', '/logos/lidl.png'),
('My market', '/logos/mymarket.jpg'),
('Μασούτης', '/logos/masoutis.webp'),
('Γαλαξίας', '/logos/galaxias.png'),
('Κρητικός', '/logos/kritikos.jpg'),
('Bazaar', '/logos/bazaar.png'),
('Χαλκιαδάκης', '/logos/chalkiadakis.png'),
('Synka', '/logos/synka.jpg');