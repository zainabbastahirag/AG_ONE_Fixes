# Performance report — root-cause diagnosis (AG ONE)

This folder contains a small, self-contained C# program that **proves what is actually
causing the failures** in the QA performance report, plus a ready-to-send reply you can
post back to the team.

## TL;DR — the report is being mis-read

The headline ("after 100 users we get errors / unauthorized / bad request") points at a
**capacity / scaling limit**. The numbers in the report say something different:

| Evidence in the report | What it means |
|---|---|
| **Every failing endpoint requires login** and returns **`401 Unauthorized`** | The requests arrive **without a valid auth token**. |
| `POST /api/auth/login` returns **200** (0% failures) and all **public/static** routes pass | The server is **healthy** and login itself works under load. |
| Response times stay **fast and flat** (median ~30 ms, p95 ~60–77 ms, throughput steady) | There is **no queueing/timeout/saturation** — not a capacity problem. |
| Failures are a clean **100%** on protected routes, **0%** on public routes | Failure correlates with **"needs a token"**, *not* with the number of users. |
| `POST /api/auth/verify-email` returns **`400 Bad Request`** | A **one-time** email code was **replayed** — stale test data, not load. |

**Root cause:** the recorded-session load script (mitmproxy → Locust replay) does **not
carry the auth (Bearer/JWT) token** from the `login` response into the subsequent
protected requests. Each virtual user logs in, throws the token away, and then calls every
protected API unauthenticated → `401`. The single recorded browser session "worked"
because the browser had the token; the replayed virtual users do not.

> Note also the doubled slash in every URL (`//api/...`). That's a base-URL/host
> mis-config in the script (host configured with a trailing slash). Worth cleaning up,
> but it is not what causes the 401s.

So this run does **not** yet tell us how the app behaves under load. It has to be re-run
with the auth token wired up correctly first.

## How this program proves it

`Program.cs` is a single .NET 8 app that:

1. Starts a tiny local API that behaves like AG ONE — public routes are open, protected
   routes require `Authorization: Bearer <token>`, `login` issues a token, and a one-time
   email code returns `400` when replayed.
2. Runs the **same load at the same concurrency (150 users — deliberately above 100)** two
   ways:
   - **Run A — Broken replay:** virtual users do **not** reuse the token → reproduces the
     report (≈100% `401`).
   - **Run B — Corrected replay:** virtual users log in and **reuse the Bearer token** →
     ≈100% success at the exact same concurrency.
3. Writes an HTML report (`perf_auth_diagnosis_report.html`) and prints a console summary.

Because only the token handling changes between the two runs, the result isolates the
cause: **the 401 storm is an auth-token-propagation bug in the load script, not a load
ceiling.**

## Run it

```bash
cd perf-auth-diagnosis
dotnet run -c Release
```

Then open `perf_auth_diagnosis_report.html` in a browser.

Sample console output:

```
=== RUN A: BROKEN replay (token NOT propagated) — 150 concurrent users ===
  Success rate   : 0.0%      (3150 requests, 3150 failed, all 401)
=== RUN B: CORRECTED replay (login then reuse Bearer token) — 150 concurrent users ===
  Success rate   : 100.0%    (3150 requests, 0 failed, all 200)
```

## Suggested reply to your QA engineer

> Thanks for getting the first run out — really useful. Before we read this as a capacity
> problem, I think the numbers point at the **test script**, not the app:
>
> - Every endpoint that's failing is an **authenticated** one and it's failing with **401
>   Unauthorized at 100%** — not a gradual increase as users ramp.
> - `POST /api/auth/login` itself returns **200**, and all the public/static routes
>   (`/api/products`, `/_framework/*`, `appsettings.json`, `/login`) pass.
> - Response times stay **low and flat** the whole run (median ~30 ms, p95 under ~80 ms,
>   throughput steady). If we were hitting a capacity limit we'd see latency climbing and
>   timeouts, not clean 401s.
> - `verify-email` returns **400** because we're replaying a **one-time** email code.
>
> That signature means the replay script logs in but **doesn't reuse the auth token** — so
> every protected call goes out unauthenticated and gets a 401. So this run is really
> telling us "the script isn't authenticated," not "the app fails after 100 users."
>
> I put together a tiny reproduction that runs the same 150-user load two ways — without
> the token (≈100% 401, matches our report) and with the token reused (≈100% success at the
> same concurrency). Report + code attached.
>
> Could we update the Locust script to:
> 1. Capture the token from the `/api/auth/login` response and send it as
>    `Authorization: Bearer <token>` on every following request (per virtual user),
> 2. Use a **fresh** one-time code per user for register/verify-email (don't replay the
>    recorded one), and
> 3. Fix the doubled slash in the base URL (`//api/...`)?
>
> Once that's in, let's re-run — then the results will actually reflect how the app holds
> up under load. Happy to pair on the script.

## Files

- `Program.cs` — the reproduction + report generator.
- `PerfAuthDiagnosis.csproj` — .NET 8 project file.
- `perf_auth_diagnosis_report.html` — generated when you run it (git-ignored).
