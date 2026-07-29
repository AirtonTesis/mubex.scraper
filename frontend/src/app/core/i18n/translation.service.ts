import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { tap, catchError } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  private translations: Record<string, string> = {};
  private currentLocale = 'pt-BR';

  constructor(private http: HttpClient) {
    this.loadTranslations(this.currentLocale);
  }

  loadTranslations(locale: string): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`/assets/i18n/${locale}.json`).pipe(
      tap(data => {
        this.translations = data;
        this.currentLocale = locale;
      }),
      catchError(error => {
        console.error(`Failed to load translations for locale: ${locale}`, error);
        return of({});
      })
    );
  }

  translate(key: string): string {
    return this.translations[key] || key;
  }

  instant(key: string): string {
    return this.translate(key);
  }

  getCurrentLocale(): string {
    return this.currentLocale;
  }
}
