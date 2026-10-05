import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { TabsModule } from 'primeng/tabs';

import {
  DrillFilter,
  RequestsDashboardComponent,
} from '../requests-dashboard/requests-dashboard.component';
import { SearchPageComponent } from '../requests-search/requests-search-page.component';

const SEARCH_TAB = 'search';
const OVERVIEW_TAB = 'overview';

/**
 * Hosts the search and dashboard tabs and lifts the drill-down state. When a dashboard chart slice
 * is clicked, the applied filter flows into the search page and the search tab is activated.
 */
@Component({
  selector: 'app-requests-home',
  standalone: true,
  imports: [TranslatePipe, TabsModule, SearchPageComponent, RequestsDashboardComponent],
  templateUrl: './requests-home.component.html',
  styleUrl: './requests-home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestsHomeComponent {
  protected readonly searchTab = SEARCH_TAB;
  protected readonly overviewTab = OVERVIEW_TAB;

  protected readonly activeTab = signal<string>(SEARCH_TAB);
  protected readonly drillFilter = signal<DrillFilter | null>(null);

  protected onDrill(filter: DrillFilter): void {
    this.drillFilter.set(filter);
    this.activeTab.set(SEARCH_TAB);
  }
}
