import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-container">
      <div class="login-card glass-card">
        <h2>Entrar</h2>
        <form (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label for="email">E-mail</label>
            <input
              type="email"
              id="email"
              name="email"
              [(ngModel)]="email"
              required
              placeholder="seu@email.com"
            />
          </div>
          <div class="form-group">
            <label for="password">Senha</label>
            <input
              type="password"
              id="password"
              name="password"
              [(ngModel)]="password"
              required
              placeholder="Sua senha"
            />
          </div>
          <div class="error-message" *ngIf="errorMessage">
            {{ errorMessage }}
          </div>
          <button type="submit" class="btn-primary" [disabled]="isLoading">
            {{ isLoading ? 'Entrando...' : 'Entrar' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    }

    .login-card {
      padding: 2rem;
      width: 100%;
      max-width: 400px;
      border-radius: 16px;
      background: rgba(255, 255, 255, 0.1);
      backdrop-filter: blur(10px);
      border: 1px solid rgba(255, 255, 255, 0.2);
    }

    h2 {
      text-align: center;
      color: white;
      margin-bottom: 1.5rem;
    }

    .form-group {
      margin-bottom: 1rem;
    }

    label {
      display: block;
      color: rgba(255, 255, 255, 0.8);
      margin-bottom: 0.5rem;
      font-size: 0.875rem;
    }

    input {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.1);
      color: white;
      font-size: 1rem;
      box-sizing: border-box;
    }

    input::placeholder {
      color: rgba(255, 255, 255, 0.5);
    }

    input:focus {
      outline: none;
      border-color: #4f46e5;
    }

    .btn-primary {
      width: 100%;
      padding: 0.75rem;
      background: #4f46e5;
      color: white;
      border: none;
      border-radius: 8px;
      font-size: 1rem;
      font-weight: 500;
      cursor: pointer;
      transition: background 0.2s;
    }

    .btn-primary:hover:not(:disabled) {
      background: #4338ca;
    }

    .btn-primary:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .error-message {
      color: #f87171;
      font-size: 0.875rem;
      margin-bottom: 1rem;
      text-align: center;
    }
  `]
})
export class LoginComponent {
  email = '';
  password = '';
  errorMessage = '';
  isLoading = false;

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  onSubmit(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.errorMessage = 'Credenciais inválidas';
        this.isLoading = false;
      }
    });
  }
}
