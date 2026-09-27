import { Pipe, type PipeTransform } from '@angular/core';

/**
 * Display-only formatting pipes.
 *
 * These format numbers the server has already calculated and rounded. Nothing here does
 * arithmetic that affects a price: there is exactly one calculation engine, and it is on the
 * server (SPEC 14).
 */

@Pipe({ name: 'teMoney' })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, currency = 'USD'): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }

    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }
}

/** Formats a quantity with its unit, trimming trailing zeros: `201.6 SF`, `21 BOX`. */
@Pipe({ name: 'teQuantity' })
export class QuantityPipe implements PipeTransform {
  transform(value: number | null | undefined, unit?: string | null, maxDecimals = 4): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }

    const formatted = new Intl.NumberFormat('en-US', {
      minimumFractionDigits: 0,
      maximumFractionDigits: maxDecimals,
    }).format(value);

    return unit ? `${formatted} ${unit}` : formatted;
  }
}

/** Formats a percentage the way it is stored: 12 becomes `12%`. */
@Pipe({ name: 'tePercent' })
export class PercentagePipe implements PipeTransform {
  transform(value: number | null | undefined, maxDecimals = 2): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }

    const formatted = new Intl.NumberFormat('en-US', {
      minimumFractionDigits: 0,
      maximumFractionDigits: maxDecimals,
    }).format(value);

    return `${formatted}%`;
  }
}

/** Splits a PascalCase status into words: `EstimateReady` becomes `Estimate Ready`. */
@Pipe({ name: 'teStatusLabel' })
export class StatusLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '—';
    return value.replace(/([a-z])([A-Z])/g, '$1 $2');
  }
}

/** A CSS modifier for a status chip, e.g. `te-status--accepted`. */
@Pipe({ name: 'teStatusClass' })
export class StatusClassPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return 'te-status--neutral';

    switch (value) {
      case 'Accepted':
      case 'Completed':
      case 'Finalized':
        return 'te-status--success';
      case 'Rejected':
      case 'Cancelled':
        return 'te-status--danger';
      case 'Expired':
      case 'Superseded':
        return 'te-status--muted';
      case 'Sent':
      case 'Viewed':
      case 'Quoted':
      case 'InReview':
        return 'te-status--info';
      case 'Draft':
      case 'Estimating':
        return 'te-status--warn';
      default:
        return 'te-status--neutral';
    }
  }
}

/** Short, readable date. Times are stored in UTC and shown in the viewer's local zone. */
@Pipe({ name: 'teDate' })
export class ShortDatePipe implements PipeTransform {
  transform(value: string | Date | null | undefined, withTime = false): string {
    if (!value) return '—';

    const date = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(date.getTime())) return '—';

    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      ...(withTime ? { hour: 'numeric', minute: '2-digit' } : {}),
    }).format(date);
  }
}
