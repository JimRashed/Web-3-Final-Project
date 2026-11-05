import { useId, type InputHTMLAttributes } from 'react';

export function ValidatedField({ label, help, error, id, ...input }: InputHTMLAttributes<HTMLInputElement> & { label: string; help?: string; error?: string }) {
  const generated = useId();
  const fieldId = id ?? generated;
  const description = [help ? `${fieldId}-help` : '', error ? `${fieldId}-error` : ''].filter(Boolean).join(' ') || undefined;
  return <div className="field">
    <label htmlFor={fieldId}>{label}</label>
    <input {...input} id={fieldId} aria-invalid={!!error} aria-describedby={description} />
    {help && <p className="field-help" id={`${fieldId}-help`}>{help}</p>}
    {error && <p className="field-error" id={`${fieldId}-error`}>{error}</p>}
  </div>;
}
