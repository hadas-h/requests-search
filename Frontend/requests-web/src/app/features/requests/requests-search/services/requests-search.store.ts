import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { PagedResult, RequestDto, RequestQuery } from '../models/request.models';
import { RequestsSearchService } from './requests-search.service';

export type RequestsStatus = 'idle' | 'loading' | 'success' | 'error';

/**
 * Signal-based state store for the Requests search feature. Owns the result list, request status,
 * and keyset cursor, exposing read-only signals plus derived `hasMore`/`isEmpty`. Retains the
 * last query (without its cursor) so `loadMore` can fetch the next page and `retry` can re-run it.
 */
@Injectable({ providedIn: 'root' })
export class RequestsStore {
  private readonly api = inject(RequestsSearchService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _items = signal<RequestDto[]>([]);
  private readonly _status = signal<RequestsStatus>('idle');
  private readonly _nextCursor = signal<string | null>(null);

  private lastQuery: RequestQuery | null = null;

  readonly items = this._items.asReadonly();
  readonly status = this._status.asReadonly();

  readonly hasMore = computed(() => this._nextCursor() !== null);
  readonly isEmpty = computed(() => this._status() === 'success' && this._items().length === 0);

  /** Runs a new search, replacing any existing results. */
  search(query: RequestQuery): void {
    this.lastQuery = { ...query, cursor: undefined };
    this._status.set('loading');

    this.api
      .search(this.lastQuery)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => this.applyPage(result, false),
        error: () => this._status.set('error'),
      });
  }

  /** Appends the next page using the current cursor. No-op when there is nothing more to load. */
  loadMore(): void {
    const cursor = this._nextCursor();
    if (cursor === null || this.lastQuery === null || this._status() === 'loading') {
      return;
    }

    this._status.set('loading');

    this.api
      .search({ ...this.lastQuery, cursor })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => this.applyPage(result, true),
        error: () => this._status.set('error'),
      });
  }

  /** Re-runs the last query from the first page (wired to the error state's retry control). */
  retry(): void {
    if (this.lastQuery === null) {
      return;
    }

    this.search(this.lastQuery);
  }

  private applyPage(result: PagedResult<RequestDto>, append: boolean): void {
    this._items.update((current) => (append ? [...current, ...result.items] : result.items));
    this._nextCursor.set(result.nextCursor);
    this._status.set('success');
  }
}
