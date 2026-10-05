/**
 * TypeScript models mirroring the backend contract. Enum values equal the backend enum NAMES
 * (the API serializes enums as names), and field casing matches the camelCase JSON response.
 */

/** Request lifecycle status; values match the backend RequestStatus names. */
export enum RequestStatus {
  New = 'New',
  InProgress = 'InProgress',
  Completed = 'Completed',
  Cancelled = 'Cancelled',
}

/** Request category; values match the backend RequestType names. */
export enum RequestType {
  General = 'General',
  Legal = 'Legal',
  Payment = 'Payment',
  Appeal = 'Appeal',
}

/** The only sort fields the Search API supports. */
export enum SortField {
  CreatedAt = 'CreatedAt',
  RequestNumber = 'RequestNumber',
  Status = 'Status',
  RequestType = 'RequestType',
}

export enum SortDirection {
  Asc = 'Asc',
  Desc = 'Desc',
}

/** Reference value carried as both its numeric id and its name (for logic vs. display). */
export interface EnumRef<TName extends string = string> {
  id: number;
  name: TName;
}

/** A single Request as returned by the API (camelCase JSON). */
export interface RequestDto {
  id: number;
  requestNumber: string;
  customerId: number;
  ownerId: number;
  assignedToUserId: number | null;
  status: EnumRef<RequestStatus>;
  requestType: EnumRef<RequestType>;
  /** ISO 8601 date-time string (e.g. "2024-05-01T10:00:00Z"). */
  createdAt: string;
}

/**
 * Client-side query model. Every field is optional; supplied fields map to the
 * JSON body the RequestsApiClient sends to POST /api/requests/search.
 */
export interface RequestQuery {
  requestNumber?: string;
  statuses?: RequestStatus[];
  requestType?: RequestType;
  /** Inclusive lower bound, ISO 8601 date-time string. */
  createdFrom?: string;
  /** Inclusive upper bound, ISO 8601 date-time string. */
  createdTo?: string;
  sortBy?: SortField;
  sortDirection?: SortDirection;
  /** 1–200, default 50 on the server. */
  pageSize?: number;
  /** Opaque Base64url cursor for keyset pagination. */
  cursor?: string;
}

/** Paged response envelope; `nextCursor` is null when no further pages remain. */
export interface PagedResult<T> {
  items: T[];
  nextCursor: string | null;
}
