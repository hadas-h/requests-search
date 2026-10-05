import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { RequestStats } from '../models/request-stats.models';

const REQUESTS_STATS_PATH = '/api/requests/stats';

/**
 * HTTP client for the dashboard aggregate counts. One call powers the whole dashboard; the JWT
 * Bearer token is attached by the auth interceptor.
 */
@Injectable({ providedIn: 'root' })
export class RequestsStatsService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiBaseUrl}${REQUESTS_STATS_PATH}`;

  stats(): Observable<RequestStats> {
    return this.http.post<RequestStats>(this.endpoint, {});
  }
}
