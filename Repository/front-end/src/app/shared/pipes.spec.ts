import { describe, expect, it } from 'vitest';

import {
  MoneyPipe,
  PercentagePipe,
  QuantityPipe,
  ShortDatePipe,
  StatusClassPipe,
  StatusLabelPipe,
} from './pipes';

describe('MoneyPipe', () => {
  const pipe = new MoneyPipe();

  it('formats USD with two decimal places', () => {
    expect(pipe.transform(4065.46)).toBe('$4,065.46');
  });

  it('keeps trailing zeros so columns line up', () => {
    expect(pipe.transform(1200)).toBe('$1,200.00');
  });

  it('formats negative amounts', () => {
    expect(pipe.transform(-250.5)).toBe('-$250.50');
  });

  it('shows a dash rather than $0.00 when there is no value', () => {
    expect(pipe.transform(null)).toBe('—');
    expect(pipe.transform(undefined)).toBe('—');
  });

  it('respects the organization currency', () => {
    expect(pipe.transform(100, 'EUR')).toContain('100.00');
  });
});

describe('QuantityPipe', () => {
  const pipe = new QuantityPipe();

  it('appends the unit', () => {
    expect(pipe.transform(201.6, 'SF')).toBe('201.6 SF');
  });

  it('trims trailing zeros a contractor does not need', () => {
    expect(pipe.transform(21, 'BOX')).toBe('21 BOX');
  });

  it('keeps the precision the engine returned', () => {
    expect(pipe.transform(2.1221, 'BAG')).toBe('2.1221 BAG');
  });

  it('works without a unit', () => {
    expect(pipe.transform(12.5)).toBe('12.5');
  });

  it('shows a dash for a missing quantity', () => {
    expect(pipe.transform(null, 'SF')).toBe('—');
  });
});

describe('PercentagePipe', () => {
  const pipe = new PercentagePipe();

  it('formats a stored percentage as typed', () => {
    // 12 means 12%, matching how the server stores it.
    expect(pipe.transform(12)).toBe('12%');
  });

  it('keeps fractional rates', () => {
    expect(pipe.transform(8.25)).toBe('8.25%');
  });

  it('shows a dash when absent', () => {
    expect(pipe.transform(null)).toBe('—');
  });
});

describe('StatusLabelPipe', () => {
  const pipe = new StatusLabelPipe();

  it('splits PascalCase into words', () => {
    expect(pipe.transform('EstimateReady')).toBe('Estimate Ready');
    expect(pipe.transform('ChangesRequested')).toBe('Changes Requested');
  });

  it('leaves single words alone', () => {
    expect(pipe.transform('Draft')).toBe('Draft');
  });

  it('shows a dash for nothing', () => {
    expect(pipe.transform(null)).toBe('—');
  });
});

describe('StatusClassPipe', () => {
  const pipe = new StatusClassPipe();

  it('marks accepted and finalized as success', () => {
    expect(pipe.transform('Accepted')).toBe('te-status--success');
    expect(pipe.transform('Finalized')).toBe('te-status--success');
  });

  it('marks rejected and cancelled as danger', () => {
    expect(pipe.transform('Rejected')).toBe('te-status--danger');
    expect(pipe.transform('Cancelled')).toBe('te-status--danger');
  });

  it('falls back to neutral for anything unrecognised', () => {
    expect(pipe.transform('SomethingNew')).toBe('te-status--neutral');
  });
});

describe('ShortDatePipe', () => {
  const pipe = new ShortDatePipe();

  it('formats an ISO date', () => {
    expect(pipe.transform('2026-03-14T12:00:00Z')).toMatch(/Mar 1[45], 2026/);
  });

  it('shows a dash for nothing', () => {
    expect(pipe.transform(null)).toBe('—');
  });

  it('shows a dash rather than "Invalid Date"', () => {
    expect(pipe.transform('not-a-date')).toBe('—');
  });
});
