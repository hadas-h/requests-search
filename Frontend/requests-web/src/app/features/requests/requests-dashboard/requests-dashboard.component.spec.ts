import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { provideTranslateService } from '@ngx-translate/core';

import { DrillFilter, RequestsDashboardComponent } from './requests-dashboard.component';
import { RequestStats } from './models/request-stats.models';
import { RequestsStatsService } from './services/requests-stats.service';

/**
 * Chart.js `onClick` signature as the component uses it: the second argument is the array of
 * clicked elements, each exposing the slice `index`.
 */
type ChartClick = (event: unknown, elements: Array<{ index: number }>) => void;

/**
 * Narrow view over the component's protected chart options so the test can invoke the native
 * Chart.js `onClick` exactly as PrimeNG/Chart.js would on a slice click — exercising the real
 * drill-down path (index -> bucket name -> emitted DrillFilter) rather than mocking it.
 */
interface DashboardInternals {
  statusOptions: { onClick: ChartClick };
  typeOptions: { onClick: ChartClick };
}

const STATS: RequestStats = {
  total: 60,
  byStatus: [
    { value: { id: 1, name: 'New' }, count: 25 },
    { value: { id: 2, name: 'InProgress' }, count: 15 },
    { value: { id: 3, name: 'Completed' }, count: 18 },
    { value: { id: 4, name: 'Cancelled' }, count: 2 },
  ],
  byType: [
    { value: { id: 1, name: 'General' }, count: 30 },
    { value: { id: 2, name: 'Legal' }, count: 20 },
    { value: { id: 3, name: 'Payment' }, count: 7 },
    { value: { id: 4, name: 'Appeal' }, count: 3 },
  ],
};

describe('RequestsDashboardComponent', () => {
  let fixture: ComponentFixture<RequestsDashboardComponent>;
  let component: RequestsDashboardComponent;
  let internals: DashboardInternals;
  let statsApi: jasmine.SpyObj<RequestsStatsService>;

  function setup(statsResult = of(STATS)): void {
    statsApi = jasmine.createSpyObj<RequestsStatsService>('RequestsStatsService', ['stats']);
    statsApi.stats.and.returnValue(statsResult);

    TestBed.configureTestingModule({
      imports: [RequestsDashboardComponent],
      providers: [
        { provide: RequestsStatsService, useValue: statsApi },
        provideTranslateService(),
      ],
    });

    fixture = TestBed.createComponent(RequestsDashboardComponent);
    component = fixture.componentInstance;
    internals = component as unknown as DashboardInternals;
    fixture.detectChanges(); // triggers ngOnInit -> stats() load
  }

  // ---------------------------------------------------------------------
  // KPIs derived from a single stats response
  // ---------------------------------------------------------------------

  it('derives KPIs from the single stats response without extra API calls', () => {
    setup();

    expect(statsApi.stats).toHaveBeenCalledTimes(1);
    expect(component['total']()).toBe(60);
    // Open = New + InProgress = 25 + 15 = 40.
    expect(component['openCount']()).toBe(40);
    // Closed = total - open = 60 - 40 = 20.
    expect(component['closedCount']()).toBe(20);
    // Completion = Completed / total = 18 / 60 = 30%.
    expect(component['completionPct']()).toBe(30);
  });

  // ---------------------------------------------------------------------
  // Drill-down: a slice click emits the matching filter (the significant logic)
  // ---------------------------------------------------------------------

  it('emits a status drill-down for the clicked status slice', () => {
    setup();
    const emitted: DrillFilter[] = [];
    component.drillStatus.subscribe((f) => emitted.push(f));

    // Click the 2nd status slice (index 1) -> "InProgress".
    internals.statusOptions.onClick(null, [{ index: 1 }]);

    expect(emitted).toEqual([{ statuses: ['InProgress'] }]);
  });

  it('emits a type drill-down for the clicked type slice', () => {
    setup();
    const emitted: DrillFilter[] = [];
    component.drillType.subscribe((f) => emitted.push(f));

    // Click the 3rd type slice (index 2) -> "Payment".
    internals.typeOptions.onClick(null, [{ index: 2 }]);

    expect(emitted).toEqual([{ requestType: 'Payment' }]);
  });

  it('does not emit when the click resolves to no slice', () => {
    setup();
    const emitted: DrillFilter[] = [];
    component.drillStatus.subscribe((f) => emitted.push(f));

    // An empty elements array (click on empty chart area) must not emit.
    internals.statusOptions.onClick(null, []);

    expect(emitted).toEqual([]);
  });

  it('does not emit when the clicked index is out of range', () => {
    setup();
    const emitted: DrillFilter[] = [];
    component.drillType.subscribe((f) => emitted.push(f));

    // Index beyond the available buckets must be ignored (no throw, no emit).
    internals.typeOptions.onClick(null, [{ index: 99 }]);

    expect(emitted).toEqual([]);
  });

  // ---------------------------------------------------------------------
  // Error state
  // ---------------------------------------------------------------------

  it('shows the error state when the stats request fails', () => {
    setup(throwError(() => new Error('network')));

    expect(component['failed']()).toBeTrue();
    expect(component['loading']()).toBeFalse();
    expect(fixture.nativeElement.querySelector('.dashboard__error')).not.toBeNull();
  });
});
