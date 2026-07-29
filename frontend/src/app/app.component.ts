import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app-container" *ngIf="isAuthenticated; else loginView">
      <nav class="sidebar glass-nav">
        <div class="logo">
          <h2>Scraper</h2>
        </div>
        <ul class="nav-links">
          <li>
            <a routerLink="/dashboard" routerLinkActive="active">
              <span class="icon">📊</span>
              <span class="text">Dashboard</span>
            </a>
          </li>
          <li>
            <a routerLink="/searchlists" routerLinkActive="active">
              <span class="icon">📋</span>
              <span class="text">Listas</span>
            </a>
          </li>
          <li>
            <a routerLink="/jobs" routerLinkActive="active">
              <span class="icon">⚡</span>
              <span class="text">Jobs</span>
            </a>
          </li>
        </ul>
        <div class="nav-footer">
          <button (click)="logout()" class="btn-logout">
            <span class="icon">🚪</span>
            <span class="text">Sair</span>
          </button>
        </div>
      </nav>
      <main class="main-content">
        <router-outlet />
      </main>
    </div>
    <ng-template #loginView>
      <router-outlet />
    </ng-template>
  `,
  styles: [`
    .app-container {
      display: flex;
      min-height: 100vh;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    }

    .sidebar {
      width: 250px;
      background: rgba(255, 255, 255, 0.05);
      backdrop-filter: blur(10px);
      border-right: 1px solid rgba(255, 255, 255, 0.1);
      display: flex;
      flex-direction: column;
      position: fixed;
      height: 100vh;
    }

    .logo {
      padding: 1.5rem;
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    }

    .logo h2 {
      margin: 0;
      color: white;
      font-size: 1.5rem;
    }

    .nav-links {
      list-style: none;
      padding: 1rem 0;
      margin: 0;
      flex: 1;
    }

    .nav-links li a {
      display: flex;
      align-items: center;
      padding: 0.75rem 1.5rem;
      color: rgba(255, 255, 255, 0.7);
      text-decoration: none;
      transition: all 0.2s;
    }

    .nav-links li a:hover {
      background: rgba(255, 255, 255, 0.1);
      color: white;
    }

    .nav-links li a.active {
      background: rgba(79, 70, 229, 0.3);
      color: white;
      border-right: 3px solid #4f46e5;
    }

    .icon {
      margin-right: 0.75rem;
      font-size: 1.25rem;
    }

    .nav-footer {
      padding: 1rem;
      border-top: 1px solid rgba(255, 255, 255, 0.1);
    }

    .btn-logout {
      display: flex;
      align-items: center;
      width: 100%;
      padding: 0.75rem;
      background: transparent;
      border: none;
      color: rgba(255, 255, 255, 0.7);
      cursor: pointer;
      transition: all 0.2s;
      font-size: 1rem;
    }

    .btn-logout:hover {
      background: rgba(239, 68, 68, 0.2);
      color: #ef4444;
    }

    .main-content {
      flex: 1;
      margin-left: 250px;
      min-height: 100vh;
    }
  `]
})
export class AppComponent implements OnInit {
  title = 'Google Search Scraper';
  isAuthenticated = false;

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.authService.isAuthenticated$.subscribe(isAuth => {
      this.isAuthenticated = isAuth;
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
