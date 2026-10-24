/* Database schema for the price comparison app.

   DatabaseInitializer runs this whole file on every app start, so every statement
   must be safe to repeat: tables and indexes use IF NOT EXISTS, and the seed data at
   the bottom uses an upsert. Never use INSERT OR REPLACE here. It deletes the old row
   first, and the ON DELETE CASCADE rules below would then delete that row's prices.

   Foreign keys are switched on by DatabaseInitializer (SQLite has them off by default). */


/* Category tree. A row with no ParentId is a top-level category.
   If a parent is deleted its children become top-level instead of being deleted. */
CREATE TABLE IF NOT EXISTS Categories (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    ImageURL TEXT NULL,
    ParentId TEXT NULL,
    FOREIGN KEY (ParentId) REFERENCES Categories(Id) ON DELETE SET NULL
);


/* Supermarket chains. The name is the primary key and prices link to it.
   NOCASE makes "Lidl" and "LIDL" the same, but only for Latin letters,
   so Greek names must match exactly. */
CREATE TABLE IF NOT EXISTS Supermarkets (
    Name TEXT PRIMARY KEY COLLATE NOCASE,
    LogoURL TEXT NULL
);


/* Products.
   NameSearch is a lowercase, accent-free copy of Name that the search endpoint matches
   against. The scraper fills it using TextNormalizer.ForSearch.
   MinimumPrice is the lowest valid retailer price, calculated by the scraper.
   SQLite stores DECIMAL as a plain floating point number, MySQL will store it exactly.
   Size is not filled by the scraper yet. */
CREATE TABLE IF NOT EXISTS Products (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    NameSearch TEXT NULL,
    MinimumPrice DECIMAL(10, 2) NOT NULL,
    CategoryId TEXT NULL,
    ImageURL TEXT NULL,
    ProductBio TEXT NULL,
    Size DECIMAL(10, 2) NULL,
    FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
);


/* Price of a product at a supermarket: one row per product per supermarket.
   The composite primary key means a supermarket can have only one price for a product,
   which is what lets the scraper update prices in place instead of duplicating them.
   Deleting a product or a supermarket deletes its prices (CASCADE).
   UnitPrice and UpdatedAt are not filled by the scraper yet. */
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


/* Indexes let the database jump straight to matching rows (using a B-Tree) instead of
   reading the whole table. They cover the columns the app filters and joins on.
   The trade-off is a little disk space and slightly slower inserts while scraping.
   ProductPrices by ProductId needs no extra index, the primary key already covers it. */
CREATE INDEX IF NOT EXISTS IX_Categories_ParentId ON Categories(ParentId);
CREATE INDEX IF NOT EXISTS IX_Products_CategoryId ON Products(CategoryId);
CREATE INDEX IF NOT EXISTS IX_ProductPrices_RetailerName ON ProductPrices(RetailerName);


/* Seed the known supermarket chains with their local logo paths.
   This is an upsert: if the supermarket already exists, only its logo is updated in place.
   The row is never deleted, so its prices are never removed by the cascade above. */
INSERT INTO Supermarkets (Name, LogoURL) VALUES
('Σκλαβενίτης', '/logos/sklavenitis.png'),
('ΑΒ Βασιλόπουλος', '/logos/ab.png'),
('Lidl', '/logos/lidl.png'),
('My market', '/logos/mymarket.jpg'),
('Μασούτης', '/logos/masoutis.webp'),
('Γαλαξίας', '/logos/galaxias.png'),
('Κρητικός', '/logos/kritikos.jpg'),
('Bazaar', '/logos/bazaar.png'),
('Χαλκιαδάκης', '/logos/chalkiadakis.png'),
('Synka', '/logos/synka.jpg')
ON CONFLICT(Name) DO UPDATE SET LogoURL = excluded.LogoURL;