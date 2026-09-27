# ADR-011: QuestPDF for proposals, under the Community licence

Status: Accepted

## Context

SPEC 17 needs a professional PDF proposal, and SPEC 2 asks that the licence terms be confirmed in
an ADR.

## Decision

QuestPDF, with `QuestPDF.Settings.License = LicenseType.Community` set at startup.

## Licence position

QuestPDF is dual-licensed. The Community licence is free for organisations with **annual gross
revenue below $1M USD**, which covers development and early customers.

**This must be revisited before revenue crosses that threshold**, at which point a Professional
or Enterprise licence is required. The declaration is a single line in `Program.cs`.

## Rejected

**iText.** AGPL, or a commercial licence from the start.

**Headless-browser HTML to PDF.** Adds a browser to the deployment and makes layout harder to
control precisely.

**A hand-rolled PDF writer.** Not a good use of the time.

## Consequences

- A fluent C# API with real layout control, generating a roughly 52 KB proposal in well under a
  second.
- The generator sits behind `IQuotePdfGenerator`, so replacing it means replacing one class.
- **Action:** review the licence tier before revenue reaches $1M.
