import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { usePriceStore, useUIStore, PriceData } from '@/stores/priceStore';

const THROTTLE_MS = 100;
const POLL_INTERVAL_MS = 2000;

export function usePriceSocket(gatewayUrl: string = 'http://localhost:5000') {
  const isThrottledRef = useRef<Map<string, number>>(new Map());
  const pollIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const connectionAttemptedRef = useRef(false);

  const isThrottled = (symbol: string): boolean => {
    const now = Date.now();
    const lastUpdate = isThrottledRef.current.get(symbol) || 0;
    if (now - lastUpdate < THROTTLE_MS) return true;
    isThrottledRef.current.set(symbol, now);
    return false;
  };

  const fetchPrices = async () => {
    console.log('fetchPrices: called');
    const symbols = ['BTC', 'ETH', 'AAPL', 'GOOGL', 'TSLA', 'XAUUSD'];
    const setPrice = usePriceStore.getState().setPrice;

    for (const symbol of symbols) {
      try {
        const url = `${gatewayUrl}/api/prices?symbol=${symbol}`;
        console.log('Fetching:', url);
        const response = await fetch(url);
        console.log('Response:', response.status, response.ok);
        if (response.ok) {
          const data = await response.json();
          console.log('Data for', symbol, ':', data?.Price);
          if (data && !isThrottled(symbol)) {
            setPrice({
              symbol: data.Symbol,
              price: data.Price,
              bidPrice: data.BidPrice,
              askPrice: data.AskPrice,
              change24h: data.Change24h,
              timestamp: new Date(data.Timestamp),
              sources: data.Sources || []
            });
            console.log('Price set for', symbol, ':', data.Price);
          }
        }
      } catch (err) {
        console.error(`Failed to fetch price for ${symbol}:`, err);
      }
    }
  };

  useEffect(() => {
    console.log('usePriceSocket: Effect running');
    const setConnected = useUIStore.getState().setConnected;
    const setConnectionError = useUIStore.getState().setConnectionError;

    // Start polling immediately
    pollIntervalRef.current = setInterval(fetchPrices, POLL_INTERVAL_MS);
    console.log('Polling started');
    
    // Try SignalR first
    if (!connectionAttemptedRef.current) {
      connectionAttemptedRef.current = true;
      try {
        const connection = new signalR.HubConnectionBuilder()
          .withUrl(`${gatewayUrl}/price-hub`)
          .withAutomaticReconnect([0, 1000, 5000, 10000])
          .configureLogging(signalR.LogLevel.Warning)
          .build();

        connection.on('PriceUpdated', (data: PriceData) => {
          if (data?.symbol && !isThrottled(data.symbol)) {
            usePriceStore.getState().setPrice({
              ...data,
              timestamp: new Date(data.timestamp)
            });
          }
        });

        connection.onreconnecting(() => {
          setConnectionError('Reconnecting...');
          setConnected(false);
        });

        connection.onreconnected(() => {
          setConnected(true);
          setConnectionError(null);
        });

        connection.onclose(() => {
          setConnected(false);
          // Start polling fallback
          pollIntervalRef.current = setInterval(fetchPrices, POLL_INTERVAL_MS);
        });

        connection.start()
          .then(() => {
            setConnected(true);
            setConnectionError(null);
          })
          .catch((err) => {
            console.warn('SignalR failed:', err);
            setConnectionError('Using polling mode');
          });
      } catch (err) {
        console.warn('SignalR setup failed:', err);
        setConnectionError('Using polling mode');
      }
    }

    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
      }
    };
  }, [gatewayUrl]);

  return {};
}