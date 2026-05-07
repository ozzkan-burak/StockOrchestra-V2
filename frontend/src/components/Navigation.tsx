import clsx from 'clsx';
import { useUIStore } from '@/stores/priceStore';

interface NavigationProps {
  activeView: string;
  onViewChange: (view: string) => void;
}

const tabs = [
  { id: 'dashboard', label: 'Dashboard' },
  { id: 'markets', label: 'Markets' },
  { id: 'portfolio', label: 'Portfolio' },
  { id: 'trade', label: 'Trade' },
];

export function Navigation({ activeView, onViewChange }: NavigationProps) {
  return (
    <nav className="bg-slate-900 border-b border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-14">
          <div className="flex items-center space-x-8">
            <div className="text-lg font-semibold text-white">
              StockOrchestra
            </div>
            
            <div className="flex space-x-1">
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  onClick={() => onViewChange(tab.id)}
                  className={clsx(
                    'px-3 py-2 text-sm font-medium transition-colors duration-150',
                    'rounded-md',
                    activeView === tab.id
                      ? 'bg-slate-800 text-white'
                      : 'text-slate-400 hover:text-white hover:bg-slate-800'
                  )}
                >
                  {tab.label}
                </button>
              ))}
            </div>
          </div>
          
          <div className="flex items-center space-x-4">
            <ConnectionStatus />
          </div>
        </div>
      </div>
    </nav>
  );
}

function ConnectionStatus() {
  const connected = useUIStore((state) => state.connected);
  const connectionError = useUIStore((state) => state.connectionError);
  
  if (connectionError) {
    return (
      <div className="flex items-center space-x-2 text-sm text-red-400">
        <div className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
        <span>Disconnected</span>
      </div>
    );
  }
  
  return (
    <div className="flex items-center space-x-2 text-sm text-emerald-400">
      <div className="w-2 h-2 rounded-full bg-emerald-500" />
      <span>Live</span>
    </div>
  );
}