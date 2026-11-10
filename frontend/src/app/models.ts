// Types matching the TickPulse API responses. The API serializes
// property names in camelCase.
export interface MarketTick {
  ticker: string;
  price: number;
  volume: number;
  timestamp: string;
  changePercent: number;
}

export interface Symbol {
  ticker: string;
  name: string;
  basePrice: number;
  volatility: number;
}

export interface WatchlistItem {
  id: number;
  ticker: string;
  addedAt: string;
}

export interface AnalystEvent {
  id: number;
  ticker: string;
  type: number;
  changePercent: number;
  detectedAt: string;
  summary: string;
  explanation: string;
  analystName: string;
}

export type ConnectionState = 'connecting' | 'connected' | 'disconnected';
