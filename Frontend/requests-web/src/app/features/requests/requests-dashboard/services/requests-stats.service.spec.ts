import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

import { environment } from '../../../../../environments/environment';
import { RequestStats } from '../models/request-stats.models';
import { RequestsStatsService } from './requests-stats.service';

const ENDPOINT = `${environment.apiBaseUrl}/api/requests/stats`;

describe('RequestsStatsService', () => {
  let service: RequestsStatsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RequestsStatsService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RequestsStatsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('calls POST /api/requests/stats with an empty body and returns the stats', () => {
    const body: RequestStats = {
      total: 60,
      byStatus: [
        { value: { id: 1, name: 'New' }, count: 40 },
        { value: { id: 2, name: 'InProgress' }, count: 20 },
      ],
      byType: [{ value: { id: 1, name: 'General' }, count: 60 }],
    };

    let result: RequestStats | undefined;
    service.stats().subscribe((r) => (result = r));

    const req = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(body);

    expect(result).toEqual(body);
  });
});
