/** Mirrors the backend EnumRef: a value carried as both its numeric id and its name. */
export interface EnumRef {
  id: number;
  name: string;
}

/** A single group bucket from the stats endpoint. */
export interface EnumCount {
  value: EnumRef;
  count: number;
}

/** Aggregate counts returned by POST /api/requests/stats. */
export interface RequestStats {
  total: number;
  byStatus: EnumCount[];
  byType: EnumCount[];
}
