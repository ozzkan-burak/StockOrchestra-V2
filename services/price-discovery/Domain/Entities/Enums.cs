namespace PriceDiscovery.Domain.Entities;

/// <summary>
/// Fiyat kaynakları - Sistemin veri aldığı tüm kaynaklar
/// </summary>
public enum PriceSource
{
    /// <summary>Binance - Kripto para borsası</summary>
    Binance = 1,
    
    /// <summary>CoinGecko - Kripto para agregatörü</summary>
    CoinGecko = 2,
    
    /// <summary>Yahoo Finance - Hisse senedi ve endeks verileri</summary>
    YahooFinance = 3,
    
    /// <summary>Alpha Vantage - Finansal veri API'si</summary>
    AlphaVantage = 4,
    
    /// <summary>Exchange Rate API - Döviz kurları</summary>
    ExchangeRateApi = 5,
    
    /// <summary>Mock - Test için sahte veri</summary>
    Mock = 98,
    
    /// <summary> manuel veyaInternal referans fiyatı</summary>
    Internal = 99
}

/// <summary>
/// Varlık türleri - Fetcher'ın hangi piyasalara eriştiğini belirtir
/// </summary>
public enum AssetType
{
    /// <summary>Kripto para birimleri</summary>
    Crypto = 1,
    
    /// <summary>Hisse senetleri</summary>
    Stock = 2,
    
    /// <summary>Borsa yatırım fonları</summary>
    Etf = 3,
    
    /// <summary>Döviz kurları</summary>
    Forex = 4,
    
    /// <summary>Emtia (altın, gümüş vb.)</summary>
    Commodity = 5,
    
    /// <summary>Tahvil ve bono</summary>
    Bond = 6
}

/// <summary>
/// Fiyat doğrulama durumu
/// </summary>
public enum PriceValidationStatus
{
    /// <summary>Fiyat geçerli</summary>
    Valid = 1,
    
    /// <summary>Fiyat çok eski (staleness)</summary>
    Stale = 2,
    
    /// <summary>Fiyat sıfır veya negatif</summary>
    InvalidPrice = 3,
    
    /// <summary>Fiyat aşırı sapma gösteriyor</summary>
    PriceDeviation = 4,
    
    /// <summary>Ağ hatası</summary>
    NetworkError = 5,
    
    /// <summary>Kaynak hatası</summary>
    SourceError = 6
}