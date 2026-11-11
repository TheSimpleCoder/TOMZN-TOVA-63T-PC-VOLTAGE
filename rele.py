import tinytuya
import base64
import time

DEVICE_ID = ""
IP = ""
LOCAL_KEY = ""

VOLTAGE_DP = "6"
HEARTBEAT_INTERVAL = 10


def decode_voltage(value: str) -> float:
    """Decode voltage value from Tuya Base64 payload."""
    raw = base64.b64decode(value)
    return int.from_bytes(raw[:2], byteorder="big") / 10


def main():
    device = tinytuya.OutletDevice(
        dev_id=DEVICE_ID,
        address=IP,
        local_key=LOCAL_KEY,
        version=3.5
    )

    device.set_socketPersistent(True)

    device.send(device.generate_payload(tinytuya.DP_QUERY))

    last_heartbeat = time.monotonic()

    while True:
        try:
            data = device.receive()

            if data and "dps" in data:
                voltage = data["dps"].get(VOLTAGE_DP)

                if voltage:
                    print(f"VOLTAGE:{decode_voltage(voltage)}")
                    time.sleep(3)
                    device.set_value('35', 'online')
                else:
                    continue

            if time.monotonic() - last_heartbeat >= HEARTBEAT_INTERVAL:
                device.send(device.generate_payload(tinytuya.HEART_BEAT))
                last_heartbeat = time.monotonic()

        except (ValueError, TypeError, KeyError) as error:
            print(f"Decode error: {error}")

        except KeyboardInterrupt:
            break

    device.close()


if __name__ == "__main__":
    main()