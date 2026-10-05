import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { RequestStatus } from '../../models/request.models';

/** PrimeIcon per status, giving each state a recognizable glyph. */
const STATUS_ICON: Readonly<Record<RequestStatus, string>> = {
  [RequestStatus.New]: 'pi pi-sparkles',
  [RequestStatus.InProgress]: 'pi pi-spin pi-spinner',
  [RequestStatus.Completed]: 'pi pi-check-circle',
  [RequestStatus.Cancelled]: 'pi pi-times-circle',
};

/** Renders a status as a vibrant colored pill with an icon and a localized label. */
@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './request-status-badge.component.html',
  styleUrl: './request-status-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadgeComponent {
  readonly status = input.required<RequestStatus>();

  protected readonly labelKey = computed(() => `Status.${this.status()}`);
  protected readonly icon = computed(() => STATUS_ICON[this.status()]);
  // Lower-cased status name drives the CSS modifier class (e.g. status-badge--inprogress).
  protected readonly modifier = computed(() => this.status().toLowerCase());
}
