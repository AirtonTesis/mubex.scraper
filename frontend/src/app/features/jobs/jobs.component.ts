import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';
import { interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';

export interface Job {
  id: string;
  searchListId: string;
  searchListName: string;
  status: string;
  startedAt: string | null;
  completedAt: string | null;
  retryCount: number;
  errorMessage: string | null;
  createdAt: string;
}

@Component({
  selector: 'app-jobs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="jobs-container">
      <h1>Gerenciamento de Jobs</h1>

      <!-- Enqueue Job -->
      <div class="glass-card enqueue-card">
        <h3>Criar Novo Job</h3>
        <div class="enqueue-form">
          <select [(ngModel)]="selectedSearchListId" class="select-input">
            <option value="">Selecione uma lista de busca</option>
            <option *ngFor="let list of searchLists" [value]="list.id">
              {{ list.name }}
            </option>
          </select>
          <button (click)="enqueueJob()" class="btn-primary" [disabled]="!selectedSearchListId || enqueueing">
            {{ enqueueing ? 'Enfileirando...' : 'Enfileirar Job' }}
          </button>
        </div>
        <p class="success-msg" *ngIf="successMsg">{{ successMsg }}</p>
        <p class="error-msg" *ngIf="errorMsg">{{ errorMsg }}</p>
      </div>

      <!-- Jobs List -->
      <div class="glass-card">
        <div class="list-header">
          <h3>Jobs (atualiza automaticamente)</h3>
          <span class="refresh-indicator">🔄 {{ lastRefresh }}</span>
        </div>

        <div *ngIf="jobs.length === 0" class="empty-state">
          <p>Nenhum job encontrado. Crie um job acima.</p>
        </div>

        <table class="jobs-table" *ngIf="jobs.length > 0">
          <thead>
            <tr>
              <th>ID</th>
              <th>Lista</th>
              <th>Status</th>
              <th>Tentativas</th>
              <th>Iniciado</th>
              <th>Concluído</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let job of jobs">
              <td class="mono">{{ job.id | slice:0:8 }}...</td>
              <td>{{ job.searchListName }}</td>
              <td>
                <span class="status-badge" [ngClass]="job.status.toLowerCase()">
                  {{ job.status }}
                </span>
              </td>
              <td>{{ job.retryCount }}</td>
              <td>{{ job.startedAt ? (job.startedAt | date:'HH:mm:ss') : '-' }}</td>
              <td>{{ job.completedAt ? (job.completedAt | date:'HH:mm:ss') : '-' }}</td>
              <td class="actions">
                <button *ngIf="job.status === 'Active'" (click)="pauseJob(job.id)" class="btn-sm btn-warn">
                  Pausar
                </button>
                <button *ngIf="job.status === 'Paused'" (click)="activateJob(job.id)" class="btn-sm btn-success">
                  Retomar
                </button>
                <span *ngIf="job.status === 'Completed'" class="done">✅</span>
                <span *ngIf="job.status === 'Failed'" class="failed" [title]="job.errorMessage || ''">❌</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .jobs-container { padding: 2rem; color: white; }
    h1 { margin-bottom: 1.5rem; font-size: 2rem; }

    .glass-card {
      background: rgba(255,255,255,0.1);
      backdrop-filter: blur(10px);
      border: 1px solid rgba(255,255,255,0.2);
      border-radius: 12px;
      padding: 1.5rem;
      margin-bottom: 1.5rem;
    }

    .enqueue-card { max-width: 640px; }

    .enqueue-form { display: flex; gap: 1rem; align-items: center; margin-top: 1rem; }

    .select-input {
      flex: 1; padding: 0.75rem;
      border: 1px solid rgba(255,255,255,0.2);
      border-radius: 8px;
      background: rgba(255,255,255,0.1);
      color: white; font-size: 1rem;
    }
    .select-input option { background: #1e1e2e; }

    .btn-primary {
      padding: 0.75rem 1.5rem; background: #4f46e5;
      color: white; border: none; border-radius: 8px;
      cursor: pointer; font-weight: 600; white-space: nowrap;
    }
    .btn-primary:hover:not(:disabled) { background: #4338ca; }
    .btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

    .btn-sm { padding: 0.3rem 0.75rem; border: none; border-radius: 4px; cursor: pointer; font-size: 0.8rem; }
    .btn-warn { background: #f59e0b; color: #000; }
    .btn-success { background: #10b981; color: #fff; }

    .success-msg { margin-top: 0.75rem; color: #86efac; font-size: 0.9rem; }
    .error-msg { margin-top: 0.75rem; color: #fca5a5; font-size: 0.9rem; }

    .list-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .refresh-indicator { font-size: 0.8rem; color: rgba(255,255,255,0.5); }

    .jobs-table { width: 100%; border-collapse: collapse; }
    .jobs-table th, .jobs-table td {
      padding: 0.75rem; text-align: left;
      border-bottom: 1px solid rgba(255,255,255,0.1);
      font-size: 0.875rem;
    }
    .jobs-table th { color: rgba(255,255,255,0.6); font-weight: 600; }

    .mono { font-family: monospace; color: rgba(255,255,255,0.6); }

    .status-badge {
      padding: 0.2rem 0.6rem; border-radius: 4px;
      font-size: 0.75rem; font-weight: 700; text-transform: uppercase;
    }
    .status-badge.pending   { background: #fbbf24; color: #000; }
    .status-badge.active    { background: #10b981; color: #fff; }
    .status-badge.paused    { background: #6b7280; color: #fff; }
    .status-badge.completed { background: #3b82f6; color: #fff; }
    .status-badge.failed    { background: #ef4444; color: #fff; }

    .actions { display: flex; gap: 0.5rem; align-items: center; }

    .empty-state { text-align: center; padding: 2rem; color: rgba(255,255,255,0.5); }
  `]
})
export class JobsComponent implements OnInit, OnDestroy {
  searchLists: any[] = [];
  jobs: Job[] = [];
  selectedSearchListId = '';
  enqueueing = false;
  successMsg = '';
  errorMsg = '';
  lastRefresh = 'nunca';

  private pollSub?: Subscription;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadSearchLists();
    this.loadJobs();
    // Polling automático a cada 3 segundos
    this.pollSub = interval(3000).pipe(
      switchMap(() => this.api.get<Job[]>('/jobs'))
    ).subscribe(jobs => {
      this.jobs = jobs;
      this.lastRefresh = new Date().toLocaleTimeString('pt-BR');
    });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  loadSearchLists(): void {
    this.api.get<any[]>('/searchlists').subscribe({
      next: data => this.searchLists = data,
      error: () => {}
    });
  }

  loadJobs(): void {
    this.api.get<Job[]>('/jobs').subscribe({
      next: data => {
        this.jobs = data;
        this.lastRefresh = new Date().toLocaleTimeString('pt-BR');
      },
      error: () => {}
    });
  }

  enqueueJob(): void {
    if (!this.selectedSearchListId) return;
    this.enqueueing = true;
    this.successMsg = '';
    this.errorMsg = '';

    this.api.post('/jobs', { searchListId: this.selectedSearchListId }).subscribe({
      next: () => {
        this.enqueueing = false;
        this.successMsg = 'Job enfileirado com sucesso! Aguarde o processamento...';
        this.selectedSearchListId = '';
        setTimeout(() => this.successMsg = '', 5000);
        this.loadJobs();
      },
      error: (err) => {
        this.enqueueing = false;
        this.errorMsg = 'Erro ao enfileirar job: ' + (err.error?.title || err.message);
      }
    });
  }

  pauseJob(id: string): void {
    this.api.patch(`/jobs/${id}/pause`).subscribe({
      next: () => this.loadJobs(),
      error: err => console.error('Erro ao pausar:', err)
    });
  }

  activateJob(id: string): void {
    this.api.patch(`/jobs/${id}/activate`).subscribe({
      next: () => this.loadJobs(),
      error: err => console.error('Erro ao ativar:', err)
    });
  }
}
