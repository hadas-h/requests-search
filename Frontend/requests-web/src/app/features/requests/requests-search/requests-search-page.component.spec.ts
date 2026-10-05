import { Component, WritableSignal, computed, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { RequestDto, RequestStatus, RequestType } from './models/request.models';
import { RequestsStore, RequestsStatus } from './services/requests-search.store';
import { FilterFormComponent } from './components/request-filter-form/request-filter-form.component';
import { ResultsTableComponent } from './components/request-results-table/request-results-table.component';
import { SearchPageComponent } from './requests-search-page.component';

/**
 * A signal-backed stub of {@link RequestsStore} whose state can be driven per
 * test. It mirrors the public signal surface the SearchPage template reads
 * (status/items/hasMore/isEmpty) and exposes no-op action methods so the
 * container's constructor search does not hit HTTP.
 */
class RequestsStoreStub {
  readonly statusSig: WritableSignal<RequestsStatus> = signal<RequestsStatus>('idle');
  readonly itemsSig: WritableSignal<RequestDto[]> = signal<RequestDto[]>([]);
  readonly nextCursor: WritableSignal<string | null> = signal<string | null>(null);

  readonly status = this.statusSig.asReadonly();
  readonly items = this.itemsSig.asReadonly();
  readonly hasMore = computed(() => this.nextCursor() !== null);
  readonly isEmpty = computed(() => this.statusSig() === 'success' && this.itemsSig().length === 0);

  readonly search = jasmine.createSpy('search');
  readonly loadMore = jasmine.createSpy('loadMore');
  readonly retry = jasmine.createSpy('retry');
}

/**
 * Minimal stand-in for the filter form. It aliases the real {@link FilterFormComponent} token so
 * the container's `viewChild.required(FilterFormComponent)` resolves, and provides a no-op
 * `applyExternal` matching the real component's imperative drill-down API.
 */
@Component({
  selector: 'app-filter-form',
  standalone: true,
  template: '',
  providers: [{ provide: FilterFormComponent, useExisting: FilterFormStubComponent }],
})
class FilterFormStubComponent {
  readonly filterChange = output<unknown>();
  applyExternal(): void {}
}

/** Minimal stand-in for the results table, mirroring its signal inputs/outputs. */
@Component({ selector: 'app-results-table', standalone: true, template: '' })
class ResultsTableStubComponent {
  readonly items = input<unknown[]>([]);
  readonly hasMore = input<boolean>(false);
  readonly loading = input<boolean>(false);
  readonly sortBy = input<unknown>();
  readonly sortDirection = input<unknown>();
  readonly sortChange = output<unknown>();
  readonly loadMore = output<void>();
}

function makeRequest(id: number): RequestDto {
  return {
    id,
    requestNumber: `REQ-${String(id).padStart(6, '0')}`,
    customerId: id,
    ownerId: 1,
    assignedToUserId: null,
    status: { id: 1, name: RequestStatus.New },
    requestType: { id: 1, name: RequestType.General },
    createdAt: '2024-05-01T10:00:00Z',
  };
}

describe('SearchPageComponent', () => {
  let fixture: ComponentFixture<SearchPageComponent>;
  let store: RequestsStoreStub;

  beforeEach(async () => {
    store = new RequestsStoreStub();

    await TestBed.configureTestingModule({
      imports: [SearchPageComponent],
      providers: [{ provide: RequestsStore, useValue: store }, provideTranslateService()],
    })
      .overrideComponent(SearchPageComponent, {
        remove: { imports: [FilterFormComponent, ResultsTableComponent] },
        add: { imports: [FilterFormStubComponent, ResultsTableStubComponent] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(SearchPageComponent);
  });

  function html(): string {
    return fixture.nativeElement.textContent as string;
  }

  it('runs an initial search on construction', () => {
    fixture.detectChanges();
    expect(store.search).toHaveBeenCalledTimes(1);
  });

  it('shows the loading indicator while a request is in progress', () => {
    store.statusSig.set('loading');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.search-page__loading')).not.toBeNull();
    // The loading branch renders its message; other state branches are hidden.
    expect(html()).toContain('State.loading');
    expect(fixture.nativeElement.querySelector('.search-page__error')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-results-table')).toBeNull();
  });

  it('shows the error message and retry control when the request fails', () => {
    store.statusSig.set('error');
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('.search-page__error');
    expect(errorEl).not.toBeNull();
    expect(errorEl.getAttribute('role')).toBe('alert');
    expect(fixture.nativeElement.querySelector('p-button')).not.toBeNull();
    // The retry control is wired to the store.
    expect(html()).toContain('State.retry');
  });

  it('shows the empty-state message when a search succeeds with no items', () => {
    store.statusSig.set('success');
    store.itemsSig.set([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.search-page__empty')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-results-table')).toBeNull();
  });

  it('shows the results table when a search succeeds with items', () => {
    store.statusSig.set('success');
    store.itemsSig.set([makeRequest(1), makeRequest(2)]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-results-table')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.search-page__empty')).toBeNull();
  });
});
