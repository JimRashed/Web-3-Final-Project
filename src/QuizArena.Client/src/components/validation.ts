export type Validator = (value: string) => string | undefined;
export const required = (label: string): Validator => (value) => value.trim() ? undefined : `${label} is required.`;
export const maxLength = (length: number, label: string): Validator => (value) => value.trim().length <= length ? undefined : `${label} must be ${length} characters or fewer.`;
export function validate(value: string, validators: Validator[]): string | undefined {
  for (const validator of validators) { const error = validator(value); if (error) return error; }
  return undefined;
}
