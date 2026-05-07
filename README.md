# StockOrchestra-V2: Gerçek Zamanlı Veri Orkestrasyonu

## Proje Başlığı: StockOrchestra-V2: Dağıtık ve Olay Güdümlü Veri Hattı

## Kısa Tanım

Yüksek frekanslı finansal verilerin darboğaz yaşamadan işlenebilmesi için tasarlanmış; veriyi dış API'lerden toplayıp Redis Streams üzerinden mikroservislere dağıtan, TimescaleDB ile analitik derinlik kazandıran ve Next.js arayüzü üzerinden son kullanıcıya milisaniyeler içinde ulaştıran olay güdümlü (event-driven) bir sistem mimarisidir.

---

## Senaryo

Price-Discovery servisi, dış kaynaklardan (örneğin Binance) aldığı anlık XAUUSD (Altın) fiyatını işler ve Redis Streams kanalına yayınlar. Arka planda çalışan Analytical-Store servisi bu veriyi stream üzerinden okuyarak TimescaleDB'ye kaydederken, aynı anda SignalR Hub bu güncel fiyatı WebSocket üzerinden fırlatır. Kullanıcı, sayfayı yenilemeye gerek kalmadan Next.js dashboard'u üzerindeki fiyatların ve grafiklerin canlı olarak güncellendiğini görür.

---

## Teknik Altyapı

- **Arka Plan (Backend):** .NET 8.0 / 9.0 (Mikroservisler)
- **Ön Yüz (Frontend):** Next.js (React Framework)
- **Mesaj Kuyruğu:** Redis Streams (Event-Driven Architecture)
- **Veritabanı:** TimescaleDB (Zaman serisi analitik verileri için)
- **Gerçek Zamanlı İletişim:** SignalR (WebSockets)
- **Konteynerleştirme:** Docker & Kubernetes

---

## Kurulum ve Başlatma

### 1. Mikroservis Ekosistemi ve Veritabanı Hazırlığı

Sistemin belkemiğini oluşturan Redis, TimescaleDB ve .NET veri toplayıcı servislerini Docker üzerinde hızlıca ayağa kaldırmak için kök dizinde şu komutu çalıştırın:

```

bash
# Tüm servis imajlarını yeniden inşa eder (--build) ve konteynerleri arka planda (-d) çalıştırır
docker-compose up --build -d
2. Arayüzün (Frontend) Başlatılması
Arka planda veri akışı ve altyapı hazırlandıktan sonra, gerçek zamanlı verileri izleyeceğiniz arayüzü ayağa kaldırmak için aşağıdaki adımları izleyin:

Bash
# Next.js projesinin bulunduğu klasöre geçiş yapar
cd frontend

# Next.js geliştirme sunucusunu başlatır (Varsayılan: localhost:3000)
npm run dev
