/**
 * HTTP client for the Service Requests API.
 * - Times out slow requests and supports cancellation (AbortController), so a fast typist's
 *   stale searches never overwrite newer results.
 * - Converts RFC 9457 Problem Details into ApiError with field errors, code and trace id.
 * - Carries ETags so updates use optimistic concurrency (If-Match).
 */

export class ApiError extends Error {
  /**
   * @param {number} status
   * @param {Record<string, any> | null} problem
   */
  constructor(status, problem) {
    super(problem?.detail ?? problem?.title ?? `The server responded with status ${status}.`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  get code() { return this.problem?.code ?? null; }

  get traceId() { return this.problem?.correlationId ?? this.problem?.traceId ?? null; }

  /** @returns {Record<string, string[]>} */
  get fieldErrors() { return this.problem?.errors ?? {}; }

  get isNetworkError() { return this.status === 0; }
}

export class ApiClient {
  /**
   * @param {string} baseUrl
   * @param {{ timeoutMs?: number }} [options]
   */
  constructor(baseUrl, { timeoutMs = 15000 } = {}) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.timeoutMs = timeoutMs;
  }

  /**
   * @returns {Promise<{ data: any, etag: string | null }>}
   */
  async request(path, { method = 'GET', body, headers = {}, signal } = {}) {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(new DOMException('Request timed out', 'TimeoutError')), this.timeoutMs);
    const abortFromCaller = () => controller.abort(signal.reason);
    signal?.addEventListener('abort', abortFromCaller, { once: true });

    let response;
    try {
      response = await fetch(`${this.baseUrl}${path}`, {
        method,
        signal: controller.signal,
        headers: {
          Accept: 'application/json',
          ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
          ...headers,
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
    } catch (error) {
      if (signal?.aborted) throw error;                 // caller cancelled: let it propagate as AbortError
      throw new ApiError(0, {
        title: 'Network error',
        detail: error?.name === 'TimeoutError'
          ? 'The server took too long to respond. Check your connection and try again.'
          : 'We could not reach the server. Check your connection and try again.',
      });
    } finally {
      window.clearTimeout(timeout);
      signal?.removeEventListener('abort', abortFromCaller);
    }

    const etag = response.headers.get('ETag');
    if (response.status === 204) return { data: null, etag };

    const isJson = (response.headers.get('Content-Type') ?? '').includes('json');
    const data = isJson ? await response.json() : null;
    if (!response.ok) throw new ApiError(response.status, data);
    return { data, etag };
  }

  async getMetadata() {
    return (await this.request('/metadata')).data;
  }

  async getSummary() {
    return (await this.request('/service-requests/summary')).data;
  }

  /** @param {{ q?: string, status?: string, sort?: string, page?: number, pageSize?: number }} params */
  async listRequests({ q, status, sort, page, pageSize } = {}, signal) {
    const query = new URLSearchParams();
    if (q) query.set('search', q);
    if (status) query.set('status', status);
    if (sort) query.set('sort', sort);
    if (page) query.set('page', String(page));
    if (pageSize) query.set('pageSize', String(pageSize));
    const suffix = query.size ? `?${query}` : '';
    return (await this.request(`/service-requests${suffix}`, { signal })).data;
  }

  getRequest(id) {
    return this.request(`/service-requests/${encodeURIComponent(id)}`);
  }

  async createRequest(payload) {
    return (await this.request('/service-requests', { method: 'POST', body: payload })).data;
  }

  changeStatus(id, status, etag) {
    return this.request(`/service-requests/${encodeURIComponent(id)}/status`, {
      method: 'PATCH',
      body: { status },
      headers: { 'If-Match': etag },
    });
  }

  async deleteRequest(id, etag) {
    await this.request(`/service-requests/${encodeURIComponent(id)}`, {
      method: 'DELETE',
      headers: { 'If-Match': etag },
    });
  }
}
