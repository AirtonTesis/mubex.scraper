import { Pipe, PipeTransform } from '@angular/core';
import { TranslationService } from './translation.service';

@Pipe({
  name: 'validationKey',
  standalone: true
})
export class ValidationKeyPipe implements PipeTransform {
  constructor(private translationService: TranslationService) {}

  transform(key: string): string {
    return this.translationService.translate(key);
  }
}
