import { Injectable } from '@angular/core';
import { FormControl, FormGroupDirective, NgForm } from '@angular/forms';
import { ErrorStateMatcher } from '@angular/material/core';

/**
 * Surfaces mat-form-field errors only after the user has attempted to
 * submit the form. Defers validation visibility entirely until intent
 * to submit is expressed: typing is uninterrupted, errors appear together
 * on the first submit click, and after that they stay live so the user
 * sees corrections update as they type.
 *
 * See "Form-field errors still surfaced during typing" in
 * docs/genai/issues.md.
 */
@Injectable({ providedIn: 'root' })
export class SubmittedErrorStateMatcher implements ErrorStateMatcher {
  isErrorState(
    control: FormControl | null,
    form: FormGroupDirective | NgForm | null,
  ): boolean {
    return !!(control && control.invalid && form && form.submitted);
  }
}
