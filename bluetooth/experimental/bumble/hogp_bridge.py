"""Experimental encrypted LE HID boot host; no generic report-descriptor parser."""
from bumble.core import UUID
from bumble.device import Peer
from hid_bridge import BootInput


class HogpHost:
    def __init__(self, emit):
        self.input = BootInput(emit)
        self.subscriptions = {}

    async def connect(self, connection, role):
        if role not in ('keyboard', 'mouse'):
            raise ValueError('Unknown HID role')
        if not connection.is_encrypted:
            await connection.pair()
        if not connection.is_encrypted:
            raise RuntimeError('HOGP input requires encryption')
        peer = Peer(connection)
        services = await peer.discover_service(UUID.from_16_bits(0x1812))
        input_uuid = UUID.from_16_bits(0x2A22 if role == 'keyboard' else 0x2A33)
        for service in services:
            chars = await peer.discover_characteristics(service=service)
            mode = next((c for c in chars if c.uuid == UUID.from_16_bits(0x2A4E)), None)
            report = next((c for c in chars if c.uuid == input_uuid), None)
            if mode is None or report is None:
                continue
            await mode.write_value(b'\x00')
            if await mode.read_value() != b'\x00':
                raise ValueError('Boot protocol was not selected')
            token = ('le', connection.handle)

            def notification(data):
                try:
                    if role == 'keyboard':
                        self.input.keyboard(token, data)
                    else:
                        self.input.mouse(token, data)
                except ValueError:
                    pass

            await report.subscribe(notification)
            self.subscriptions[token] = (report, notification)
            connection.on('disconnection', lambda *args: self.release(token))
            return
        raise ValueError('No supported HOGP boot service; report-mode parsing is not implemented')

    def release(self, token):
        self.subscriptions.pop(token, None)
        self.input.release(token)
