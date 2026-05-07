-- ==============================================================================
-- StockOrchestra - TimescaleDB Zaman Serisi Şeması
-- ==============================================================================
-- Proje: StockOrchestra-MCP
-- Faz: 5 - Analitik ve Zaman Serisi (Time-Series Power)
-- Açıklama: Fiyat verilerinin zaman serisi olarak saklanması ve analiz edilmesi
--
/// Önemli: TimescaleDB extension'ı etkinleştirilmelidir
-- ==============================================================================

-- ----------------------------------------------------------------------------
-- TimescaleDB Uzantısını Aktifleştir
-- ----------------------------------------------------------------------------
-- TimescaleDB, PostgreSQL'i zaman serisi veritabanına dönüştürür
-- Hypertable: Otomatik chunk yönetimi, sıkıştırma, continuous aggregates

CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

-- ----------------------------------------------------------------------------
-- Fiyatlar Tablosu (Prices)
-- ----------------------------------------------------------------------------
-- Açıklama: Tüm varlık fiyatlarının zaman serisi olarak saklandığı ana tablo.
-- Bu tablo, daha sonra TimescaleDB Hypertable'a dönüştürülecek.
--
-- Zaman Serisi Optimizasyonları:
-- - Chunk: 1 günlük veriler tek chunk'ta (hızlı sorgu)
-- - Segment by: Varlık bazında partition (paralel sorgu)
-- - Compression: Sıkıştırılmış veri (disk tasarrufu)

CREATE TABLE IF NOT EXISTS prices (
    -- Benzersiz kayıt kimliği
    id BIGSERIAL,
    
    -- Zaman damgası (PRIMARY KEY bileşeni - chunk yönetimi için kritik)
    -- NOT: Bu alan, hypertable'ın partition anahtarı olacak
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Varlık sembolü (örn: BTC, ETH, AAPL)
    symbol VARCHAR(20) NOT NULL,
    
    -- Varlık ID'si (veritabanı referansı)
    asset_id UUID,
    
    -- Keşfedilen median fiyat
    price DECIMAL(36, 18) NOT NULL,
    
    -- Alış fiyatı (Bid)
    bid_price DECIMAL(36, 18),
    
    -- Satış fiyatı (Ask)
    ask_price DECIMAL(36, 18),
    
    -- 24 saatlik değişim yüzdesi
    change_24h DECIMAL(10, 4),
    
    -- Fiyat kaynağı (multi-source)
    sources JSONB,
    
    -- Geçerli kaynak sayısı
    valid_source_count INTEGER DEFAULT 0,
    
    -- Fiyatın kaynağı tarafından belirlenen zaman
    source_timestamp TIMESTAMPTZ,
    
    -- Doğrulama durumu
    validation_status VARCHAR(20) DEFAULT 'valid',
    
    -- --------------------------------------------------------------------
    -- Zaman Serisi İndeksleri (Sorgu Performansı İçin)
    -- --------------------------------------------------------------------
    
    -- PRIMARY KEY: Belirsiz kayıtları engellemek için
    PRIMARY KEY (id, created_at)
);

-- Kompozit indeks: symbol + created_at (en sık kullanılan sorgu)
-- Bu indeks, belirli bir varlığın zaman dilimindeki fiyatlarını hızlı bulur
CREATE INDEX IF NOT EXISTS idx_prices_symbol_time 
    ON prices(symbol, created_at DESC);

-- Kompozit indeks: created_at (zaman bazlı sorgular)
-- Bu indeks, belirli bir zaman dilimindeki tüm fiyatları bulur
CREATE INDEX IF NOT EXISTS idx_prices_created_at 
    ON prices(created_at DESC);

-- Hash indeks: symbol (hızlı lookup için)
-- NOT: TimescaleDB bu indeksi otomatik yönetir, manuel gerekmez
CREATE INDEX IF NOT EXISTS idx_prices_symbol_hash 
    ON prices USING HASH (symbol);

-- ----------------------------------------------------------------------------
-- TimescaleDB Hypertable Dönüşümü
-- ----------------------------------------------------------------------------
-- Açıklama: Normal tabloyu zaman serisi tablosuna dönüştürür.
--
-- Hypertable Faydaları:
-- 1. Otomatik chunk yönetimi (büyük veri setlerini küçük parçalara böl)
-- 2. Sorgu parallelism (birden fazla chunk'ta paralel sorgu)
-- 3. Time-based index optimizasyonları
-- 4. Continuous aggregates (önceden hesaplanmış agregalar)
-- 5. Sıkıştırma politikaları (compression policies)
--
-- chunk_time_interval: Her chunk'un zaman aralığı
-- NOT: 1 gün = 86400 saniye (aksi belirtilmedikçe)
-- if_not_exists: Aynı tablo tekrar oluşturulmaz

SELECT create_hypertable(
    'prices', 
    'created_at',
    chunk_time_interval => INTERVAL '1 day',
    migrate_data => FALSE,
    if_not_exists => TRUE
);

-- ----------------------------------------------------------------------------
-- Segment By Yapılandırması (İleri Düzey)
-- ----------------------------------------------------------------------------
-- Açıklama: Chunk'ları hem zaman hem de varlık bazında böler.
-- Bu, aynı anda birden fazla varlık sorgulanırken performansı artırır.
--
-- Örnek: BTC ve ETH aynı anda sorgulanırken,
-- her biri farklı chunk'tan paralel okunur

ALTER TABLE prices SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'symbol'
);

-- NOT: compress_segmentby ayarlandıktan sonra,
-- compress_chunk_by_interval da ayarlanabilir
-- (isteğe bağlı)

-- ----------------------------------------------------------------------------
-- Sıkıştırma Politikası (Compression Policy)
-- ----------------------------------------------------------------------------
-- Açıklama: 7 günden eski verileri otomatik sıkıştırır.
--
-- Sıkıştırma Faydaları:
-- - Disk alanından %90'a varan tasarruf
-- - Daha hızlı sorgu (daha az okunacak veri)
-- - Daha iyi IO performansı
--
-- compress_after: Kaç gün sonra sıkıştırılacağı
-- NOT: 7 gün = 168 saat = 604800 saniye

SELECT add_compression_policy(
    'prices',
    compress_after => INTERVAL '7 days'
);

-- Sıkıştırma ayrıntılarını göster (bilgi amaçlı)
-- Bu sorgu, hangi chunk'ların sıkıştırıldığını gösterir
-- SELECT * FROM timescaledb_information.chunks 
-- WHERE table_name = 'prices' AND is_compressed;

-- ----------------------------------------------------------------------------
-- Continuous Aggregates (OHLC)
-- ----------------------------------------------------------------------------
-- Açıklama: Ham verilerden önceden hesaplanmış özetler.
-- Her聚合asyon (aggregate), belirli bir zaman diliminde çalışır.
--
-- OHLC Nedir?
-- O: Open (Açılış) - İlk fiyat
-- H: High (Yüksek) - En yüksek fiyat
-- L: Low (Düşük) - En düşük fiyat
-- C: Close (Kapanış) - Son fiyat
--
-- Faydaları:
-- - Sorgu süresi binlerce kat daha hızlı
-- - Sunucu yükü azalır
-- - Gerçek zamanlı dashboard'lar mümkün

-- ----------------------------------------------------------------------------
-- 1 Dakikalık OHLC View
-- ----------------------------------------------------------------------------
-- Açıklama: Her 1 dakikalık periyotta OHLC değerlerini hesaplar.
-- Kullanım: Anlık grafikler, scalping stratejileri

CREATE MATERIALIZED VIEW IF NOT EXISTS prices_1m_ohlc
WITH (timescaledb.continuous) AS
SELECT
    -- Zaman dilimi (1 dakikalık)
    time_bucket('1 minute', created_at) AS bucket,
    
    -- Varlık sembolü
    symbol,
    
    -- OHLC değerleri
    first(price, created_at) AS open,      -- İlk fiyat
    max(price) AS high,                    -- En yüksek fiyat
    min(price) AS low,                     -- En düşük fiyat
    last(price, created_at) AS close,      -- Son fiyat
    
    -- Ek istatistikler
    avg(price) AS avg,                    -- Ortalama fiyat
    count(*) AS volume,                  -- Veri sayısı
    
    -- Fiyat değişimi
    last(price, created_at) - first(price, created_at) AS change,
    (last(price, created_at) - first(price, created_at)) / first(price, created_at) * 100 AS change_percent
FROM prices
WHERE validation_status = 'valid'
GROUP BY 
    time_bucket('1 minute', created_at),
    symbol
WITH NO DATA;

-- 1 dakikalık aggregate için refresh policy
-- Her 1 dakikada bir güncellenir
SELECT add_continuous_aggregate_policy(
    'prices_1m_ohlc',
    start_offset => INTERVAL '15 minutes',
    end_offset => INTERVAL '1 minute',
    schedule_interval => INTERVAL '1 minute'
);

-- ----------------------------------------------------------------------------
-- 1 Saatlik OHLC View
-- ----------------------------------------------------------------------------
-- Açıklama: Her 1 saatlik periyotta OHLC değerlerini hesaplar.
-- Kullanım: Günlük grafikler, gün içi analizler

CREATE MATERIALIZED VIEW IF NOT EXISTS prices_1h_ohlc
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', created_at) AS bucket,
    symbol,
    first(price, created_at) AS open,
    max(price) AS high,
    min(price) AS low,
    last(price, created_at) AS close,
    avg(price) AS avg,
    count(*) AS volume,
    last(price, created_at) - first(price, created_at) AS change,
    (last(price, created_at) - first(price, created_at)) / first(price, created_at) * 100 AS change_percent
FROM prices
WHERE validation_status = 'valid'
GROUP BY 
    time_bucket('1 hour', created_at),
    symbol
WITH NO DATA;

-- 1 saatlik aggregate için refresh policy
-- Her 5 dakikada bir güncellenir
SELECT add_continuous_aggregate_policy(
    'prices_1h_ohlc',
    start_offset => INTERVAL '2 hours',
    end_offset => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes'
);

-- ----------------------------------------------------------------------------
-- 1 Günlük OHLC View
-- ----------------------------------------------------------------------------
-- Açıklama: Her 1 günlük periyotta OHLC değerlerini hesaplar.
-- Kullanım: Günlük grafikler, uzun vadeli analizler

CREATE MATERIALIZED VIEW IF NOT EXISTS prices_1d_ohlc
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 day', created_at) AS bucket,
    symbol,
    first(price, created_at) AS open,
    max(price) AS high,
    min(price) AS low,
    last(price, created_at) AS close,
    avg(price) AS avg,
    count(*) AS volume,
    last(price, created_at) - first(price, created_at) AS change,
    (last(price, created_at) - first(price, created_at)) / first(price, created_at) * 100 AS change_percent
FROM prices
WHERE validation_status = 'valid'
GROUP BY 
    time_bucket('1 day', created_at),
    symbol
WITH NO DATA;

-- 1 günlük aggregate için refresh policy
-- Her 1 saatte bir güncellenir
SELECT add_continuous_aggregate_policy(
    'prices_1d_ohlc',
    start_offset => INTERVAL '2 days',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour'
);

-- ----------------------------------------------------------------------------
-- Index'ler (Continuous Aggregates için)
-- ----------------------------------------------------------------------------

-- 1 dakikalık OHLC için zaman indeksi
CREATE INDEX IF NOT EXISTS idx_prices_1m_ohlc_bucket 
    ON prices_1m_ohlc(bucket DESC);

-- 1 dakikalık OHLC için symbol indeksi
CREATE INDEX IF NOT EXISTS idx_prices_1m_ohlc_symbol 
    ON prices_1m_ohlc(symbol, bucket DESC);

-- 1 saatlik OHLC için zaman indeksi
CREATE INDEX IF NOT EXISTS idx_prices_1h_ohlc_bucket 
    ON prices_1h_ohlc(bucket DESC);

-- 1 saatlik OHLC için symbol indeksi
CREATE INDEX IF NOT EXISTS idx_prices_1h_ohlc_symbol 
    ON prices_1h_ohlc(symbol, bucket DESC);

-- 1 günlük OHLC için zaman indeksi
CREATE INDEX IF NOT EXISTS idx_prices_1d_ohlc_bucket 
    ON prices_1d_ohlc(bucket DESC);

-- 1 günlük OHLC için symbol indeksi
CREATE INDEX IF NOT EXISTS idx_prices_1d_ohlc_symbol 
    ON prices_1d_ohlc(symbol, bucket DESC);

-- ----------------------------------------------------------------------------
-- Sorgu Örnekleri
-- ----------------------------------------------------------------------------

-- Örnek 1: Bitcoin'in son 1 saatlik 1 dakikalık OHLC:
-- SELECT * FROM prices_1m_ohlc 
-- WHERE symbol = 'BTC' AND bucket >= NOW() - INTERVAL '1 hour'
-- ORDER BY bucket DESC;

-- Örnek 2: Bitcoin'in son 24 saatlik saatlik OHLC:
-- SELECT * FROM prices_1h_ohlc 
-- WHERE symbol = 'BTC' AND bucket >= NOW() - INTERVAL '1 day'
-- ORDER BY bucket DESC;

-- Örnek 3: Tüm varlıkların günlük OHLC (dün):
-- SELECT * FROM prices_1d_ohlc 
-- WHERE bucket >= CURRENT_DATE - INTERVAL '1 day' AND bucket < CURRENT_DATE;

-- Örnek 4: Hangi chunk'lar sıkıştırılmış?
-- SELECT chunk_table, compression_ratio, is_compressed
-- FROM timescaledb_information.chunks
-- WHERE table_name = 'prices';

-- Örnek 5: Hypertable bilgileri
-- SELECT * FROM timescaledb_information.hypertables
-- WHERE table_name = 'prices';

-- ==============================================================================