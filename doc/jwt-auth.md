# JWT Authentication Guide

The Smart Trip Planner API uses **JWT Bearer tokens** (HS256, symmetric key) to authenticate requests. Every endpoint under `/api/trips` requires a valid `Authorization: Bearer <token>` header.

> **MVP Note**: Token generation is external. The API only validates tokens; it does not issue them. For development, generate tokens manually using the instructions below. For production, integrate with an Identity Provider (Auth0, Azure AD B2C, Keycloak, etc.).

---

## Token Requirements

The API validates the following claims and parameters:

| Parameter | Dev Value | Description |
|---|---|---|
| **Algorithm** | `HS256` | HMAC with SHA-256 |
| **Secret** | `dev-secret-key-that-is-at-least-32-bytes-long-for-hs256` | Must match `Jwt:Secret` in User Secrets (dev) or environment variable (prod) |
| **Issuer (`iss`)** | `smart-trip-planner` | Must match `Jwt:Issuer` |
| **Audience (`aud`)** | `smart-trip-planner-api` | Must match `Jwt:Audience` |
| **Subject (`sub`)** | Any string (e.g. `user-42`) | Becomes the trip's `OwnerUserId` |
| **JWT ID (`jti`)** | Random GUID | Recommended for token uniqueness |

> **Important**: The secret must be **at least 32 bytes** long for HS256. Shorter secrets will cause validation errors.

---

## Generating a Token with jwt.io

1. Open [https://jwt.io](https://jwt.io) in your browser.

2. In the **PAYLOAD: DATA** section, paste the following JSON and adjust the values:

   ```json
   {
     "sub": "user-42",
     "jti": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
     "iss": "smart-trip-planner",
     "aud": "smart-trip-planner-api",
     "iat": 1750000000,
     "exp": 1750003600
   }
   ```

   - **`sub`**: This is your user identifier. It will be stored as `OwnerUserId` on every trip you create.
   - **`exp`**: Unix timestamp for expiration. Use [epochconverter.com](https://www.epochconverter.com/) to generate a future timestamp.

3. In the **VERIFY SIGNATURE** section:
   - Select **`HS256`**.
   - Paste the secret into the text box:
     ```
     dev-secret-key-that-is-at-least-32-bytes-long-for-hs256
     ```

4. **Check that the signature matches**. The right-hand box in the VERIFY SIGNATURE section must show a green **"Signature Verified"** badge. If it does not, the secret is wrong or the payload is malformed.

5. Copy the encoded token from the left box (starts with `eyJhbGciOiJIUzI1NiIs...`).

---

## Using the Token in API Requests

Add the token to every request via the `Authorization` header:

```bash
curl -X POST https://localhost:7080/api/trips \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -d '{ "cityCode": "madrid", ... }'
```

### Expected Status Codes

| Scenario | HTTP Status |
|---|---|
| Missing or malformed `Authorization` header | `401 Unauthorized` |
| Token expired or signature invalid | `401 Unauthorized` |
| Valid token, but `sub` does not match trip owner | `403 Forbidden` |
| Valid token, everything OK | `200` / `201` / `204` |

---

## Development Setup

In development, JWT settings are stored in [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) (not in `appsettings.json`).

View current secrets:
```bash
dotnet user-secrets list --project SmartTripPlanner.API
```

If the secret is missing, set it:
```bash
dotnet user-secrets set "Jwt:Secret" "dev-secret-key-that-is-at-least-32-bytes-long-for-hs256" --project SmartTripPlanner.API
dotnet user-secrets set "Jwt:Issuer" "smart-trip-planner" --project SmartTripPlanner.API
dotnet user-secrets set "Jwt:Audience" "smart-trip-planner-api" --project SmartTripPlanner.API
```

## Production Setup

In production, **never** hardcode the secret in `appsettings.json`. Use one of these approaches:

- **Environment variables**: `Jwt__Secret=your-production-secret`
- **Azure Key Vault** or AWS Secrets Manager
- **Kubernetes secrets**

Ensure the secret is **at least 32 bytes** and rotated periodically.

---

## Quick Check: Is My Token Valid?

If you get `401`, verify these three things in order:

1. **Is the signature valid?** Paste the token into jwt.io with the correct secret. You must see **"Signature Verified"**.
2. **Is the token expired?** Check the `exp` claim against the current Unix time.
3. **Do `iss` and `aud` match?** They must exactly match the values in `Jwt:Issuer` and `Jwt:Audience` from the server's configuration.
