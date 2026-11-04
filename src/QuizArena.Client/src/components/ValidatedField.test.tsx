import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { ValidatedField } from './ValidatedField';

it('associates a unique label, help and error with an invalid field', () => {
  render(<><ValidatedField label="Display name" help="Choose a name." error="Name is required." /><ValidatedField label="Other name" /></>);
  const input = screen.getByRole('textbox', { name: 'Display name' });
  expect(input).toHaveAttribute('aria-invalid', 'true');
  expect(input).toHaveAccessibleDescription('Choose a name. Name is required.');
  expect(input.id).not.toBe(screen.getByRole('textbox', { name: 'Other name' }).id);
});

it('preserves caller input props and an explicit ID', () => {
  render(<ValidatedField id="name" label="Display name" defaultValue="Curious" required maxLength={80} />);
  expect(screen.getByLabelText('Display name')).toHaveValue('Curious');
  expect(screen.getByLabelText('Display name')).toBeRequired();
  expect(screen.getByLabelText('Display name')).toHaveAttribute('maxlength', '80');
});
