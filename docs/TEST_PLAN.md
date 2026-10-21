# API regression checklist

Use two valid accounts: **Alice** and **Bob**, plus a third account: **Mallory**.

| ID | Scenario | Expected result | Current evidence / actual result |
| --- | --- | --- | --- |
| AUTH-01 | Register with a valid username, email, and 8+ character password | `200 OK`; password is never returned. | Passed in isolated HTTP test. |
| AUTH-02 | Register with an existing username/email | `409 Conflict`. | Duplicate username and duplicate email passed in isolated HTTP tests. |
| AUTH-03 | Register or log in with invalid/missing input | `400 Bad Request` validation response. | Invalid registration and missing login password passed in isolated HTTP tests. |
| AUTH-04 | Call an authorized API without a JWT | `401 Unauthorized`. | Passed in isolated HTTP test. |
| FRIEND-01 | Alice sends Bob a request; Bob accepts it | Both see each other in the friend list. | Request, acceptance, and Alice's friend list passed in isolated HTTP test; WPF not checked. |
| FRIEND-02 | Mallory requests `GET /api/Messages/{aliceId}` | `403 Forbidden` unless Mallory is an accepted friend. | Non-friend forbidden behavior passed in isolated HTTP test. |
| CHAT-01 | Alice sends Bob a SignalR message | Message is saved; Bob receives it in real time when connected. | Passed in isolated HTTP/SignalR test; WPF not checked. |
| CHAT-02 | Alice opens Bob's conversation with more than 50 messages | The latest 50 messages are returned in chronological order. | Passed with 55 seeded messages in isolated HTTP test. |
| FILE-01 | Upload a 25 MB-or-smaller PNG/PDF/WAV | Upload succeeds and returns a `fileId`. | Small TXT upload/download passed; these media types still to test. |
| FILE-02 | Upload `.exe`, an unsupported extension, an empty file, or a file over 25 MB | `400 Bad Request`. | `.exe` and empty-file rejection passed; size boundary still to test. |
| FILE-03 | Mallory requests an attachment URL belonging to Alice and Bob | `404 Not Found`; file bytes are not returned. | Passed in isolated HTTP test. |
| PRES-01 | Sign in with the same account from two WPF windows, then close one | The account remains online until both connections close. | Presence-tracker unit test passed; WPF two-window behavior not checked. |

## Current automation and remaining checks

The xUnit suite sends HTTP requests through `WebApplicationFactory` and covers registration, login, JWT enforcement, friend-request authorization, friend-only history, rejected executable uploads, private file downloads, and the blocked public legacy URL. A SignalR integration test sends a real message between two test accounts and confirms both real-time delivery and saved history. Tests use an isolated EF Core InMemory database; they never insert test users into `ChatDB`.

Still to verify manually in WPF: two-window login/chat, image and voice playback, upload and download of a legacy attachment, and disconnect/reconnect behavior. Further automation should use a disposable SQL Server database for relational/migration checks and add WPF UI tests. The CI workflow is ready to run the current suite on pushes and pull requests; a passing local run does not prove that GitHub Actions has passed until the repository is pushed.
