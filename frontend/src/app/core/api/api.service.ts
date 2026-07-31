import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { timeout } from 'rxjs/operators';

const DEFAULT_TIMEOUT_MS = 10000;

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl = '/api';

  constructor(private http: HttpClient) {}

  get<T>(path: string, params?: any): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`, { params: this.buildParams(params) })
      .pipe(timeout(DEFAULT_TIMEOUT_MS));
  }

  post<T>(path: string, body: any): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body)
      .pipe(timeout(DEFAULT_TIMEOUT_MS));
  }

  put<T>(path: string, body: any): Observable<T> {
    return this.http.put<T>(`${this.baseUrl}${path}`, body)
      .pipe(timeout(DEFAULT_TIMEOUT_MS));
  }

  patch<T>(path: string, body?: any): Observable<T> {
    return this.http.patch<T>(`${this.baseUrl}${path}`, body)
      .pipe(timeout(DEFAULT_TIMEOUT_MS));
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`)
      .pipe(timeout(DEFAULT_TIMEOUT_MS));
  }

  private buildParams(params?: any): HttpParams {
    let httpParams = new HttpParams();
    if (params) {
      Object.keys(params).forEach(key => {
        if (params[key] !== null && params[key] !== undefined) {
          httpParams = httpParams.set(key, params[key].toString());
        }
      });
    }
    return httpParams;
  }
}
