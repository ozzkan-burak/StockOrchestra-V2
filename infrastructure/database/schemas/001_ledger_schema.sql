-- ==============================================================================
-- StockOrchestra - Domain Modeli ve Ledger Şeması
-- ==============================================================================
-- Proje: StockOrchestra-MCP
-- Faz: 2 - Domain Modeli ve Ledger (The Source of Truth)
-- Açıklama: Double-Entry Bookkeeping mantığıyla çalışan immutable transaction log
-- ==============================================================================
-- Önemli: Bu şema append-only prensibiyle çalışır. Hiçbir kayıt silinmez veya 
-- güncellenmez. Bakiye, kayıtların toplamından hesaplanır.
-- ==============================================================================

-- ----------------------------------------------------------------------------
-- Uzantılar ve TimescaleDB Hipertabloları
-- ----------------------------------------------------------------------------

-- UUID oluşturucu uzantısı - Benzersiz kimlikler için
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- TimescaleDB uzantısı - Time-series veritabanı için (zaten kurulu olmalı)
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- ----------------------------------------------------------------------------
-- Kullanıcılar Tablosu (Users)
-- ----------------------------------------------------------------------------
-- Açıklama: Sisteme kayıtlı kullanıcıları saklar. Bilgiler değiştirilebilir
-- ancak kimlikler (id) sabit kalır.

CREATE TABLE IF NOT EXISTS users (
    -- Benzersiz kullanıcı kimliği (Primary Key)
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Kullanıcı adı (benzersiz olmalı)
    username VARCHAR(50) NOT NULL UNIQUE,
    
    -- Kullanıcı email adresi (benzersiz olmalı)
    email VARCHAR(255) NOT NULL UNIQUE,
    
    -- Email doğrulandı mı?
    email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    
    -- Kullanıcı şifresinin hash'i (bcrypt veya argon2 ile)
    password_hash VARCHAR(255) NOT NULL,
    
    -- Kullanıcı tam adı
    full_name VARCHAR(255),
    
    -- Kullanıcı durumu: active, suspended, deleted
    status VARCHAR(20) NOT NULL DEFAULT 'active' 
        CHECK (status IN ('active', 'suspended', 'deleted')),
    
    -- Son giriş zamanı
    last_login_at TIMESTAMPTZ,
    
    -- Hesap oluşturulma zamanı
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Son güncelleme zamanı
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- İndeksler
    CONSTRAINT users_username_key UNIQUE (username),
    CONSTRAINT users_email_key UNIQUE (email)
);

-- Kullanıcı adı indeksi - Arama performansı için
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);

-- Kullanıcı durumu indeksi - Filtreleme için
CREATE INDEX IF NOT EXISTS idx_users_status ON users(status);

-- Kullanıcı oluşturma tarihi indeksi - Sıralama için
CREATE INDEX IF NOT EXISTS idx_users_created_at ON users(created_at DESC);

-- ----------------------------------------------------------------------------
-- Varlıklar Tablosu (Assets)
-- ----------------------------------------------------------------------------
-- Açıklama: Sistemde takip edilen tüm finansal varlıkları saklar.
-- Örnek: BTC, ETH, AAPL, GOOGL vb.

CREATE TABLE IF NOT EXISTS assets (
    -- Benzersiz varlık kimliği (Primary Key)
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Varlık sembolü (benzersiz, örn: BTC, ETH, AAPL)
    symbol VARCHAR(20) NOT NULL UNIQUE,
    
    -- Varlık adı (örneğin: Bitcoin, Ethereum)
    name VARCHAR(255) NOT NULL,
    
    -- Varlık tipi: crypto, stock, etf, forex, commodity
    asset_type VARCHAR(20) NOT NULL 
        CHECK (asset_type IN ('crypto', 'stock', 'etf', 'forex', 'commodity', 'bond')),
    
    -- Ana para birimi (örneğin: USD, TRY, EUR)
    quote_currency VARCHAR(10) NOT NULL DEFAULT 'USD',
    
    -- Minimum alım miktarı (ondalık hassasiyet)
    min_quantity DECIMAL(36, 18) NOT NULL DEFAULT 0.000000000000000001,
    
    -- Minimum işlem tutarı (quote currency cinsinden)
    min_notional DECIMAL(36, 18) NOT NULL DEFAULT 0.01,
    
    -- Varlık aktif mi?
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Kaç ondalık basamak gösterilecek?
    decimal_places INT NOT NULL DEFAULT 8,
    
    -- İşlem başina komisyon oranı
    commission_rate DECIMAL(10, 8) NOT NULL DEFAULT 0.001,
    
    -- Asset Ledger'daki son işlem zamanı (performans için)
    last_traded_at TIMESTAMPTZ,
    
    -- Oluşturulma zamanı
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Son güncelleme zamanı
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Kısıtlamalar
    CONSTRAINT assets_symbol_key UNIQUE (symbol)
);

-- Varlık sembolü indeksi
CREATE INDEX IF NOT EXISTS idx_assets_symbol ON assets(symbol);

-- Varlık tipi indeksi
CREATE INDEX IF NOT EXISTS idx_assets_type ON assets(asset_type);

-- Aktif varlıklar indeksi
CREATE INDEX IF NOT EXISTS idx_assets_is_active ON assets(is_active);

-- ----------------------------------------------------------------------------
-- Varlık Defteri Tablosu (Asset_Ledger) - THE SOURCE OF TRUTH
-- ----------------------------------------------------------------------------
-- Açıklama: Double-Entry Bookkeeping mantığıyla çalışan immutable işlem defteri.
-- Her işlem bu tabloya YENİ KAYIT olarak eklenir. Hiçbir kayıt güncellenmez
-- veya silinmez. Kullanıcının bakiyesi, bu tablodaki kayıtların toplamından
-- hesaplanır (SUM).
--
-- Double-Entry Prensibi:
-- - BUY: Kullanıcı varlık satın alır (+quantity, -quote_balance)
-- - SELL: Kullanıcı varlık satar (-quantity, +quote_balance)
-- - TRANSFER_IN: Varlık transferi (+quantity)
-- - TRANSFER_OUT: Varlık transferi (-quantity)
-- - DEPOSIT: Nakit yatırma (+quote_balance)
-- - WITHDRAWAL: Nakit çekme (-quote_balance)
-- - FEE: Komisyon (-quote_balance)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS asset_ledger (
    -- Benzersiz işlem kimliği (Primary Key)
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- İşlemi yapan kullanıcı (Foreign Key)
    user_id UUID NOT NULL 
        REFERENCES users(id) ON DELETE RESTRICT,
    
    -- İşlem yapılan varlık (Foreign Key) - NOT NULL olmayabilir (nakit işlemleri için)
    asset_id UUID 
        REFERENCES assets(id) ON DELETE RESTRICT,
    
    -- İşlem tipi - Double-Entry bookeepering kategorileri
    transaction_type VARCHAR(20) NOT NULL 
        CHECK (transaction_type IN (
            'BUY',           -- Varlık satın alma
            'SELL',          -- Varlık satma
            'TRANSFER_IN',   -- Varlık transferi (giriş)
            'TRANSFER_OUT',  -- Varlık transferi (çıkış)
            'DEPOSIT',       -- Nakit yatırma
            'WITHDRAWAL',    -- Nakit çekme
            'FEE',           -- Komisyon/Ücret
            'DIVIDEND',      -- Temettü geliri
            'INTEREST',       -- Faiz geliri
            'CORRECTION'     -- Düzeltme (eski hataları düzeltmek için)
        )),
    
    -- İşlem yönü: DEBIT veya CREDIT
    -- DEBIT: Varlık veya nakit hesaba GİRİYOR
    -- CREDIT: Varlık veya nakit hesaptan ÇIKIYOR
    side VARCHAR(6) NOT NULL 
        CHECK (side IN ('DEBIT', 'CREDIT')),
    
    -- İşlem miktarı (varlık için, ondalık hassasiyet)
    -- Örnek: 0.5 BTC, 10.25 ETH
    quantity DECIMAL(36, 18) NOT NULL 
        CHECK (quantity >= 0),
    
    -- İşlem birimi başına fiyat (quote currency cinsinden)
    -- Örnek: BTC buy = 45000.50 USD
    price_at_time DECIMAL(36, 18),
    
    -- Toplam işlem tutarı (quote currency cinsinden)
    -- quantity * price_at_time hesaplanır (hesaplama hatasi için saklı)
    -- NOT: Komisyon ve ücretler bu alanın dışında tutulur
    total_notional DECIMAL(36, 18),
    
    -- İşlem yapılan quote currency bakiyesi DEĞİŞİMİ
    -- Double-Entry kaydı: Bu işlemden sonra kullanıcının quote balance'ı
    -- ne oldu? (hesap doğrulaması için saklı)
    quote_balance_after DECIMAL(36, 18),
    
    -- İşlem yapılan varlık bakiyesi DEĞİŞİMİ
    -- Double-Entry kaydı: Bu işlemden sonra kullanıcının varlık bakiyesi
    -- ne oldu? (hesap doğrulaması için saklı)
    -- NULL olabilir (nakit işlemleri için)
    asset_balance_after DECIMAL(36, 18),
    
    -- İşlem için ödenen komisyon tutarı
    commission_amount DECIMAL(36, 18) NOT NULL DEFAULT 0,
    
    -- Komisyon ödendi mi?
    commission_paid BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Referans numarası (dış sistem bağlantısı için)
    -- Örnek: Binance order ID, banka transaction ID
    external_ref VARCHAR(255),
    
    -- İşlem açıklaması (opsiyonel)
    description TEXT,
    
    -- İşlem durumu: pending, completed, failed, cancelled
    status VARCHAR(20) NOT NULL DEFAULT 'completed' 
        CHECK (status IN ('pending', 'completed', 'failed', 'cancelled')),
    
    -- İşlem zamanı (otomatik olabilir, ama elle ayarlanabilir)
    -- Bu alan, sıralama ve zaman serisi analiz için kritik
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- İşlem tamamlanma zamanı
    completed_at TIMESTAMPTZ,
    
    -- Son güncelleme zamanı (asla güncellenmemeli ama polymorphic için)
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Kısıtlamalar
    CONSTRAINT ledger_quantity_positive CHECK (quantity >= 0),
    CONSTRAINT ledger_commission_positive CHECK (commission_amount >= 0),
    CONSTRAINT ledger_total_notional_positive CHECK (
        total_notional IS NULL OR total_notional >= 0
    )
);

-- ----------------------------------------------------------------------------
-- İndeksler - Asset Ledger için optimize
-- ----------------------------------------------------------------------------

-- Kompozit indeks: Kullanıcı + Varlık + Zaman (en sık kullanılan sorgu)
-- Bu indeks, kullanıcının belirli bir varlıktaki bakiyesini hızlı hesaplar
CREATE INDEX IF NOT EXISTS idx_ledger_user_asset_time 
    ON asset_ledger(user_id, asset_id, created_at DESC);

-- Kompozit indeks: Kullanıcı + Zaman (kullanıcının tüm işlem geçmişi)
CREATE INDEX IF NOT EXISTS idx_ledger_user_time 
    ON asset_ledger(user_id, created_at DESC);

-- Kompozit indeks: Varlık + Zaman (varlığın işlem geçmişi)
CREATE INDEX IF NOT EXISTS idx_ledger_asset_time 
    ON asset_ledger(asset_id, created_at DESC);

-- İşlem tipi indeksi (filtreleme için)
CREATE INDEX IF NOT EXISTS idx_ledger_transaction_type 
    ON asset_ledger(transaction_type);

-- İşlem durumu indeksi (filtreleme için)
CREATE INDEX IF NOT EXISTS idx_ledger_status 
    ON asset_ledger(status);

-- Zaman indeksi (zaman serisi sorguları için)
CREATE INDEX IF NOT EXISTS idx_ledger_created_at 
    ON asset_ledger(created_at DESC);

-- Hash indeks: user_id (hızlı lookup için)
CREATE INDEX IF NOT EXISTS idx_ledger_user_id_hash 
    ON asset_ledger USING HASH (user_id);

-- Hash indeks: asset_id (hızlı lookup için)
CREATE INDEX IF NOT EXISTS idx_ledger_asset_id_hash 
    ON asset_ledger USING HASH (asset_id);

-- ----------------------------------------------------------------------------
-- TimescaleDB Hipertablo Dönüşümü (Opsiyonel - Faz 5'te aktive edilecek)
-- ----------------------------------------------------------------------------
-- Açıklama: Asset_ledger'ı hypertable'a dönüştürerek time-series 
-- performansını artırır. Bu işlem normalde Faz 5'te yapılır.
-- Uncomment ederek aktive edebilirsiniz:
/*
SELECT create_hypertable(
    'asset_ledger', 
    'created_at',
    chunk_time_interval => INTERVAL '1 day',
    migrate_data => FALSE,
    if_not_exists => TRUE
);

-- Sıkıştırma politikası (1 günlük veriler sıkıştırılmaz, sonra sıkıştırılır)
ALTER TABLE asset_ledger SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'user_id, asset_id'
);

-- Sıkıştırma policy'si
SELECT add_compression_policy(
    'asset_ledger',
    compress_after => INTERVAL '7 days'
);
*/

-- ----------------------------------------------------------------------------
-- Fonksiyonlar ve View'ler
-- ----------------------------------------------------------------------------

-- Kullanıcının belirli bir varlıktaki NET bakiyesini hesaplayan fonksiyon
-- Bu, Double-Entry prensibinin kalbidir. Bakiye, kayıtların toplamından hesaplanır.
CREATE OR REPLACE FUNCTION fn_get_asset_balance(
    p_user_id UUID,
    p_asset_id UUID
) RETURNS DECIMAL(36, 18) AS $$
DECLARE
    v_balance DECIMAL(36, 18);
BEGIN
    -- DEBIT kayıtlarının toplamı - CREDIT kayıtlarının toplamı
    -- BUY ve TRANSFER_IN: DEBIT (+quantity)
    -- SELL ve TRANSFER_OUT: CREDIT (-quantity)
    SELECT COALESCE(
        SUM(
            CASE 
                WHEN side = 'DEBIT' THEN quantity
                ELSE -quantity
            END
        ),
        0
    ) INTO v_balance
    FROM asset_ledger
    WHERE user_id = p_user_id
      AND asset_id = p_asset_id
      AND status = 'completed';
    
    RETURN v_balance;
END;
$$ LANGUAGE plpgsql STABLE PARALLEL SAFE;


-- Kullanıcının quote currency (USD, TRY vb.) bakiyesini hesaplayan fonksiyon
CREATE OR REPLACE FUNCTION fn_get_quote_balance(
    p_user_id UUID,
    p_quote_currency VARCHAR(10) DEFAULT 'USD'
) RETURNS DECIMAL(36, 18) AS $$
DECLARE
    v_balance DECIMAL(36, 18);
BEGIN
    -- Quote currency bakiyesi, DEPOSIT/WITHDRAWAL işlemlerinden hesaplanır
    -- DEPOSIT: DEBIT (+quote_balance)
    -- WITHDRAWAL, FEE, BUY: CREDIT (-quote_balance)
    -- SELL: DEBIT (+quote_balance)
    SELECT COALESCE(
        SUM(
            CASE 
                WHEN side = 'DEBIT' 
                     AND transaction_type IN ('DEPOSIT', 'SELL', 'DIVIDEND', 'INTEREST', 'CORRECTION')
                THEN total_notional - COALESCE(commission_amount, 0)
                WHEN side = 'CREDIT'
                     AND transaction_type IN ('WITHDRAWAL', 'BUY', 'FEE')
                THEN -(total_notional + COALESCE(commission_amount, 0))
                ELSE 0
            END
        ),
        0
    ) INTO v_balance
    FROM asset_ledger
    WHERE user_id = p_user_id
      AND (asset_id IS NULL)  -- Nakit işlemleri için asset_id NULL
      AND (
          quote_currency_after IS NOT NULL 
          OR transaction_type IN ('DEPOSIT', 'WITHDRAWAL', 'BUY', 'SELL', 'FEE', 'DIVIDEND', 'INTEREST')
      )
      AND status = 'completed';
    
    RETURN v_balance;
END;
$$ LANGUAGE plpgsql STABLE PARALLEL SAFE;


-- Kullanıcının varlık portföyünü özetleyen view
CREATE OR REPLACE VIEW vw_user_portfolio AS
SELECT 
    ul.user_id,
    ul.asset_id,
    a.symbol AS asset_symbol,
    a.name AS asset_name,
    a.asset_type,
    a.quote_currency,
    fn_get_asset_balance(ul.user_id, ul.asset_id) AS quantity,
    -- Son işlem fiyatı (eğer varsa)
    COALESCE(
        (SELECT price_at_time 
         FROM asset_ledger 
         WHERE user_id = ul.user_id 
           AND asset_id = ul.asset_id 
           AND status = 'completed'
         ORDER BY created_at DESC 
         LIMIT 1),
        0
    ) AS last_price,
    -- Toplam notional değer (varlık * son fiyat)
    fn_get_asset_balance(ul.user_id, ul.asset_id) * 
    COALESCE(
        (SELECT price_at_time 
         FROM asset_ledger 
         WHERE user_id = ul.user_id 
           AND asset_id = ul.asset_id 
           AND status = 'completed'
         ORDER BY created_at DESC 
         LIMIT 1),
        0
    ) AS total_value
FROM (
    -- Her kullanıcının her varlık için son işlem
    SELECT DISTINCT user_id, asset_id
    FROM asset_ledger
    WHERE status = 'completed'
) ul
JOIN assets a ON a.id = ul.asset_id;


-- Kullanıcının işlem geçmişini özetleyen view
CREATE OR REPLACE VIEW vw_user_transactions AS
SELECT 
    l.id AS transaction_id,
    l.user_id,
    l.asset_id,
    a.symbol AS asset_symbol,
    l.transaction_type,
    l.side,
    l.quantity,
    l.price_at_time,
    l.total_notional,
    l.commission_amount,
    l.status,
    l.created_at,
    l.external_ref,
    l.description
FROM asset_ledger l
LEFT JOIN assets a ON a.id = l.asset_id;


-- ----------------------------------------------------------------------------
-- Tetikleyiciler (Triggers)
-- ----------------------------------------------------------------------------

-- created_at Otomatik Ayarlama
-- Yeni kayıt eklendiğinde created_at otomatik olarak ayarlanır
CREATE OR REPLACE FUNCTION fn_set_created_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.created_at = COALESCE(NEW.created_at, NOW());
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Asset Ledger için updateEngelleme (Append-only)
-- Aslında hiçbir kaydın güncellenmemesi gerekir ama polymorphic için
-- updated_at güncellenir, ama data değişmez
CREATE OR REPLACE FUNCTION fn_ledger_prevent_update()
RETURNS TRIGGER AS $$
BEGIN
    -- Update işlemi engelle
    RAISE EXCEPTION 'Asset_Ledger append-only table. Updates are not allowed. Id: %', NEW.id;
END;
$$ LANGUAGE plpgsql;

-- Asset Ledger tetikleyicileri
CREATE TRIGGER trg_ledger_created_at
    BEFORE INSERT ON asset_ledger
    FOR EACH ROW EXECUTE FUNCTION fn_set_created_at();

-- Users ve Assets tabloları için update tetikleyicileri
CREATE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION fn_set_created_at();

CREATE TRIGGER trg_assets_updated_at
    BEFORE UPDATE ON assets
    FOR EACH ROW EXECUTE FUNCTION fn_set_created_at();

-- ----------------------------------------------------------------------------
-- Açılış Verileri (Seed Data)
-- ----------------------------------------------------------------------------

-- Örnek kullanıcı (şifre: Password123!)
INSERT INTO users (username, email, password_hash, full_name, status)
VALUES 
    ('admin', 'admin@stockorchestra.local', 
     '$2a$11$1234567890abcdef1234567890abcdef1234567890abcdef1234567890a', 
     'System Administrator', 'active')
ON CONFLICT (username) DO NOTHING;

-- Örnek varlıklar
INSERT INTO assets (symbol, name, asset_type, quote_currency, decimal_places, is_active)
VALUES 
    ('BTC', 'Bitcoin', 'crypto', 'USD', 8, TRUE),
    ('ETH', 'Ethereum', 'crypto', 'USD', 8, TRUE),
    ('USDT', 'Tether', 'crypto', 'USD', 2, TRUE),
    ('AAPL', 'Apple Inc.', 'stock', 'USD', 4, TRUE),
    ('GOOGL', 'Alphabet Inc.', 'stock', 'USD', 4, TRUE),
    ('TSLA', 'Tesla Inc.', 'stock', 'USD', 4, TRUE),
    ('EURUSD', 'EUR/USD', 'forex', 'USD', 5, TRUE),
    ('XAUUSD', 'Gold/USD', 'commodity', 'USD', 4, TRUE)
ON CONFLICT (symbol) DO NOTHING;


-- ==============================================================================
-- Sorgu Örnekleri
-- ==============================================================================
-- Kullanıcının Bitcoin bakiyesi:
--   SELECT fn_get_asset_balance(user_id, asset_id) FROM assets WHERE symbol = 'BTC';

-- Kullanıcının tüm portföyü:
--   SELECT * FROM vw_user_portfolio WHERE user_id = 'xxx';

-- Kullanıcının i��lem geçmişi:
--   SELECT * FROM vw_user_transactions WHERE user_id = 'xxx' ORDER BY created_at DESC;
-- ==============================================================================