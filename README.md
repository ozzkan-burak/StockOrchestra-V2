# StockOrchestra-MCP: Otonom Stok Yönetim Köprüsü

## Proje Başlığı: StockOrchestra-MCP: Otonom Stok Yönetim Köprüsü

## Kısa Tanım

Yapay zeka modellerinin (LLM) dış dünyadan izole yapısını kırarak, standart bir protokol (MCP) üzerinden şirketin yerel PostgreSQL veritabanına güvenli ve kontrollü erişim sağlamasıdır. Bu sunucu, modelin veritabanı şemasını anlamasına, stokları analiz etmesine ve otonom kararlar almasına olanak tanıyan bir köprü görevi görür.

---

## Senaryo

Kullanıcının Claude arayüzüne veya herhangi bir MCP istemcisine "Kritik seviyenin altına düşen ürünleri kontrol et ve tedarik süreci için satın alma taleplerini oluştur" talimatını vermesiyle süreç başlar. Ajan, arka planda senin yazdığın .NET MCP sunucusuna bağlanır; veritabanından anlık stok verisini çeker, eşik değerleri analiz eder ve eksik miktarlar için satın alma tablosuna (PurchaseRequests) otomatik kayıtlar atarak kullanıcıya süreci raporlar.

---

## Teknik Altyapı

- **Çalışma Zamanı:** .NET 8.0 / 9.0
- **Protokol:** Model Context Protocol (MCP)
- **Veritabanı:** PostgreSQL (Docker üzerinden)
- **İletişim:** JSON-RPC over stdio (Standart Girdi/Çıktı)
- **ORM:** Entity Framework Core

---

## Kurulum ve Başlatma

### 1. Veritabanı Hazırlığı

PostgreSQL'i Docker üzerinde hızlıca ayağa kaldırmak için ilgili dizinde şu komutu çalıştırın:

```bash
docker-compose up -d
```

2. İstemci Yapılandırması (Claude Desktop Örneği)
   Claude Desktop yapılandırma dosyanıza (claude_desktop_config.json) aşağıdaki sunucu tanımını ekleyin:

```
JSON
{
  "mcpServers": {
    "stock-orchestra": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/Projeler/StockOrchestra-MCP/src/StockOrchestra.Server/StockOrchestra.Server.csproj"],
      "env": {
        "CONNECTION_STRING": "Host=localhost;Database=StockDb;Username=postgres;Password=password"
      }
    }
  }
}
```

### 3. MCP Inspector'ı Kararlı Başlatma (Linux)

Inspector'da `disconnected` hatasını önlemek için aşağıdaki script'i kullanın:

```bash
chmod +x scripts/start-inspector.sh
./scripts/start-inspector.sh
```

Script eski Inspector süreçlerini ve çakışan portları (6274/6277) temizler,
tarayıcıyı otomatik açmadan Inspector'ı başlatır.
