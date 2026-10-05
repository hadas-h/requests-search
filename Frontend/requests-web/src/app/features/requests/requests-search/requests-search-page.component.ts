import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SkeletonModule } from 'primeng/skeleton';

import { FilterFormComponent } from './components/request-filter-form/request-filter-form.component';
import {
  ResultsTableComponent,
  SortChange,
} from './components/request-results-table/request-results-table.component';
import {
  RequestQuery,
  RequestStatus,
  RequestType,
  SortDirection,
  SortField,
} from './models/request.models';
import { RequestsStore } from './services/requests-search.store';

/** Drill-down filter pushed by the dashboard: enum string names map to the query fields. */
export interface SearchDrillFilter {
  statuses?: string[];
  requestType?: string;
}

const DEFAULT_PAGE_SIZE = 50;

/**
 * Smart container coordinating the search screen. Owns the current filter and sort, translates
 * child events into store operations, and renders loading / error / empty / results states from
 * the store's status. Child components stay presentational.
 */
@Component({
  selector: 'app-search-page',
  standalone: true,
  imports: [
    TranslatePipe,
    ButtonModule,
    ProgressSpinnerModule,
    SkeletonModule,
    FilterFormComponent,
    ResultsTableComponent,
  ],
  templateUrl: './requests-search-page.component.html',
  styleUrl: './requests-search-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchPageComponent {
  /** Filter pushed from the dashboard drill-down; applied via an effect. */
  readonly drillFilter = input<SearchDrillFilter | null>(null);

  protected readonly store = inject(RequestsStore);

  private readonly filterForm = viewChild.required(FilterFormComponent);

  private readonly filter = signal<RequestQuery>({});

  protected readonly sortBy = signal<SortField>(SortField.CreatedAt);
  protected readonly sortDirection = signal<SortDirection>(SortDirection.Desc);

  protected readonly isLoading = computed(() => this.store.status() === 'loading');
  protected readonly hasResults = computed(() => this.store.items().length > 0);

  /** Placeholder rows rendered as skeletons during the initial load. */
  protected readonly skeletonRows = Array.from({ length: 6 }, (_, i) => i);

  constructor() {
    // Load the unfiltered first page on open, using the default CreatedAt/Desc order.
    this.runSearch();

    // React only to a new drill-down value. Everything else runs untracked so the effect does not
    // re-fire on unrelated signals (e.g. the viewChild) and re-apply a stale filter over the
    // user's own changes or Clear.
    effect(() => {
      const drill = this.drillFilter();
      if (!drill) {
        return;
      }

      untracked(() => {
        const query: RequestQuery = {
          statuses: drill.statuses as RequestStatus[] | undefined,
          requestType: drill.requestType as RequestType | undefined,
        };
        this.filter.set(query);
        // Reflect the drill-down in the form once; afterwards the form owns its own state, so the
        // user can freely change or clear it.
        this.filterForm().applyExternal(query);
        this.runSearch();
      });
    });
  }

  protected onFilterChange(query: RequestQuery): void {
    this.filter.set(query);
    this.runSearch();
  }

  protected onSortChange(change: SortChange): void {
    this.sortBy.set(change.sortBy);
    this.sortDirection.set(change.sortDirection);
    this.runSearch();
  }

  protected onLoadMore(): void {
    this.store.loadMore();
  }

  // Composes filter + current sort + default page size and dispatches a fresh search (resets paging).
  private runSearch(): void {
    this.store.search({
      ...this.filter(),
      sortBy: this.sortBy(),
      sortDirection: this.sortDirection(),
      pageSize: DEFAULT_PAGE_SIZE,
    });
  }
}
