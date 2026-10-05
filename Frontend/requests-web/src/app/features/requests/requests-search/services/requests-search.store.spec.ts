import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import {
  PagedResult,
  RequestDto,
  RequestQuery,
  RequestStatus,
  RequestType,
  SortDirection,
  SortField,
} from '../models/request.models';
import { RequestsSearchService } from './requests-search.service';
import { RequestsStore } from './requests-search.store';

/** Builds a lightweight RequestDto for tests, overriding only the fields that matter. */
function makeRequest(id: number, overrides: Partial<RequestDto> = {}): RequestDto {
  return {
    id,
    requestNumber: `REQ-${String(id).padStart(6, '0')}`,
    customerId: id,
    ownerId: 1,
    assignedToUserId: null,
    status: { id: 1, name: RequestStatus.New },
    requestType: { id: 1, name: RequestType.General },
    createdAt: '2024-05-01T10:00:00Z',
    ...overrides,
  };
}

/** Builds a PagedResult envelope from items and an optional next cursor. */
function pagedResult(items: RequestDto[], nextCursor: string | null): PagedResult<RequestDto> {
  return { items, nextCursor };
}

describe('RequestsStore', () => {
  let store: RequestsStore;
  let api: jasmine.SpyObj<RequestsSearchService>;

  const baseQuery: RequestQuery = {
    sortBy: SortField.CreatedAt,
    sortDirection: SortDirection.Desc,
    pageSize: 50,
  };

  beforeEach(() => {
    api = jasmine.createSpyObj<RequestsSearchService>('RequestsApiClient', ['search']);

    TestBed.configureTestingModule({
      providers: [RequestsStore, { provide: RequestsSearchService, useValue: api }],
    });

    store = TestBed.inject(RequestsStore);
  });

  it('starts in the idle state with no items', () => {
    expect(store.status()).toBe('idle');
    expect(store.items()).toEqual([]);
    expect(store.hasMore()).toBeFalse();
    expect(store.isEmpty()).toBeFalse();
  });

  it('transitions idle -> loading -> success and replaces items on search', () => {
    const items = [makeRequest(1), makeRequest(2)];
    // The API resolves synchronously via of(...), so we assert the terminal state.
    api.search.and.returnValue(of(pagedResult(items, null)));

    store.search(baseQuery);

    expect(api.search).toHaveBeenCalledTimes(1);
    expect(store.status()).toBe('success');
    expect(store.items()).toEqual(items);
  });

  it('transitions to error when the API fails', () => {
    api.search.and.returnValue(throwError(() => new Error('network')));

    store.search(baseQuery);

    expect(store.status()).toBe('error');
    expect(store.items()).toEqual([]);
  });

  it('reports isEmpty when a search succeeds with no items', () => {
    api.search.and.returnValue(of(pagedResult([], null)));

    store.search(baseQuery);

    expect(store.status()).toBe('success');
    expect(store.isEmpty()).toBeTrue();
  });

  it('reflects hasMore from the returned nextCursor', () => {
    api.search.and.returnValue(of(pagedResult([makeRequest(1)], 'cursor-1')));

    store.search(baseQuery);

    expect(store.hasMore()).toBeTrue();
  });

  it('has no more pages when nextCursor is null', () => {
    api.search.and.returnValue(of(pagedResult([makeRequest(1)], null)));

    store.search(baseQuery);

    expect(store.hasMore()).toBeFalse();
  });

  it('appends items on loadMore using the cursor and clears hasMore on the last page', () => {
    const firstPage = [makeRequest(1), makeRequest(2)];
    const secondPage = [makeRequest(3), makeRequest(4)];

    // First page returns a cursor; the follow-up page (with that cursor) returns null.
    api.search.and.callFake((query: RequestQuery) =>
      query.cursor === 'cursor-1'
        ? of(pagedResult(secondPage, null))
        : of(pagedResult(firstPage, 'cursor-1')),
    );

    store.search(baseQuery);
    expect(store.items()).toEqual(firstPage);
    expect(store.hasMore()).toBeTrue();

    store.loadMore();

    // Items were appended, not replaced.
    expect(store.items()).toEqual([...firstPage, ...secondPage]);
    expect(store.hasMore()).toBeFalse();

    // loadMore issued the second call carrying the cursor from the first page.
    expect(api.search).toHaveBeenCalledTimes(2);
    const secondCall = api.search.calls.argsFor(1)[0];
    expect(secondCall.cursor).toBe('cursor-1');
  });

  it('does not call the API on loadMore when there is no next cursor', () => {
    api.search.and.returnValue(of(pagedResult([makeRequest(1)], null)));

    store.search(baseQuery);
    api.search.calls.reset();

    store.loadMore();

    expect(api.search).not.toHaveBeenCalled();
  });

  it('re-runs the last query on retry with paging reset', () => {
    // Initial search fails.
    api.search.and.returnValue(throwError(() => new Error('network')));
    store.search(baseQuery);
    expect(store.status()).toBe('error');

    // Recover: retry re-issues the same query, now succeeding.
    const items = [makeRequest(1)];
    api.search.and.returnValue(of(pagedResult(items, null)));

    store.retry();

    expect(store.status()).toBe('success');
    expect(store.items()).toEqual(items);
    // The retried query carries the original criteria without a cursor.
    const retryCall = api.search.calls.mostRecent().args[0];
    expect(retryCall.sortBy).toBe(SortField.CreatedAt);
    expect(retryCall.cursor).toBeUndefined();
  });

  it('does nothing on retry when no query has been issued', () => {
    store.retry();

    expect(api.search).not.toHaveBeenCalled();
    expect(store.status()).toBe('idle');
  });
});
