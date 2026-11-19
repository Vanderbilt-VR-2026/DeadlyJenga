# Live pose streaming checkpoint

Owner: MAIN, GameSession/Session. Base: `60b0c2b5dcf46665eeaf582985e3e38e25ca3fec`.

The six-frame reliable pose window filled during independent desktop peer tests.
Increasing it to 24 frames increased application acknowledgement backlog rather
than establishing smoother motion. The installed Fusion streaming path sends
one fragment per connection send; pose receipts also consume that path.

This checkpoint losslessly compresses bounded live pose payloads when smaller,
batches exact acknowledgement serials every 100 ms, and omits the recipient's
unused survivor echo. Own-hand feedback remains included. World snapshots,
authority, scene guards, the six-frame window and existing byte budgets remain.
CRC32 uses a table and preserves the tested envelope bytes. Both peers require
the `live-stream-gzip-exact-acks-v1` catalog marker; older builds are incompatible.

Validation: 113-source compile; 514 extracted-code Mono assertions, including
CRC32 known-vector and old/new complete encoded-byte parity; 13 source guards.
The sealed candidate patch SHA256 is
`b18fb2e650f00c456f1811bd035298ff12b468eb4d0200248c519bffa3e1021d`.
These checks do not establish native Unity transport performance or Quest behavior.

Pending: matched-build independent peers, full leave/rejoin/second-game flow,
actual serialized sizes, acknowledgement age, application cadence, frame cost
and allocations. Keep this checkpoint distinct from the interim Quest APK built
from the base commit. Do not claim a networking performance fix until those
runtime comparisons pass.
