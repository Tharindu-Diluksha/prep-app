import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Like Validators.required, but also rejects whitespace-only strings. */
export const notBlank: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  typeof control.value === 'string' && control.value.trim().length === 0 ? { blank: true } : null;

// Mirrors the API rules in prep-api/Services/AuthValidator.cs.
export const NAME_MAX_LENGTH = 100;
export const EMAIL_MAX_LENGTH = 254;
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;
