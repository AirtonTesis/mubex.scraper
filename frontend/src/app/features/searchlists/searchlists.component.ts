import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api/api.service';
import { interval, Subscription, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';

export interface SearchList {
  id: string;
  name: string;
  keywords: string[];
  domains: string[];
  userId: string;
  createdAt: string;
  updatedAt: string;
  latestJobId: string | null;
  latestJobStatus: string | null;
  latestJobCreatedAt: string | null;
  totalJobs: number;
  completedJobs: number;
  failedJobs: number;
  totalItemsCollected: number;
}

@Component({
  selector: 'app-searchlists',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="searchlists-container">
      <div class="header">
        <div>
          <h1>Listas de Busca</h1>
          <p class="subtitle">{{ searchLists.length }} lista(s) • Atualiza a cada 3s</p>
        </div>
        <button (click)="showCreateForm()" class="btn-primary">+ Criar Nova Lista</button>
      </div>

      <!-- Create/Edit Form -->
      <div class="form-card glass-card" *ngIf="showForm">
        <h3>{{ editingId ? 'Editar Lista' : 'Criar Nova Lista' }}</h3>
        <form (ngSubmit)="saveSearchList()">
          <div class="form-group">
            <label>Nome</label>
            <input type="text" [(ngModel)]="formData.name" name="name" required placeholder="Ex: Monitoramento SEO" />
          </div>
          <div class="form-group">
            <label>Palavras-chave (uma por linha)</label>
            <textarea [(ngModel)]="keywordsText" name="keywords" rows="4" placeholder="google analytics&#10;react tutorial&#10;angular vs react"></textarea>
          </div>
          <div class="form-group">
            <label>Domínios para monitorar (um por linha)</label>
            <textarea [(ngModel)]="domainsText" name="domains" rows="3" placeholder="example.com&#10;mysite.com.br"></textarea>
          </div>
          <div class="form-actions">
            <button type="button" (click)="cancelForm()" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">{{ editingId ? 'Atualizar' : 'Criar' }}</button>
          </div>
        </form>
      </div>

      <!-- Search Lists Grid -->
      <div class="lists-grid">
        <div class="list-card glass-card" *ngFor="let list of searchLists">
          <!-- Header with name and status -->
          <div class="card-header">
            <h3>{{ list.name }}</h3>
            <span class="status-badge" *ngIf="list.latestJobStatus" [ngClass]="list.latestJobStatus.toLowerCase()">
              {{ getStatusLabel(list.latestJobStatus) }}
            </span>
            <span class="status-badge never-run" *ngIf="!list.latestJobStatus">
              Nunca executado
            </span>
          </div>

          <!-- Collected items -->
          <div class="collected-banner" *ngIf="list.totalItemsCollected > 0">
            <div class="collected-icon">📊</div>
            <div class="collected-info">
              <span class="collected-number">{{ list.totalItemsCollected }}</span>
              <span class="collected-label">itens coletados</span>
            </div>
          </div>

          <!-- Job stats bar -->
          <div class="job-stats" *ngIf="list.totalJobs > 0">
            <div class="stat-item">
              <span class="stat-number">{{ list.totalJobs }}</span>
              <span class="stat-label">Total</span>
            </div>
            <div class="stat-item success">
              <span class="stat-number">{{ list.completedJobs }}</span>
              <span class="stat-label">Sucesso</span>
            </div>
            <div class="stat-item danger">
              <span class="stat-number">{{ list.failedJobs }}</span>
              <span class="stat-label">Falha</span>
            </div>
            <div class="stat-item">
              <span class="stat-number">{{ list.totalJobs - list.completedJobs - list.failedJobs }}</span>
              <span class="stat-label">Ativos</span>
            </div>
          </div>

          <!-- Info -->
          <div class="list-info">
            <div class="info-item">
              <span class="label">Palavras-chave:</span>
              <div class="tags">
                <span class="tag" *ngFor="let kw of list.keywords">{{ kw }}</span>
              </div>
            </div>
            <div class="info-item" *ngIf="list.domains.length > 0">
              <span class="label">Domínios:</span>
              <div class="tags">
                <span class="tag domain" *ngFor="let d of list.domains">{{ d }}</span>
              </div>
            </div>
          </div>

          <!-- Progress bar when job is active -->
          <div class="progress-container" *ngIf="list.latestJobStatus === 'Active' || list.latestJobStatus === 'Pending'">
            <div class="progress-bar">
              <div class="progress-fill"></div>
            </div>
            <span class="progress-text">{{ list.latestJobStatus === 'Active' ? 'Processando...' : 'Aguardando...' }}</span>
          </div>

          <!-- Error message -->
          <div class="error-banner" *ngIf="list.latestJobStatus === 'Failed'">
            ⚠️ Último job falhou
          </div>

          <!-- Actions -->
          <div class="list-actions">
            <!-- Botao dinamico: Executar / Parar -->
            <ng-container *ngIf="list.latestJobStatus === 'Active' || list.latestJobStatus === 'Pending'">
              <button (click)="pauseJob(list)" class="btn-stop" [disabled]="stoppingId === list.id">
                <span *ngIf="stoppingId !== list.id">⏹ Parar</span>
                <span *ngIf="stoppingId === list.id" class="spinner"></span>
              </button>
            </ng-container>
            <ng-container *ngIf="list.latestJobStatus !== 'Active' && list.latestJobStatus !== 'Pending'">
              <button (click)="enqueueJob(list)" class="btn-run"
                      [disabled]="enqueueingId === list.id">
                <span *ngIf="enqueueingId !== list.id">▶ Executar</span>
                <span *ngIf="enqueueingId === list.id" class="spinner"></span>
              </button>
            </ng-container>
            <button (click)="editSearchList(list)" class="btn-secondary">Editar</button>
            <button (click)="deleteSearchList(list.id)" class="btn-danger">Excluir</button>
          </div>

          <!-- Last run info -->
          <div class="last-run" *ngIf="list.latestJobCreatedAt">
            Última execução: {{ list.latestJobCreatedAt | date:'dd/MM/yyyy HH:mm:ss' }}
          </div>
        </div>
      </div>

      <!-- Empty state -->
      <div class="empty-state glass-card" *ngIf="searchLists.length === 0 && !showForm">
        <div class="empty-icon">📋</div>
        <h2>Nenhuma lista de busca</h2>
        <p>Crie uma lista para monitorar posições de domínios nos resultados de busca do Google.</p>
        <button (click)="showCreateForm()" class="btn-primary">Criar Primeira Lista</button>
      </div>

      <!-- Toast notification -->
      <div class="toast" *ngIf="toastMessage" [ngClass]="toastType">
        {{ toastMessage }}
      </div>
    </div>
  `,
  styles: [`
    .searchlists-container {
      padding: 2rem;
      color: white;
      position: relative;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
    }

    h1 { margin: 0; font-size: 2rem; }
    .subtitle { margin: 0.25rem 0 0; color: rgba(255,255,255,0.5); font-size: 0.875rem; }

    .glass-card {
      background: rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(12px);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 16px;
      padding: 1.5rem;
      margin-bottom: 1.5rem;
      transition: all 0.3s ease;
    }

    .glass-card:hover {
      background: rgba(255, 255, 255, 0.12);
      border-color: rgba(255, 255, 255, 0.2);
    }

    .form-card { max-width: 600px; }
    .form-group { margin-bottom: 1rem; }

    label {
      display: block;
      color: rgba(255, 255, 255, 0.7);
      margin-bottom: 0.4rem;
      font-size: 0.85rem;
      font-weight: 500;
    }

    input, textarea {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 10px;
      background: rgba(255, 255, 255, 0.06);
      color: white;
      font-size: 0.95rem;
      box-sizing: border-box;
      font-family: inherit;
      transition: border-color 0.2s;
    }

    input:focus, textarea:focus {
      outline: none;
      border-color: rgba(79, 70, 229, 0.6);
    }

    input::placeholder, textarea::placeholder { color: rgba(255,255,255,0.3); }
    textarea { resize: vertical; }

    .form-actions { display: flex; gap: 0.5rem; justify-content: flex-end; margin-top: 1.5rem; }

    .btn-primary, .btn-secondary, .btn-danger, .btn-run {
      padding: 0.6rem 1.2rem;
      border: none;
      border-radius: 10px;
      cursor: pointer;
      font-weight: 600;
      font-size: 0.9rem;
      transition: all 0.2s;
    }

    .btn-primary { background: #4f46e5; color: white; }
    .btn-primary:hover { background: #4338ca; transform: translateY(-1px); }

    .btn-secondary { background: rgba(255, 255, 255, 0.15); color: white; }
    .btn-secondary:hover { background: rgba(255, 255, 255, 0.25); }

    .btn-danger { background: rgba(239, 68, 68, 0.2); color: #fca5a5; }
    .btn-danger:hover { background: rgba(239, 68, 68, 0.4); }

    .btn-run {
      background: rgba(16, 185, 129, 0.2);
      color: #6ee7b7;
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .btn-run:hover:not(:disabled) { background: rgba(16, 185, 129, 0.4); transform: translateY(-1px); }
    .btn-run:disabled { opacity: 0.4; cursor: not-allowed; }

    .btn-stop {
      padding: 0.6rem 1.2rem;
      border: none;
      border-radius: 10px;
      cursor: pointer;
      font-weight: 600;
      font-size: 0.9rem;
      transition: all 0.2s;
      background: rgba(239, 68, 68, 0.2);
      color: #fca5a5;
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .btn-stop:hover:not(:disabled) { background: rgba(239, 68, 68, 0.4); transform: translateY(-1px); }
    .btn-stop:disabled { opacity: 0.4; cursor: not-allowed; }

    .lists-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
      gap: 1.25rem;
      margin-bottom: 1.5rem;
    }

    .list-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
    }

    .card-header h3 {
      margin: 0;
      color: white;
      font-size: 1.15rem;
      flex: 1;
    }

    .status-badge {
      padding: 0.25rem 0.75rem;
      border-radius: 20px;
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      white-space: nowrap;
      flex-shrink: 0;
    }

    .status-badge.pending { background: rgba(251, 191, 36, 0.2); color: #fbbf24; border: 1px solid rgba(251, 191, 36, 0.3); }
    .status-badge.active { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); animation: pulse 2s infinite; }
    .status-badge.paused { background: rgba(107, 114, 128, 0.2); color: #9ca3af; border: 1px solid rgba(107, 114, 128, 0.3); }
    .status-badge.completed { background: rgba(59, 130, 246, 0.2); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.3); }
    .status-badge.failed { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.3); }
    .status-badge.never-run { background: rgba(255, 255, 255, 0.05); color: rgba(255, 255, 255, 0.4); border: 1px solid rgba(255, 255, 255, 0.1); }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.6; }
    }

    .job-stats {
      display: flex;
      gap: 1rem;
      padding: 0.75rem;
      background: rgba(255, 255, 255, 0.04);
      border-radius: 10px;
    }

    .stat-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      flex: 1;
    }

    .stat-number { font-size: 1.25rem; font-weight: 700; color: rgba(255,255,255,0.8); }
    .stat-label { font-size: 0.7rem; color: rgba(255,255,255,0.4); text-transform: uppercase; letter-spacing: 0.5px; }
    .stat-item.success .stat-number { color: #34d399; }
    .stat-item.danger .stat-number { color: #f87171; }

    .collected-banner {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.85rem 1rem;
      background: linear-gradient(135deg, rgba(59, 130, 246, 0.15), rgba(79, 70, 229, 0.15));
      border: 1px solid rgba(59, 130, 246, 0.2);
      border-radius: 10px;
    }

    .collected-icon {
      font-size: 1.5rem;
    }

    .collected-info {
      display: flex;
      flex-direction: column;
    }

    .collected-number {
      font-size: 1.6rem;
      font-weight: 800;
      background: linear-gradient(135deg, #60a5fa, #a78bfa);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      line-height: 1.1;
    }

    .collected-label {
      font-size: 0.75rem;
      color: rgba(255,255,255,0.5);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .list-info { display: flex; flex-direction: column; gap: 0.75rem; }
    .info-item .label { color: rgba(255, 255, 255, 0.5); font-size: 0.8rem; display: block; margin-bottom: 0.3rem; }

    .tags { display: flex; flex-wrap: wrap; gap: 0.35rem; }

    .tag {
      padding: 0.2rem 0.6rem;
      background: rgba(79, 70, 229, 0.15);
      border: 1px solid rgba(79, 70, 229, 0.25);
      border-radius: 6px;
      font-size: 0.78rem;
      color: rgba(255, 255, 255, 0.8);
    }

    .tag.domain {
      background: rgba(16, 185, 129, 0.12);
      border-color: rgba(16, 185, 129, 0.25);
      color: #6ee7b7;
    }

    .progress-container {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }

    .progress-bar {
      flex: 1;
      height: 4px;
      background: rgba(255, 255, 255, 0.1);
      border-radius: 2px;
      overflow: hidden;
    }

    .progress-fill {
      height: 100%;
      background: linear-gradient(90deg, #4f46e5, #7c3aed);
      border-radius: 2px;
      animation: progress-indeterminate 1.5s infinite ease-in-out;
    }

    @keyframes progress-indeterminate {
      0% { width: 0%; margin-left: 0; }
      50% { width: 60%; margin-left: 20%; }
      100% { width: 0%; margin-left: 100%; }
    }

    .progress-text { font-size: 0.75rem; color: rgba(255,255,255,0.5); white-space: nowrap; }

    .error-banner {
      padding: 0.5rem 0.75rem;
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.2);
      border-radius: 8px;
      font-size: 0.8rem;
      color: #fca5a5;
    }

    .list-actions { display: flex; gap: 0.5rem; }

    .last-run {
      font-size: 0.75rem;
      color: rgba(255, 255, 255, 0.35);
    }

    .spinner {
      width: 14px;
      height: 14px;
      border: 2px solid rgba(255,255,255,0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }

    @keyframes spin { to { transform: rotate(360deg); } }

    .empty-state {
      text-align: center;
      padding: 4rem 2rem;
    }

    .empty-icon { font-size: 3rem; margin-bottom: 1rem; }
    .empty-state h2 { margin: 0 0 0.5rem; color: rgba(255,255,255,0.8); }
    .empty-state p { color: rgba(255,255,255,0.5); margin-bottom: 1.5rem; }

    .toast {
      position: fixed;
      bottom: 2rem;
      right: 2rem;
      padding: 0.75rem 1.5rem;
      border-radius: 10px;
      font-size: 0.9rem;
      font-weight: 500;
      animation: slideIn 0.3s ease;
      z-index: 1000;
    }

    .toast.success { background: rgba(16, 185, 129, 0.9); color: white; }
    .toast.error { background: rgba(239, 68, 68, 0.9); color: white; }

    @keyframes slideIn {
      from { transform: translateY(20px); opacity: 0; }
      to { transform: translateY(0); opacity: 1; }
    }
  `]
})
export class SearchListsComponent implements OnInit, OnDestroy {
  searchLists: SearchList[] = [];
  showForm = false;
  editingId: string | null = null;
  formData = { name: '', keywords: [] as string[], domains: [] as string[] };
  keywordsText = '';
  domainsText = '';
  enqueueingId: string | null = null;
  stoppingId: string | null = null;
  toastMessage = '';
  toastType: 'success' | 'error' = 'success';

  private pollSub?: Subscription;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadSearchLists();
    // Polling a cada 3 segundos para atualizar status dos jobs
    // O catchError DENTRO do switchMap evita que a assinatura do
    // interval(3000) morra quando a API estiver offline.
    this.pollSub = interval(3000).pipe(
      switchMap(() => this.api.get<SearchList[]>('/searchlists').pipe(
        catchError(err => {
          console.error('Erro no polling de listas:', err);
          // API offline — resetar enqueueingId e stoppingId (requests que
          // podem estar travados) E os status Active/Pending (nao podem
          // estar rodando se API caiu).
          this.enqueueingId = null;
          this.stoppingId = null;
          this.searchLists.forEach(sl => {
            if (sl.latestJobStatus === 'Active' || sl.latestJobStatus === 'Pending') {
              sl.latestJobStatus = null;
              sl.latestJobId = null;
              sl.latestJobCreatedAt = null;
            }
          });
          return of(null);  // sinaliza "sem dados" sem quebrar o polling
        })
      ))
    ).subscribe({
      next: data => {
        if (data !== null) {
          this.searchLists = data;
        }
      },
      error: err => {
        console.error('Erro fatal no polling:', err);
      }
    });
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  loadSearchLists(): void {
    this.api.get<SearchList[]>('/searchlists').subscribe({
      next: data => this.searchLists = data,
      error: err => {
        console.error('Erro ao carregar listas:', err);
        this.showToast('Erro ao carregar listas. Verifique se a API está rodando.', 'error');
      }
    });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'Pending': 'Aguardando',
      'Active': 'Processando',
      'Paused': 'Pausado',
      'Completed': 'Concluído',
      'Failed': 'Falhou'
    };
    return labels[status] || status;
  }

  showCreateForm(): void {
    this.showForm = true;
    this.editingId = null;
    this.formData = { name: '', keywords: [], domains: [] };
    this.keywordsText = '';
    this.domainsText = '';
  }

  editSearchList(list: SearchList): void {
    this.showForm = true;
    this.editingId = list.id;
    this.formData = { name: list.name, keywords: list.keywords, domains: list.domains };
    this.keywordsText = list.keywords.join('\n');
    this.domainsText = list.domains.join('\n');
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
  }

  saveSearchList(): void {
    const data = {
      ...this.formData,
      keywords: this.keywordsText.split('\n').filter(k => k.trim()),
      domains: this.domainsText.split('\n').filter(d => d.trim())
    };

    if (this.editingId) {
      this.api.put(`/searchlists/${this.editingId}`, data).subscribe({
        next: () => {
          this.loadSearchLists();
          this.cancelForm();
          this.showToast('Lista atualizada com sucesso!', 'success');
        },
        error: () => this.showToast('Erro ao atualizar lista', 'error')
      });
    } else {
      this.api.post('/searchlists', data).subscribe({
        next: () => {
          this.loadSearchLists();
          this.cancelForm();
          this.showToast('Lista criada com sucesso!', 'success');
        },
        error: () => this.showToast('Erro ao criar lista', 'error')
      });
    }
  }

  deleteSearchList(id: string): void {
    if (confirm('Tem certeza que deseja excluir esta lista?')) {
      this.api.delete(`/searchlists/${id}`).subscribe({
        next: () => {
          this.loadSearchLists();
          this.showToast('Lista excluída', 'success');
        },
        error: () => this.showToast('Erro ao excluir lista', 'error')
      });
    }
  }

  enqueueJob(list: SearchList): void {
    this.enqueueingId = list.id;
    this.api.post('/jobs', { searchListId: list.id }).subscribe({
      next: () => {
        this.enqueueingId = null;
        this.showToast(`Job enfileirado para "${list.name}"`, 'success');
        this.loadSearchLists();
      },
      error: (err) => {
        this.enqueueingId = null;
        this.showToast('Erro ao enfileirar job: ' + (err.error?.title || err.message), 'error');
      }
    });
  }

  pauseJob(list: SearchList): void {
    if (!list.latestJobId) {
      this.showToast('Nenhum job ativo para pausar', 'error');
      return;
    }
    this.stoppingId = list.id;
    this.api.patch(`/jobs/${list.latestJobId}/pause`, {}).subscribe({
      next: () => {
        this.stoppingId = null;
        this.showToast(`Job "${list.name}" pausado`, 'success');
        this.loadSearchLists();
      },
      error: (err) => {
        this.stoppingId = null;
        this.showToast('Erro ao pausar job: ' + (err.error?.title || err.message), 'error');
        this.loadSearchLists();
      }
    });
  }

  private showToast(message: string, type: 'success' | 'error'): void {
    this.toastMessage = message;
    this.toastType = type;
    setTimeout(() => this.toastMessage = '', 4000);
  }
}
