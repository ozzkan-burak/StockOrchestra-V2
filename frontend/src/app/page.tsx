"use client";

import { useState, useMemo, useRef, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Navigation } from '@/components/Navigation';
import { usePriceSocket } from '@/hooks/usePriceSocket';
import { usePortfolioStore, usePriceStore, useUIStore } from '@/stores/priceStore';
import { createChart, IChartApi, CandlestickSeries } from 'lightweight-charts';
import { shallow } from 'zustand/shallow';

const queryClient = new QueryClient();

export default function Home() {
  const [activeView, setActiveView] = useState('dashboard');
  const selectedSymbol = useUIStore((state) => state.selectedSymbol, shallow);
  const setSelectedSymbol = useUIStore((state) => state.setSelectedSymbol, shallow);
  
  usePriceSocket();
  
  const symbols = ['BTC', 'ETH', 'AAPL', 'GOOGL', 'TSLA', 'XAUUSD'];
  
  return (
    <QueryClientProvider client={queryClient}>
      <div className="min-h-screen bg-slate-950">
        <Navigation activeView={activeView} onViewChange={setActiveView} />
        
        <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div className="grid grid-cols-4 gap-6">
            <div className="col-span-4 lg:col-span-3">
              <div className="bg-slate-900 rounded-lg border border-slate-800 p-4">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex space-x-2">
                    {symbols.map((symbol) => (
                      <button
                        key={symbol}
                        onClick={() => setSelectedSymbol(symbol)}
                        className={`px-3 py-1.5 text-sm font-medium rounded transition-colors ${
                          selectedSymbol === symbol
                            ? 'bg-blue-600 text-white'
                            : 'bg-slate-800 text-slate-300 hover:bg-slate-700'
                        }`}
                      >
                        {symbol}
                      </button>
                    ))}
                  </div>
                  <CurrentPriceDisplay symbol={selectedSymbol} />
                </div>
                
                <PriceChart symbol={selectedSymbol} />
              </div>
            </div>
            
            <div className="col-span-4 lg:col-span-1">
              <div className="bg-slate-900 rounded-lg border border-slate-800 p-4">
                <h2 className="text-lg font-semibold text-white mb-4">Portfolio Value</h2>
                <PortfolioSummary />
              </div>
            </div>
            
            <div className="col-span-4">
              <div className="bg-slate-900 rounded-lg border border-slate-800 p-4">
                <h2 className="text-lg font-semibold text-white mb-4">Assets</h2>
                <AssetTable />
              </div>
            </div>
          </div>
        </main>
      </div>
    </QueryClientProvider>
  );
}

function PriceChart({ symbol }: { symbol: string }) {
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  
  useEffect(() => {
    if (!chartContainerRef.current || !chartContainerRef.current.clientWidth) return;
    
    if (chartRef.current) {
      try {
        chartRef.current.remove();
      } catch (e) {
        // Already disposed
      }
      chartRef.current = null;
    }
    
    const chart = createChart(chartContainerRef.current, {
      width: chartContainerRef.current.clientWidth,
      height: 400,
      layout: {
        background: { color: '#0f172a' },
        textColor: '#94a3b8',
      },
      grid: {
        vertLines: { color: '#1e293b' },
        horzLines: { color: '#1e293b' },
      },
      rightPriceScale: {
        borderColor: '#334155',
      },
      timeScale: {
        borderColor: '#334155',
      },
    });
    
    const candlestick = chart.addSeries(CandlestickSeries, {
      upColor: '#22c55e',
      downColor: '#ef4444',
      borderUpColor: '#22c55e',
      borderDownColor: '#ef4444',
      wickUpColor: '#22c55e',
      wickDownColor: '#ef4444',
    });
    
    const mockData = [
      { time: 1704067200, open: 42000, high: 42500, low: 41800, close: 42200 },
      { time: 1704070800, open: 42200, high: 43000, low: 42100, close: 42800 },
      { time: 1704074400, open: 42800, high: 43500, low: 42700, close: 43200 },
      { time: 1704078000, open: 43200, high: 43800, low: 43000, close: 43500 },
      { time: 1704081600, open: 43500, high: 44000, low: 43300, close: 43800 },
    ];
    candlestick.setData(mockData as any);
    
    chart.timeScale().fitContent();
    
    chartRef.current = chart;
    
    const handleResize = () => {
      if (chartContainerRef.current) {
        chart.applyOptions({ width: chartContainerRef.current.clientWidth });
      }
    };
    
    window.addEventListener('resize', handleResize);
    
    return () => {
      window.removeEventListener('resize', handleResize);
      if (chartRef.current) {
        chartRef.current.remove();
        chartRef.current = null;
      }
    };
  }, [symbol]);
  
  return <div ref={chartContainerRef} className="w-full h-96" />;
}

function CurrentPriceDisplay({ symbol }: { symbol: string }) {
  const priceData = usePriceStore((state) => state.getPrice(symbol));
  
  console.log('CurrentPriceDisplay:', symbol, priceData);
  
  if (!priceData) {
    return <div className="text-slate-400">Loading price for {symbol}...</div>;
  }
  
  return (
    <div className="flex items-center space-x-4">
      <div className="text-2xl font-bold text-white">
        ${priceData.price.toLocaleString()}
      </div>
      {priceData.change24h !== undefined && priceData.change24h !== null && (
        <div className={`text-sm ${priceData.change24h >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
          {priceData.change24h >= 0 ? '+' : ''}{priceData.change24h.toFixed(2)}%
        </div>
      )}
    </div>
  );
}

function PortfolioSummary() {
  const totalValue = usePortfolioStore((state) => state.totalValue, shallow);
  
  return (
    <div>
      <div className="text-3xl font-bold text-white">
        ${totalValue.toLocaleString('en-US', { minimumFractionDigits: 2 })}
      </div>
      <div className="text-sm text-slate-400 mt-1">Total Value</div>
    </div>
  );
}

function AssetTable() {
  const positions = usePortfolioStore((state) => state.positions);
  const positionList = useMemo(() => Array.from(positions.values()), [positions]);
  
  if (positionList.length === 0) {
    return <div className="text-center py-8 text-slate-400">No assets yet. Start trading!</div>;
  }
  
  return (
    <div className="overflow-x-auto">
      <table className="w-full">
        <thead>
          <tr className="border-b border-slate-800">
            <th className="text-left py-3 text-sm font-medium text-slate-400">Asset</th>
            <th className="text-right py-3 text-sm font-medium text-slate-400">Qty</th>
            <th className="text-right py-3 text-sm font-medium text-slate-400">Price</th>
            <th className="text-right py-3 text-sm font-medium text-slate-400">Value</th>
            <th className="text-right py-3 text-sm font-medium text-slate-400">24h</th>
          </tr>
        </thead>
        <tbody>
          {positionList.map((pos) => (
            <tr key={pos.symbol} className="border-b border-slate-800/50">
              <td className="py-3 font-medium text-white">{pos.symbol}</td>
              <td className="py-3 text-right text-slate-300">{pos.quantity.toFixed(8)}</td>
              <td className="py-3 text-right text-white">${pos.currentPrice.toLocaleString()}</td>
              <td className="py-3 text-right text-white">${pos.totalValue.toLocaleString()}</td>
              <td className={`py-3 text-right ${(pos.change24h || 0) >= 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                {((pos.change24h || 0) >= 0 ? '+' : '')}{(pos.change24h || 0).toFixed(2)}%
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}