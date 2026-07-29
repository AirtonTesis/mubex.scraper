import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';

export interface DashboardMetrics {
  totalSearches: number;
  successRate: number;
  failureRate: number;
  activeExecutions: number;
  startDate: string;
  endDate: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="dashboard-container">
      <h1>Dashboard</h1>

      <div class="filters glass-card">
        <div class="filter-group">
          <button
            *ngFor="let preset of datePresets"
            [class.active]="selectedPreset === preset.value"
            (click)="selectPreset(preset.value)"
            class="btn-filter"
          >
            {{ preset.label }}
          </button>
        </div>
        <div class="custom-range" *ngIf="selectedPreset === 'custom'">
          <input type="date" [(ngModel)]="startDate" />
          <span>até</span>
          <input type="date" [(ngModel)]="endDate" />
          <button (click)="applyCustomRange()" class="btn-primary">Aplicar</button>
        </div>
      </div>

      <div class="metrics-grid">
        <div class="metric-card glass-card">
          <div class="metric-value">{{ metrics?.totalSearches || 0 }}</div>
          <div class="metric-label">Total de Buscas</div>
        </div>
        <div class="metric-card glass-card">
          <div class="metric-value success">{{ metrics?.successRate || 0 }}%</div>
          <div class="metric-label">Taxa de Sucesso</div>
        </div>
        <div class="metric-card glass-card">
          <div class="metric-value danger">{{ metrics?.failureRate || 0 }}%</div>
          <div class="metric-label">Taxa de Falha</div>
        </div>
        <div class="metric-card glass-card">
          <div class="metric-value active">{{ metrics?.activeExecutions || 0 }}</div>
          <div class="metric-label">Execuções Ativas</div>
        </div>
      </div>

      <div class="api-info glass-card">
        <h3>API Endpoints</h3>
        <div class="endpoint">
          <span class="method get">GET</span>
          <span class="path">/api/dashboard/metrics</span>
        </div>
        <div class="response" *ngIf="metrics">
          <pre>{{ metrics | json }}</pre>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-container {
      padding: 2rem;
      color: white;
    }

    h1 {
      margin-bottom: 1.5rem;
      font-size: 2rem;
    }

    .glass-card {
      background: rgba(255, 255, 255, 0.1);
      backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 12px;
      padding: 1.5rem;
      margin-bottom: 1.5rem;
    }

    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      align-items: center;
    }

    .filter-group {
      display: flex;
      gap: 0.5rem;
    }

    .btn-filter {
      padding: 0.5rem 1rem;
      border: 1px solid rgba(255, 255, 255, 0.3);
      border-radius: 8px;
      background: transparent;
      color: white;
      cursor: pointer;
      transition: all 0.2s;
    }

    .btn-filter:hover, .btn-filter.active {
      background: #4f46e5;
      border-color: #4f46e5;
    }

    .custom-range {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    input[type="date"] {
      padding: 0.5rem;
      border: 1px solid rgba(255, 255, 255, 0.3);
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.1);
      color: white;
    }

    .btn-primary {
      padding: 0.5rem 1rem;
      background: #4f46e5;
      color: white;
      border: none;
      border-radius: 8px;
      cursor: pointer;
    }

    .metrics-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
    }

    .metric-card {
      text-align: center;
    }

    .metric-value {
      font-size: 2.5rem;
      font-weight: 700;
      color: white;
    }

    .metric-value.success { color: #10b981; }
    .metric-value.danger { color: #f87171; }
    .metric-value.active { color: #fbbf24; }

    .metric-label {
      color: rgba(255, 255, 255, 0.7);
      margin-top: 0.5rem;
    }

    .api-info h3 {
      margin-bottom: 1rem;
    }

    .endpoint {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .method {
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 700;
    }

    .method.get { background: #10b981; }
    .method.post { background: #3b82f6; }

    .path {
      font-family: monospace;
      color: rgba(255, 255, 255, 0.8);
    }

    .response pre {
      background: rgba(0, 0, 0, 0.3);
      padding: 1rem;
      border-radius: 8px;
      overflow-x: auto;
      color: #10b981;
    }
  `]
})
export class DashboardComponent implements OnInit {
  metrics: DashboardMetrics | null = null;
  startDate = '';
  endDate = '';
  selectedPreset = 'week';

  datePresets = [
    { label: 'Último Dia', value: 'day' },
    { label: 'Última Semana', value: 'week' },
    { label: 'Último Mês', value: 'month' },
    { label: 'Personalizado', value: 'custom' }
  ];

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadMetrics('week');
  }

  selectPreset(preset: string): void {
    this.selectedPreset = preset;
    if (preset !== 'custom') {
      this.loadMetrics(preset);
    }
  }

  applyCustomRange(): void {
    if (this.startDate && this.endDate) {
      this.api.get<DashboardMetrics>('/dashboard/metrics', {
        startDate: this.startDate,
        endDate: this.endDate
      }).subscribe(data => {
        this.metrics = data;
      });
    }
  }

  private loadMetrics(preset: string): void {
    const now = new Date();
    let startDate: Date;

    switch (preset) {
      case 'day':
        startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000);
        break;
      case 'month':
        startDate = new Date(now.getFullYear(), now.getMonth() - 1, now.getDate());
        break;
      case 'week':
      default:
        startDate = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    }

    this.api.get<DashboardMetrics>('/dashboard/metrics', {
      startDate: startDate.toISOString(),
      endDate: now.toISOString()
    }).subscribe(data => {
      this.metrics = data;
    });
  }
}
