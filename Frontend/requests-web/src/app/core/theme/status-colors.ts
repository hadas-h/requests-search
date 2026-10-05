import { RequestStatus, RequestType } from '../../features/requests/requests-search/models/request.models';

/**
 * Single source of truth for the brand colors used to represent each RequestStatus and
 * RequestType across the app (dashboard charts, and — mirrored as SCSS modifiers — the status
 * badge). Keeping them here avoids duplicating hex values across components.
 */
export const STATUS_COLORS: Readonly<Record<RequestStatus, string>> = {
  [RequestStatus.New]: '#1d4ed8', // blue
  [RequestStatus.InProgress]: '#b45309', // amber
  [RequestStatus.Completed]: '#15803d', // green
  [RequestStatus.Cancelled]: '#b91c1c', // red
};

export const TYPE_COLORS: Readonly<Record<RequestType, string>> = {
  [RequestType.General]: '#2563eb',
  [RequestType.Legal]: '#7c3aed',
  [RequestType.Payment]: '#0d9488',
  [RequestType.Appeal]: '#db2777',
};

/** Fallback color for any value not present in the maps above. */
export const DEFAULT_SLICE_COLOR = '#94a3b8';
