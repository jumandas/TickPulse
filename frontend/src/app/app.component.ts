import { Component, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MarketService } from './market.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  tickerToAdd = '';

  constructor(public market: MarketService) {}

  ngOnInit(): void {
    void this.market.init();
  }

  // SVG line chart for the selected ticker, built from history ticks.
  // The chart is 600 by 220, with 8 px of padding on each side.
  readonly chartPoints = computed(() => {
    const ticks = this.market.history();
    if (ticks.length < 2) {
      return '';
    }

    const prices = ticks.map((t) => t.price);
    const min = Math.min(...prices);
    const max = Math.max(...prices);
    const span = max - min || 1;
    const width = 600;
    const height = 220;
    const pad = 8;

    return ticks
      .map((t, i) => {
        const x = pad + (i / (ticks.length - 1)) * (width - pad * 2);
        const y = pad + (1 - (t.price - min) / span) * (height - pad * 2);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  });

  readonly chartLast = computed(() => {
    const ticks = this.market.history();
    return ticks.length > 0 ? ticks[ticks.length - 1] : null;
  });

  addTicker(): void {
    const ticker = this.tickerToAdd.trim().toUpperCase();
    if (!ticker) {
      return;
    }
    this.tickerToAdd = '';
    void this.market.addToWatchlist(ticker);
  }

  anomalyLabel(type: number): string {
    if (type === 0) {
      return 'Price jump';
    }
    if (type === 1) {
      return 'Stale ticks';
    }
    return 'Missing ticks';
  }

  formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString();
  }
}
