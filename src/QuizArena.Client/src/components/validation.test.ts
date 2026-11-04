import { describe, expect, it } from 'vitest';
import { maxLength, required, validate } from './validation';

describe('field validation', () => {
  it.each(['', ' ', '\t\n'])('rejects blank required values', (value) => {
    expect(validate(value, [required('Name')])).toBe('Name is required.');
  });
  it('applies trimmed boundaries and returns the first failing rule', () => {
    const rules = [required('Name'), maxLength(5, 'Name')];
    expect(validate('  Alice  ', rules)).toBeUndefined();
    expect(validate('Longer', rules)).toBe('Name must be 5 characters or fewer.');
    expect(validate('', rules)).toBe('Name is required.');
  });
});
