# Mostlylucid.Notify

Multi-channel notification library for .NET. v0.1 ships email + outbox. Multi-channel (Slack, Discord, SMS, webhook) lands in v0.2 behind the same `INotificationSender` interface.

AOT-safe: no runtime Razor, no reflection-based DI scanning. Templates are `RazorSlices` source-generated; channel providers are registered explicitly.

See the design doc in the stylobot-commercial repo: `docs/superpowers/specs/2026-05-22-notify-system-design.md`.
