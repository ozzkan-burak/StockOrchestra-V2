import { create } from 'zustand';

export interface PriceData {
  symbol: string;
  price: number;
  bidPrice?: number;
  askPrice?: number;
  change24h?: number;
  timestamp: Date;
  sources: string[];
}

export interface PortfolioPosition {
  symbol: string;
  quantity: number;
  currentPrice: number;
  totalValue: number;
  change24h?: number;
  change?: number;
  changePercent?: number;
}

interface PriceStore {
  prices: Map<string, PriceData>;
  lastUpdate: Map<string, Date>;
  setPrice: (price: PriceData) => void;
  setPrices: (prices: PriceData[]) => void;
  getPrice: (symbol: string) => PriceData | undefined;
  getAllPrices: () => PriceData[];
  clearStalePrices: (maxAgeMs: number) => void;
}

export const usePriceStore = create<PriceStore>((set, get) => ({
  prices: new Map(),
  lastUpdate: new Map(),
  
  setPrice: (price: PriceData) => {
    set((state) => {
      const newPrices = new Map(state.prices);
      const newLastUpdate = new Map(state.lastUpdate);
      
      newPrices.set(price.symbol, price);
      newLastUpdate.set(price.symbol, new Date());
      
      return { prices: newPrices, lastUpdate: newLastUpdate };
    });
  },
  
  setPrices: (prices: PriceData[]) => {
    set((state) => {
      const newPrices = new Map(state.prices);
      const newLastUpdate = new Map(state.lastUpdate);
      
      prices.forEach((price) => {
        newPrices.set(price.symbol, price);
        newLastUpdate.set(price.symbol, new Date());
      });
      
      return { prices: newPrices, lastUpdate: newLastUpdate };
    });
  },
  
  getPrice: (symbol: string) => {
    return get().prices.get(symbol);
  },
  
  getAllPrices: () => {
    return Array.from(get().prices.values());
  },
  
  clearStalePrices: (maxAgeMs: number) => {
    set((state) => {
      const newPrices = new Map(state.prices);
      const newLastUpdate = new Map(state.lastUpdate);
      const now = new Date();
      
      state.lastUpdate.forEach((lastUpdate, symbol) => {
        if (now.getTime() - lastUpdate.getTime() > maxAgeMs) {
          newPrices.delete(symbol);
          newLastUpdate.delete(symbol);
        }
      });
      
      return { prices: newPrices, lastUpdate: newLastUpdate };
    });
  },
}));

interface PortfolioStore {
  positions: Map<string, PortfolioPosition>;
  totalValue: number;
  setPosition: (position: PortfolioPosition) => void;
  setPositions: (positions: PortfolioPosition[]) => void;
  getPosition: (symbol: string) => PortfolioPosition | undefined;
  getAllPositions: () => PortfolioPosition[];
  updateTotalValue: () => void;
}

export const usePortfolioStore = create<PortfolioStore>((set, get) => ({
  positions: new Map(),
  totalValue: 0,
  
  setPosition: (position: PortfolioPosition) => {
    set((state) => {
      const newPositions = new Map(state.positions);
      newPositions.set(position.symbol, position);
      
      const totalValue = Array.from(newPositions.values())
        .reduce((sum, pos) => sum + pos.totalValue, 0);
      
      return { positions: newPositions, totalValue };
    });
  },
  
  setPositions: (positions: PortfolioPosition[]) => {
    set(() => {
      const newPositions = new Map<string, PortfolioPosition>();
      let totalValue = 0;
      
      positions.forEach((position) => {
        newPositions.set(position.symbol, position);
        totalValue += position.totalValue;
      });
      
      return { positions: newPositions, totalValue };
    });
  },
  
  getPosition: (symbol: string) => {
    return get().positions.get(symbol);
  },
  
  getAllPositions: () => {
    return Array.from(get().positions.values());
  },
  
  updateTotalValue: () => {
    const positions = get().positions;
    const totalValue = Array.from(positions.values())
      .reduce((sum, pos) => sum + pos.totalValue, 0);
    set({ totalValue });
  },
}));

interface UIStore {
  connected: boolean;
  connectionError: string | null;
  selectedSymbol: string;
  setConnected: (connected: boolean) => void;
  setConnectionError: (error: string | null) => void;
  setSelectedSymbol: (symbol: string) => void;
}

export const useUIStore = create<UIStore>((set) => ({
  connected: false,
  connectionError: null,
  selectedSymbol: 'BTC',
  
  setConnected: (connected: boolean) => set({ connected }),
  setConnectionError: (connectionError: string | null) => set({ connectionError }),
  setSelectedSymbol: (selectedSymbol: string) => set({ selectedSymbol }),
}));