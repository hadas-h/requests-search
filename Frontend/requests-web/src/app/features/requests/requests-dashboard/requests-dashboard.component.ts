import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { ProgressBarModule } from 'primeng/progressbar';
import { SkeletonModule } from 'primeng/skeleton';

import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import {
  DEFAULT_SLICE_COLOR,
  STATUS_COLORS,
  TYPE_COLORS,
} from '../../../core/theme/status-colors';
import { RequestsStatsService } from './services/requests-stats.service';
import { EnumCount, RequestStats } from './models/request-stats.models';

/** Drill-down filter emitted on a chart slice click; shapes match the search page's query fields. */
export interface DrillFilter {
  statuses?: string[];
  requestType?: string;
}

/** i18n sections for the two chart kinds; also selects which color map to apply. */
type ChartKind = 'Status' | 'RequestType';

const OPEN_STATUSES = ['New', 'InProgress'];
const COMPLETED_STATUS = 'Completed';

@Component({
  selector: 'app-requests-dashboard',
  standalone: true,
  imports: [TranslatePipe, CardModule, ChartModule, ProgressBarModule, SkeletonModule],
  templateUrl: './requests-dashboard.component.html',
  styleUrl: './requests-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestsDashboardComponent implements OnInit {
  private readonly statsApi = inject(RequestsStatsService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly drillStatus = output<DrillFilter>();
  readonly drillType = output<DrillFilter>();

  protected readonly stats = signal<RequestStats | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  // KPIs derived from the single stats response — no extra API calls.
  protected readonly total = computed(() => this.stats()?.total ?? 0);
  protected readonly openCount = computed(() => this.sumStatuses(OPEN_STATUSES));
  protected readonly closedCount = computed(() => this.total() - this.openCount());
  protected readonly completedCount = computed(() => this.sumStatuses([COMPLETED_STATUS]));
  protected readonly completionPct = computed(() =>
    this.total() === 0 ? 0 : Math.round((this.completedCount() / this.total()) * 100),
  );

  protected readonly statusChart = computed(() => this.toChart(this.stats()?.byStatus, 'Status'));
  protected readonly typeChart = computed(() => this.toChart(this.stats()?.byType, 'RequestType'));

  // Click handling via Chart.js's native onClick (reliable across PrimeNG versions). Each chart
  // reads the clicked slice index and emits the matching drill-down filter.
  protected readonly statusOptions = this.buildOptions((index) => this.emitStatus(index));
  protected readonly typeOptions = this.buildOptions((index) => this.emitType(index));

  ngOnInit(): void {
    this.statsApi
      .stats()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (stats) => {
          this.stats.set(stats);
          this.loading.set(false);
        },
        error: () => {
          this.failed.set(true);
          this.loading.set(false);
        },
      });
  }

  private emitStatus(index: number): void {
    const name = this.nameAt(this.stats()?.byStatus, index);
    if (name) {
      this.drillStatus.emit({ statuses: [name] });
    }
  }

  private emitType(index: number): void {
    const name = this.nameAt(this.stats()?.byType, index);
    if (name) {
      this.drillType.emit({ requestType: name });
    }
  }

  // Base doughnut options plus a native onClick that resolves the clicked slice index.
  private buildOptions(onSlice: (index: number) => void) {
    return {
      responsive: true,
      maintainAspectRatio: false,
      cutout: '60%',
      plugins: { legend: { position: 'bottom' } },
      onClick: (_event: unknown, elements: Array<{ index: number }>) => {
        if (elements && elements.length > 0) {
          onSlice(elements[0].index);
        }
      },
    };
  }

  private nameAt(buckets: EnumCount[] | undefined, index: number | undefined): string | null {
    if (buckets === undefined || index === undefined || index < 0 || index >= buckets.length) {
      return null;
    }
    return buckets[index].value.name;
  }

  private sumStatuses(names: string[]): number {
    const buckets = this.stats()?.byStatus ?? [];
    return buckets
      .filter((b) => names.includes(b.value.name))
      .reduce((sum, b) => sum + b.count, 0);
  }

  private toChart(buckets: EnumCount[] | undefined, kind: ChartKind) {
    const data = buckets ?? [];
    const colorFor = (name: string) => this.sliceColor(name, kind);
    return {
      labels: data.map((b) => this.translate.instant(`${kind}.${b.value.name}`)),
      datasets: [
        {
          data: data.map((b) => b.count),
          backgroundColor: data.map((b) => colorFor(b.value.name)),
          hoverBackgroundColor: data.map((b) => colorFor(b.value.name)),
          borderWidth: 0,
        },
      ],
    };
  }

  // Resolves a slice color from the shared color maps, falling back to a neutral gray.
  private sliceColor(name: string, kind: ChartKind): string {
    const map: Record<string, string> = kind === 'Status' ? STATUS_COLORS : TYPE_COLORS;
    return map[name] ?? DEFAULT_SLICE_COLOR;
  }
}
