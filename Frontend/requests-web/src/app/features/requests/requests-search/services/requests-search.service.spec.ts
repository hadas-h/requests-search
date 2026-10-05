import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';

import { environment } from '../../../../../environments/environment';
import {
  PagedResult,
  RequestDto,
  RequestQuery,
  RequestStatus,
  RequestType,
  SortDirection,
  SortField,
} from '../models/request.models';
import { RequestsSearchService } from './requests-search.service';

const ENDPOINT = `${environment.apiBaseUrl}/api/requests/search`;

describe('RequestsApiClient', () => {
  let client: RequestsSearchService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RequestsSearchService, provideHttpClient(), provideHttpClientTesting()],
    });
    client = TestBed.inject(RequestsSearchService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('calls POST /api/requests/search and returns the paged result', () => {
    const body: PagedResult<RequestDto> = {
      items: [
        {
          id: 12,
          requestNumber: 'REQ-000012',
          customerId: 13,
          ownerId: 3,
          assignedToUserId: 4,
          status: { id: 1, name: RequestStatus.New },
          requestType: { id: 2, name: RequestType.Legal },
          createdAt: '2024-05-01T10:00:00Z',
        },
      ],
      nextCursor: 'abc',
    };

    let result: PagedResult<RequestDto> | undefined;
    client.search({}).subscribe((r) => (result = r));

    const req = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect(req.request.method).toBe('POST');
    req.flush(body);

    expect(result).toEqual(body);
  });

  it('omits fields that are not supplied', () => {
    client.search({}).subscribe();

    const req = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect(req.request.body).toEqual({});
    req.flush({ items: [], nextCursor: null });
  });

  it('maps all supplied query fields to the request body', () => {
    const query: RequestQuery = {
      requestNumber: 'REQ',
      statuses: [RequestStatus.New, RequestStatus.InProgress],
      requestType: RequestType.Payment,
      createdFrom: '2024-01-01T00:00:00Z',
      createdTo: '2024-12-31T23:59:59Z',
      sortBy: SortField.CreatedAt,
      sortDirection: SortDirection.Desc,
      pageSize: 25,
      cursor: 'cursor-token',
    };

    client.search(query).subscribe();

    const req = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      requestNumber: 'REQ',
      status: [RequestStatus.New, RequestStatus.InProgress],
      requestType: RequestType.Payment,
      createdFrom: '2024-01-01T00:00:00Z',
      createdTo: '2024-12-31T23:59:59Z',
      sortBy: SortField.CreatedAt,
      sortDirection: SortDirection.Desc,
      pageSize: 25,
      cursor: 'cursor-token',
    });
    req.flush({ items: [], nextCursor: null });
  });

  it('sends a status array for multiple statuses', () => {
    client.search({ statuses: [RequestStatus.Completed, RequestStatus.Cancelled] }).subscribe();

    const req: TestRequest = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect(req.request.body.status).toEqual([RequestStatus.Completed, RequestStatus.Cancelled]);
    req.flush({ items: [], nextCursor: null });
  });

  it('trims and omits blank request numbers', () => {
    client.search({ requestNumber: '   ' }).subscribe();

    const req = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect('requestNumber' in req.request.body).toBeFalse();
    req.flush({ items: [], nextCursor: null });
  });

  it('sends pageSize when it is zero', () => {
    client.search({ pageSize: 0 }).subscribe();

    const req = httpMock.expectOne((r) => r.url === ENDPOINT);
    expect(req.request.body.pageSize).toBe(0);
    req.flush({ items: [], nextCursor: null });
  });
});
