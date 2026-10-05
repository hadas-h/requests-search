import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideTranslateService } from '@ngx-translate/core';

import { RequestQuery, RequestStatus, RequestType } from '../../models/request.models';
import { FilterFormComponent } from './request-filter-form.component';

/**
 * Internal shape of the component under test. The form and the submit/clear
 * handlers are `protected`, so tests reach them through this narrow view rather
 * than driving the full PrimeNG template — keeping the tests focused on the
 * cross-field validation and emission logic (R11.5).
 */
interface FilterFormInternals {
  form: FormGroup;
  submit(): void;
  clear(): void;
}

describe('FilterFormComponent', () => {
  let fixture: ComponentFixture<FilterFormComponent>;
  let component: FilterFormComponent;
  let internals: FilterFormInternals;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FilterFormComponent],
      providers: [provideTranslateService()],
    }).compileComponents();

    fixture = TestBed.createComponent(FilterFormComponent);
    component = fixture.componentInstance;
    internals = component as unknown as FilterFormInternals;
    fixture.detectChanges();
  });

  it('marks the form invalid when createdFrom is later than createdTo', () => {
    internals.form.patchValue({
      createdFrom: new Date('2024-06-01T00:00:00Z'),
      createdTo: new Date('2024-05-01T00:00:00Z'),
    });

    expect(internals.form.invalid).toBeTrue();
    expect(internals.form.hasError('dateRangeInvalid')).toBeTrue();
  });

  it('does not emit filterChange when submitting an inverted date range', () => {
    const emitted: RequestQuery[] = [];
    component.filterChange.subscribe((query) => emitted.push(query));

    internals.form.patchValue({
      createdFrom: new Date('2024-06-01T00:00:00Z'),
      createdTo: new Date('2024-05-01T00:00:00Z'),
    });

    internals.submit();

    expect(emitted).toEqual([]);
  });

  it('treats a valid range as valid and emits the assembled query on submit', () => {
    const emitted: RequestQuery[] = [];
    component.filterChange.subscribe((query) => emitted.push(query));

    internals.form.patchValue({
      requestNumber: '  REQ-01  ',
      statuses: [RequestStatus.New, RequestStatus.InProgress],
      requestType: RequestType.Legal,
      createdFrom: new Date('2024-05-01T00:00:00Z'),
      createdTo: new Date('2024-06-01T00:00:00Z'),
    });

    expect(internals.form.valid).toBeTrue();

    internals.submit();

    expect(emitted.length).toBe(1);
    const query = emitted[0];
    expect(query.requestNumber).toBe('REQ-01');
    expect(query.statuses).toEqual([RequestStatus.New, RequestStatus.InProgress]);
    expect(query.requestType).toBe(RequestType.Legal);
    expect(query.createdFrom).toBe(new Date('2024-05-01T00:00:00Z').toISOString());
    expect(query.createdTo).toBe(new Date('2024-06-01T00:00:00Z').toISOString());
  });

  it('treats a single-sided date range as valid and emits on submit', () => {
    const emitted: RequestQuery[] = [];
    component.filterChange.subscribe((query) => emitted.push(query));

    internals.form.patchValue({ createdFrom: new Date('2024-05-01T00:00:00Z') });

    expect(internals.form.valid).toBeTrue();

    internals.submit();

    expect(emitted.length).toBe(1);
    expect(emitted[0].createdFrom).toBe(new Date('2024-05-01T00:00:00Z').toISOString());
    expect(emitted[0].createdTo).toBeUndefined();
  });

  it('emits an empty query when cleared', () => {
    const emitted: RequestQuery[] = [];
    component.filterChange.subscribe((query) => emitted.push(query));

    internals.form.patchValue({ requestNumber: 'REQ-99' });
    internals.clear();

    expect(emitted).toEqual([{}]);
  });
});
