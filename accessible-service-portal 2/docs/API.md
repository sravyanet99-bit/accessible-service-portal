# API reference

Base URL: `/api/v1`. All bodies are JSON (camelCase, enums as strings). Errors use [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457) with a stable `code`, plus `traceId` and `correlationId`.

Send `X-Correlation-ID` to tag a request (letters, digits, `-`, `_` and `.`, up to 64 characters); the value is echoed in the response and logs.

## List requests

`GET /service-requests`

| Query | Example | Notes |
|---|---|---|
| `status` | `Open` | `Open`, `InProgress`, `Resolved`, `Cancelled` |
| `search` | `signature` | Matches reference, subject or requester name (case-insensitive) |
| `sort` | `-priority` | `createdAt`, `reference`, `priority`, `status`; prefix `-` for descending. Default `-createdAt` |
| `page` | `2` | 1-based |
| `pageSize` | `10` | 1–50 |

```json
{
  "items": [
    {
      "id": 1012, "reference": "SR-001012",
      "subject": "Signing link shows an expired-session message",
      "category": "Document signing", "priority": "High", "status": "Open",
      "requesterName": "Jordan Lee", "createdAt": "2026-10-24T07:16:51Z", "version": 1
    }
  ],
  "page": 1, "pageSize": 10, "totalCount": 12, "totalPages": 2
}
```

## Get a request

`GET /service-requests/{id}` returns `200` with an `ETag: "v3"` header. Send `If-None-Match: "v3"` to receive `304 Not Modified` when nothing has changed.

The body includes `description`, the `requester`, the full `history`, and `allowedTransitions`, the statuses this request can move to next.

## Submit a request

`POST /service-requests`, rate limited per client (default 10 per minute; `429` includes `Retry-After`).

```json
{
  "subject": "Keyboard trap in signing modal",
  "category": "Accessibility barrier",
  "priority": "High",
  "description": "Focus cannot leave the signing modal using Tab or Escape in Edge.",
  "requesterName": "Alex Morgan",
  "requesterEmail": "alex.morgan@example.com",
  "requesterPhone": null
}
```

Returns `201 Created` with `Location` and `ETag` headers. A validation failure returns `400` with one message per field:

```json
{
  "status": 400,
  "title": "Some fields need your attention.",
  "code": "validation_failed",
  "errors": {
    "subject": ["Enter a subject."],
    "priority": ["Choose Low, Medium or High."]
  },
  "traceId": "00-…", "correlationId": "…"
}
```

## Change status

`PATCH /service-requests/{id}/status` with `If-Match: "v1"` (required)

```json
{ "status": "InProgress" }
```

| Response | When |
|---|---|
| `200` + new `ETag` | Changed |
| `400` | Unknown status value |
| `404` | No such request |
| `409` `service_request.invalid_status_transition` | Not allowed from the current status |
| `412` `service_request.concurrency_conflict` | Someone else changed the request; reload and retry |
| `428` `service_request.version_required` | `If-Match` header missing |

## Delete a request

`DELETE /service-requests/{id}` with `If-Match` (required). Returns `204`, `404`, `412` or `428`.

## Summary

`GET /service-requests/summary` returns `{ "total": 12, "byStatus": [{ "status": "Open", "label": "Open", "count": 4 }, …] }`.

## Metadata

`GET /metadata` returns the categories, priorities (with descriptions), status labels and input limits. Clients build their forms from this instead of hard-coding rules. Cacheable for 5 minutes.

## Health

`GET /health/live` reports whether the process is up. `GET /health/ready` also checks that storage can be read. Both return `200 Healthy` or `503 Unhealthy`.
