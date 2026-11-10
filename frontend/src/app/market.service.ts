import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { AnalystEvent, ConnectionState, MarketTick, Symbol, WatchlistItem } from './models';

// Talks to the TickPulse API over REST and SignalR. All live state is
// exposed as Angular signals so the UI updates as ticks arrive.
@Injectable({ providedIn: 'root' })
export class MarketService {
  readonly symbols = signal<Symbol[]>([]);
  readonly quotes = signal<Map<string, MarketTick>>(new Map());
  readonly history = signal<MarketTick[]>([]);
  readonly watchlist = signal<WatchlistItem[]>([]);
  readonly analystEvents = signal<AnalystEvent[]>([]);
  readonly analystName = signal<string>('rule-based');
  readonly connectionState = signal<ConnectionState>('connecting');
  readonly selectedTicker = signal<string>('PULS');

  readonly sortedQuotes = computed(() =>
    [...this.quotes().values()].sort((a, b) => a.ticker.localeCompare(b.ticker)));

  private connection: signalR.HubConnection | null = null;

  constructor(private http: HttpClient) {}

  async init(): Promise<void> {
    await this.loadSymbols();
    await this.loadQuotes();
    await this.loadWatchlist();
    await this.loadAnalystEvents();
    await this.loadAnalystStatus();
    await this.loadHistory(this.selectedTicker());
    this.connect();
  }

  selectTicker(ticker: string): void {
    this.selectedTicker.set(ticker);
    void this.loadHistory(ticker);
  }

  async addToWatchlist(ticker: string): Promise<void> {
    await this.http.post<WatchlistItem>('/api/watchlist', { ticker }).toPromise();
    await this.loadWatchlist();
  }

  async removeFromWatchlist(ticker: string): Promise<void> {
    await this.http.delete(`/api/watchlist/${ticker}`).toPromise();
    await this.loadWatchlist();
  }

  private loadSymbols(): Promise<void> {
    return new Promise((resolve) => {
      this.http.get<Symbol[]>('/api/symbols').subscribe({
        next: (symbols) => {
          this.symbols.set(symbols);
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private loadQuotes(): Promise<void> {
    return new Promise((resolve) => {
      this.http.get<MarketTick[]>('/api/quotes').subscribe({
        next: (ticks) => {
          const map = new Map<string, MarketTick>();
          ticks.forEach((t) => map.set(t.ticker, t));
          this.quotes.set(map);
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private loadHistory(ticker: string): Promise<void> {
    return new Promise((resolve) => {
      this.http.get<MarketTick[]>(`/api/history/${ticker}?minutes=60&limit=300`).subscribe({
        next: (ticks) => {
          this.history.set(ticks);
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private loadWatchlist(): Promise<void> {
    return new Promise((resolve) => {
      this.http.get<WatchlistItem[]>('/api/watchlist').subscribe({
        next: (items) => {
          this.watchlist.set(items);
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private loadAnalystEvents(): Promise<void> {
    return new Promise((resolve) => {
      this.http.get<AnalystEvent[]>('/api/analyst/events?limit=20').subscribe({
        next: (events) => {
          this.analystEvents.set(events);
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private loadAnalystStatus(): Promise<void> {
    return new Promise((resolve) => {
      this.http.get<{ analyst: string }>('/api/analyst/status').subscribe({
        next: (status) => {
          this.analystName.set(status.analyst);
          resolve();
        },
        error: () => resolve()
      });
    });
  }

  private connect(): void {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/market')
      .withAutomaticReconnect()
      .build();

    this.connection.on('Tick', (tick: MarketTick) => {
      this.quotes.update((map) => new Map(map).set(tick.ticker, tick));
      if (tick.ticker === this.selectedTicker()) {
        this.history.update((ticks) => [...ticks.slice(-299), tick]);
      }
    });

    this.connection.on('AnalystEvent', (event: AnalystEvent) => {
      this.analystEvents.update((events) => [event, ...events].slice(0, 20));
    });

    this.connection.onreconnecting(() => this.connectionState.set('connecting'));
    this.connection.onreconnected(() => this.connectionState.set('connected'));
    this.connection.onclose(() => this.connectionState.set('disconnected'));

    this.connection
      .start()
      .then(() => {
        this.connectionState.set('connected');
        return this.connection?.invoke('SubscribeAll');
      })
      .catch(() => this.connectionState.set('disconnected'));
  }
}
