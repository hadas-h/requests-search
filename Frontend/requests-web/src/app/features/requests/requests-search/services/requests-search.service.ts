import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { PagedResult, RequestDto, RequestQuery } from '../models/request.models';

/** Path for the Requests search endpoint. Base URL comes from environment config. */
const REQUESTS_SEARCH_PATH = '/api/requests/search';

/** Request body sent to the search endpoint. Mirrors the backend `RequestSearchRequest`. */
interface RequestSearchRequestBody {
  requestNumber?: string;
  status?: string[];
  createdFrom?: string;
  createdTo?: string;
  requestType?: string;
  sortBy?: string;
  sortDirection?: string;
  pageSize?: number;
  cursor?: string;
}

/**
 * HTTP client for the Requests Search API. Maps a RequestQuery to the JSON body of
 * POST /api/requests/search and returns the PagedResult. The JWT Bearer token is attached
 * centrally by the auth interceptor.
 */
@Injectable({ providedIn: 'root' })
export class RequestsSearchService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}${REQUESTS_SEARCH_PATH}`;

  /** Executes a search. Only supplied fields are sent; multiple statuses go as a `status` array. */
  search(query: RequestQuery): Observable<PagedResult<RequestDto>> {
    return this.http.post<PagedResult<RequestDto>>(this.endpoint, this.toBody(query));
  }

  /** Maps the client query model to the API's JSON request body, omitting empty fields. */
  private toBody(query: RequestQuery): RequestSearchRequestBody {
    const body: RequestSearchRequestBody = {};

    const requestNumber = query.requestNumber?.trim();
    if (requestNumber) {
      body.requestNumber = requestNumber;
    }

    if (query.statuses && query.statuses.length > 0) {
      body.status = [...query.statuses];
    }

    if (query.requestType) {
      body.requestType = query.requestType;
    }

    if (query.createdFrom) {
      body.createdFrom = query.createdFrom;
    }

    if (query.createdTo) {
      body.createdTo = query.createdTo;
    }

    if (query.sortBy) {
      body.sortBy = query.sortBy;
    }

    if (query.sortDirection) {
      body.sortDirection = query.sortDirection;
    }

    if (query.pageSize != null) {
      body.pageSize = query.pageSize;
    }

    if (query.cursor) {
      body.cursor = query.cursor;
    }

    return body;
  }
}
