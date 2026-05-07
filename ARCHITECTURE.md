# StockOrchestra-V2: Distributed Asset Management & Real-Time Analytics

## 1. Proje Vizyonu

StockOrchestra, kullanıcıların farklı varlık sınıflarını (Kripto, Hisse Senedi, Değerli Metaller) tek bir noktadan, gerçek zamanlı verilerle ve %100 doğruluk payıyla takip edebilmelerini sağlayan dağıtık bir sistemdir. Bu proje bir "Lab Projesi" olmasına rağmen, üretim (production) standartlarında bir mimari duruş sergiler.

## 2. Mimari Prensipler

- **Microservices Architecture:** Servisler bağımsız ölçeklenebilir ve izole olmalıdır.
- **Asynchronous Event-Driven:** Sistem içi iletişim Redis Streams üzerinden olay güdümlü (event-driven) ilerler.
- **CQRS (Command Query Responsibility Segregation):** Yazma (Ledger/Transaction) ve Okuma (Dashboard/Portfolio) işlemleri birbirinden ayrılmıştır.
- **Source of Truth (Ledger):** Tüm varlık hareketleri "Double-Entry Bookkeeping" prensibiyle değişmez (immutable) bir defterde tutulur.

## 3. Domain ve Modüller

### A. Price Discovery Service (Aggregator)

- **Görevi:** Birden fazla API'den (Binance, Yahoo Finance vb.) ham fiyat verilerini toplar.
- **Kritik Özellikler:** - **Circuit Breaker:** Hatalı veya sapmalı veri gönderen kaynakları izole eder.
  - **Validation:** Farklı kaynaklardan gelen fiyatların ortancasını (Median) alarak "Gold Record" fiyatı belirler.
  - **Staleness Check:** Belirli bir süredir güncellenmeyen (donmuş) veriyi reddeder.

### B. Portfolio Manager Service

- **Görevi:** Kullanıcı bakiyelerini ve işlem geçmişini yönetir.
- **Kritik Özellikler:**
  - **Asset Ledger:** Tüm alım-satım işlemleri bu tabloda tutulur.
  - **Real-time Calculator:** Yeni bir fiyat geldiğinde, ilgili kullanıcıların portföy değerlerini Redis üzerinde anlık olarak günceller.

### C. Analytical Store Service

- **Görevi:** Tarihsel verileri saklar ve analiz eder.
- **Teknoloji:** PostgreSQL + TimescaleDB.
- **Kritik Özellikler:** Dakikalık/Saatlik OHLC (Mum) barlarını oluşturur ve sıkıştırılmış veri depolama sağlar.

### D. Frontend (Real-Time UI)

- **Görevi:** Kullanıcıya Dashboard ve Analiz araçlarını sunar.
- **Teknoloji:** Next.js + Tailwind CSS + Zustand.
- **Kritik Özellikler:** WebSocket üzerinden gelen fiyatları "Atomic Update" ile tarayıcıyı yormadan günceller.

## 4. Teknik Stack & Altyapı

- **Backend:** .NET 8/9
- **Frontend:** Next.js 14+
- **Database:** PostgreSQL (TimescaleDB Extension)
- **Message Broker & Cache:** Redis (Redis Streams & Pub/Sub)
- **Containerization:** Docker & Docker Compose
- **Orchestration:** Kubernetes (Gelecek Faz)

## 5. Veri Akış Şeması (Event-Flow)

1. `PriceFetcher` -> Ham Fiyat -> `PriceDiscovery-Queue`
2. `PriceDiscoveryService` -> Doğrulanmış Fiyat -> `Verified-Price-Stream`
3. `PortfolioManager` & `AnalyticalStore` -> `Verified-Price-Stream`'i dinler ve veriyi işler.
4. `Gateway-API` -> WebSocket üzerinden `Frontend`'e veriyi iletir.

## 6. Geliştirme Yol Haritası (Roadmap)

- **Faz 1:** Altyapı Kurulumu (Docker) ve Asset Ledger Modeli.
- **Faz 2:** Price Discovery Modülü ve Circuit Breaker.
- **Faz 3:** Redis Streams ile Asenkron İletişim ve Portfolio Engine.
- **Faz 4:** TimescaleDB Analiz Katmanı.
- **Faz 5:** UI/UX ve WebSocket Entegrasyonu.
