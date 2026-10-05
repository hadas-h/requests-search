import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';

import { RequestDto, SortDirection, SortField } from '../../models/request.models';
import { StatusBadgeComponent } from '../request-status-badge/request-status-badge.component';

export interface SortChange {
  sortBy: SortField;
  sortDirection: SortDirection;
}

interface ColumnDef {
  field: SortField;
  labelKey: string;
}

/**
 * Presentational results table: sortable columns, colored status badges, and a load-more control.
 * Communicates only through signal inputs/outputs (OnPush); it never touches the store or HTTP.
 */
@Component({
  selector: 'app-results-table',
  standalone: true,
  imports: [DatePipe, NgClass, TranslatePipe, TableModule, ButtonModule, StatusBadgeComponent],
  templateUrl: './request-results-table.component.html',
  styleUrl: './request-results-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultsTableComponent {
  readonly items = input<RequestDto[]>([]);
  readonly hasMore = input<boolean>(false);
  readonly loading = input<boolean>(false);
  readonly sortBy = input<SortField>(SortField.CreatedAt);
  readonly sortDirection = input<SortDirection>(SortDirection.Desc);

  readonly sortChange = output<SortChange>();
  readonly loadMore = output<void>();

  protected readonly SortDirection = SortDirection;

  // Limited to the sort fields the API supports; order defines display order.
  protected readonly columns: readonly ColumnDef[] = [
    { field: SortField.RequestNumber, labelKey: 'Columns.requestNumber' },
    { field: SortField.Status, labelKey: 'Columns.status' },
    { field: SortField.RequestType, labelKey: 'Columns.requestType' },
    { field: SortField.CreatedAt, labelKey: 'Columns.createdAt' },
  ];

  protected readonly activeSort = computed<SortChange>(() => ({
    sortBy: this.sortBy(),
    sortDirection: this.sortDirection(),
  }));

  // Re-activating the active column toggles direction; a different column starts ascending.
  protected onSort(field: SortField): void {
    const active = this.activeSort();
    const sortDirection =
      active.sortBy === field && active.sortDirection === SortDirection.Asc
        ? SortDirection.Desc
        : SortDirection.Asc;

    this.sortChange.emit({ sortBy: field, sortDirection });
  }

  protected ariaSort(field: SortField): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== field) {
      return 'none';
    }

    return this.sortDirection() === SortDirection.Asc ? 'ascending' : 'descending';
  }

  protected sortIcon(field: SortField): string {
    if (this.sortBy() !== field) {
      return 'pi pi-sort';
    }

    return this.sortDirection() === SortDirection.Asc ? 'pi pi-sort-up' : 'pi pi-sort-down';
  }

  protected onLoadMore(): void {
    this.loadMore.emit();
  }

  protected trackById(_index: number, item: RequestDto): number {
    return item.id;
  }
}
