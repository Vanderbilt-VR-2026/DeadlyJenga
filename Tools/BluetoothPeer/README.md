# One-headset Bluetooth testing

This Windows .NET 9 tool uses the PC's physical BLE adapter. It is not a Quest emulator and does not host or simulate Photon gameplay. Do not run it during FPS benchmarks.

Build: `dotnet build -c Release`. Generated `bin/` and `obj/` are not source.

- `dotnet run -- --probe`: report adapter role support.
- `dotnet run -- --probe-host`: briefly register and advertise the real GATT service, then stop.
- `dotnet run -- --scan-only`: list only Deadly Jenga Bluetooth rooms. The metadata contains no join code.
- `dotnet run -- --host-name "Evan" --name "PC test guest"`: request the selected headset room, then approve/deny in its Room panel. Verify no code arrives before approval; repeat denial, expiry, Bluetooth-off and headset-sleep cases.
- `dotnet run -- --host --protocol "<protocol from scanned metadata>" --code "US-ABC234" --decision approve`: advertise a controlled PC host for 120 seconds. Use the address of a real running Photon room. The headset should discover this host, request approval and join that real room. Use `deny` or `wait` to exercise the other paths. Default is deny. This helper auto-decides only because it is a test fixture.

Run the Quest and PC on different internet connections for the no-shared-Wi-Fi test. First inspect the APK permissions and verify Bluetooth permissions on-device. A successful PC/Quest exchange covers one real Quest radio path; repeat with two Quests before claiming Quest-to-Quest support. No Meta discovery/account API is used by this helper.
