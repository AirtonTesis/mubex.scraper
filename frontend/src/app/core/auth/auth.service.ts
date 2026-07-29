import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  userId: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';
  private readonly USER_ID_KEY = 'user_id';
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());

  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  constructor(private http: HttpClient) {}

  login(credentials: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', credentials).pipe(
      tap(response => {
        this.setToken(response.token);
        this.setUserId(response.userId);
        this.isAuthenticatedSubject.next(true);
      })
    );
  }

  logout(): void {
    this.clearToken();
    this.clearUserId();
    this.isAuthenticatedSubject.next(false);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getUserId(): string | null {
    return localStorage.getItem(this.USER_ID_KEY);
  }

  isAuthenticated(): boolean {
    return this.hasToken();
  }

  private setToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  private setUserId(userId: string): void {
    localStorage.setItem(this.USER_ID_KEY, userId);
  }

  private clearToken(): void {
    localStorage.removeItem(this.TOKEN_KEY);
  }

  private clearUserId(): void {
    localStorage.removeItem(this.USER_ID_KEY);
  }

  private hasToken(): boolean {
    return !!this.getToken();
  }
}
