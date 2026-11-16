import { Component } from '@angular/core';

/** Placeholder routed component for navigation targets in unit tests. */
@Component({ selector: 'app-test-stub', template: 'stub' })
export class StubPage {}

/** Sets an input's value the way a user typing would (fires `input`). */
export function typeInto(root: HTMLElement, selector: string, value: string): HTMLInputElement {
  const el = root.querySelector<HTMLInputElement>(selector);
  if (!el) {
    throw new Error(`No element matches "${selector}"`);
  }
  el.value = value;
  el.dispatchEvent(new Event('input', { bubbles: true }));
  return el;
}

/** Marks an input as touched (fires `blur`). */
export function blurElement(root: HTMLElement, selector: string): void {
  const el = root.querySelector<HTMLElement>(selector);
  if (!el) {
    throw new Error(`No element matches "${selector}"`);
  }
  el.dispatchEvent(new Event('blur', { bubbles: true }));
}

export function submitButton(root: HTMLElement): HTMLButtonElement {
  const el = root.querySelector<HTMLButtonElement>('button[type="submit"]');
  if (!el) {
    throw new Error('No submit button found');
  }
  return el;
}

export function textOf(root: HTMLElement): string {
  return (root.textContent ?? '').replace(/\s+/g, ' ').trim();
}
