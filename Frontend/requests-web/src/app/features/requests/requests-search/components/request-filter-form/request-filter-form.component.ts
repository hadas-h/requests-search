import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  output,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';
import { debounceTime } from 'rxjs/operators';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';

import { RequestQuery, RequestStatus, RequestType } from '../../models/request.models';

interface EnumOption<T> {
  value: T;
  labelKey: string;
}

const DATE_RANGE_INVALID = 'dateRangeInvalid';
const FILTER_DEBOUNCE_MS = 300;

/** Blocks submission when both dates are set and createdFrom is later than createdTo. */
const dateRangeValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const from = group.get('createdFrom')?.value as Date | null;
  const to = group.get('createdTo')?.value as Date | null;

  if (from && to && from.getTime() > to.getTime()) {
    return { [DATE_RANGE_INVALID]: true };
  }

  return null;
};

/**
 * Presentational filter form. Options come only from the typed enums and all labels are i18n keys.
 * Emits the assembled query via a signal output; holds no store or HTTP access.
 */
@Component({
  selector: 'app-filter-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    DatePickerModule,
    ButtonModule,
  ],
  templateUrl: './request-filter-form.component.html',
  styleUrl: './request-filter-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterFormComponent {
  readonly filterChange = output<RequestQuery>();

  protected readonly statusOptions: EnumOption<RequestStatus>[] = Object.values(RequestStatus).map(
    (value) => ({ value, labelKey: `Status.${value}` }),
  );

  protected readonly requestTypeOptions: EnumOption<RequestType>[] = Object.values(
    RequestType,
  ).map((value) => ({ value, labelKey: `RequestType.${value}` }));

  protected readonly form: FormGroup;
  protected readonly dateRangeInvalidKey = DATE_RANGE_INVALID;

  private readonly destroyRef = inject(DestroyRef);

  constructor(private readonly fb: FormBuilder) {
    this.form = this.fb.group(
      {
        requestNumber: this.fb.control<string | null>(null),
        statuses: this.fb.control<RequestStatus[]>([]),
        requestType: this.fb.control<RequestType | null>(null),
        createdFrom: this.fb.control<Date | null>(null),
        createdTo: this.fb.control<Date | null>(null),
      },
      { validators: dateRangeValidator },
    );

    // Debounce edits so a burst of changes issues a single search; the Search button still
    // emits immediately via submit(). Only valid states auto-emit (an inverted range never fires).
    this.form.valueChanges
      .pipe(debounceTime(FILTER_DEBOUNCE_MS), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.form.valid) {
          this.filterChange.emit(this.buildQuery());
        }
      });
  }

  /**
   * Reflects an externally supplied filter (a dashboard drill-down) in the controls. Called
   * imperatively by the parent so it runs once per drill and never fights the user's own edits
   * or Clear. Patches silently; the parent already triggers the matching search.
   */
  applyExternal(query: RequestQuery): void {
    this.form.patchValue(
      {
        requestNumber: query.requestNumber ?? null,
        statuses: query.statuses ?? [],
        requestType: query.requestType ?? null,
        createdFrom: query.createdFrom ? new Date(query.createdFrom) : null,
        createdTo: query.createdTo ? new Date(query.createdTo) : null,
      },
      { emitEvent: false },
    );
  }

  protected get hasDateRangeError(): boolean {
    return this.form.hasError(DATE_RANGE_INVALID);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.filterChange.emit(this.buildQuery());
  }

  /** Clears the form and emits an empty query so the parent reloads the unfiltered first page. */
  protected clear(): void {
    this.form.reset({
      requestNumber: null,
      statuses: [],
      requestType: null,
      createdFrom: null,
      createdTo: null,
    });
    this.filterChange.emit({});
  }

  private buildQuery(): RequestQuery {
    const { requestNumber, statuses, requestType, createdFrom, createdTo } = this.form.getRawValue();
    const query: RequestQuery = {};

    const trimmedNumber = requestNumber?.trim();
    if (trimmedNumber) {
      query.requestNumber = trimmedNumber;
    }

    if (statuses && statuses.length > 0) {
      query.statuses = [...statuses];
    }

    if (requestType) {
      query.requestType = requestType;
    }

    if (createdFrom) {
      query.createdFrom = createdFrom.toISOString();
    }

    if (createdTo) {
      query.createdTo = createdTo.toISOString();
    }

    return query;
  }
}
