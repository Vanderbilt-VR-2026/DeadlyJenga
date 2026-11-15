# Bluetooth diagnostic APK

This separate Android app embeds the game's **same NearbyBluetooth.java source**, with a simple flat test panel. It does not include Unity, the game's VR UI, or Photon joining. Its separate package `com.deadlyjenga.bluetoothprobe` leaves the game installation/profile alone.

Build with `python build.py --sdk <Android SDK> --jdk <JDK> --keystore <existing Android debug keystore> --output <new APK path>`. The script checks all three permissions, DEX inclusion and APK signature, and writes a hash receipt. It uses the standard Android debug-keystore password; do not supply a production keystore.

On one Quest: install through your normal sideload workflow, open **Deadly Jenga Bluetooth Test** under Unknown Sources, grant Nearby devices, and choose **Advertise test room**. On the Windows PC run the BluetoothPeer guest tool. Verify the request shows the PC name, and that only approval delivers the test code. Repeat deny, wait-for-expiry, cancel, permission denial, Bluetooth-off, and headset sleep/resume. Different Wi-Fi networks should not affect this radio exchange.

For the reverse direction use the Windows helper's host mode with protocol `ble-probe-1`, then **Find nearby rooms** on Quest. The PC's ability to advertise must first pass `--probe-host`; advertised role support alone is insufficient. The initial PC advertising probe aborted, so this direction is currently unverified.

Names in this diagnostic are saved separately from the game's wardrobe name. A received test code is not evidence of a joined multiplayer game. Final acceptance still requires the full game APK and two Quests on different networks.
