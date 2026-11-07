"""Research-only Classic HID boot host. Input injection is explicitly opt-in."""
import asyncio
import collections
import ctypes
from dataclasses import dataclass
import struct

from bumble import l2cap


@dataclass(frozen=True)
class InputEvent:
    kind: str
    code: int
    value: int = 0


class BootInput:
    def __init__(self, emit):
        self.emit = emit
        self.keys, self.buttons = {}, {}
        self.counts = collections.Counter()

    def keyboard(self, peer, report):
        if len(report) != 8 or report[1] != 0:
            raise ValueError('Expected eight-byte boot keyboard report')
        if any(key in (1, 2, 3) for key in report[2:]):
            return  # Rollover is not a release of all held keys.
        keys = set(report[2:]) - {0}
        keys.update(0xE0 + bit for bit in range(8) if report[0] & (1 << bit))
        old = self.keys.get(peer, set())
        for key in sorted(old - keys):
            self._key(key, False)
        for key in sorted(keys - old):
            self._key(key, True)
        self.keys[peer] = keys

    def _key(self, key, down):
        before = self.counts[key]
        self.counts[key] += 1 if down else -1
        if (before == 0 and down) or (self.counts[key] == 0 and not down):
            self.emit(InputEvent('key_down' if down else 'key_up', key))

    def mouse(self, peer, report):
        if len(report) != 3 or report[0] & 0xF8:
            raise ValueError('Expected three-byte boot mouse report')
        buttons, x, y = struct.unpack('<Bbb', report)
        old = self.buttons.get(peer, 0)
        self.buttons[peer] = buttons
        for bit in range(3):
            old_union = old
            new_union = buttons
            for other, held in self.buttons.items():
                if other != peer:
                    old_union |= held
                    new_union |= held
            if (old_union ^ new_union) & (1 << bit):
                self.emit(InputEvent('button_down' if new_union & (1 << bit) else 'button_up', bit))
        if x or y:
            self.emit(InputEvent('move', x, y))

    def release(self, peer):
        for key in self.keys.pop(peer, set()):
            self._key(key, False)
        if peer in self.buttons:
            self.mouse(peer, bytes(3))
            self.buttons.pop(peer)


class ClassicHidHub:
    def __init__(self, device, emit):
        self.input = BootInput(emit)
        self.roles, self.pending, self.armed = {}, {}, set()
        device.create_l2cap_server(l2cap.ClassicChannelSpec(0x11), self.on_channel)
        device.create_l2cap_server(l2cap.ClassicChannelSpec(0x13), self.on_channel)

    def on_channel(self, channel):
        handle = channel.connection.handle
        channel.sink = lambda data: self.on_data(handle, channel.psm, data)
        channel.on('close', lambda: self.release(handle))

    def on_data(self, handle, psm, data):
        if psm == 0x11:
            future = self.pending.get(handle)
            if future and not future.done() and data and data[0] >> 4 == 0:
                future.set_result(data == b'\x00')
            return
        if handle not in self.armed or not data or data[0] != 0xA1:
            return
        try:
            # Classic Bluetooth boot reports carry fixed report IDs (keyboard=1, mouse=2).
            if self.roles[handle] == 'keyboard' and len(data) >= 2 and data[1] == 1:
                self.input.keyboard(handle, data[2:])
            elif self.roles[handle] == 'mouse' and len(data) >= 2 and data[1] == 2:
                self.input.mouse(handle, data[2:])
        except ValueError:
            pass  # Unknown reports never become Windows input.

    async def connect(self, connection, role):
        if role not in ('keyboard', 'mouse'):
            raise ValueError('Unknown HID role')
        handle = connection.handle
        self.roles[handle] = role
        connection.on('disconnection', lambda *args: self.release(handle))
        channels = []
        try:
            for psm in (0x11, 0x13):
                channel = await connection.create_l2cap_channel(spec=l2cap.ClassicChannelSpec(psm))
                self.on_channel(channel)
                channels.append(channel)
            future = asyncio.get_running_loop().create_future()
            self.pending[handle] = future
            channels[0].write(b'\x70')  # SET_PROTOCOL: boot mode.
            if not await asyncio.wait_for(future, 3):
                raise ValueError('Peer does not support boot protocol')
            self.armed.add(handle)
        except BaseException:
            self.release(handle)
            await asyncio.gather(*(c.disconnect() for c in channels), return_exceptions=True)
            raise
        finally:
            self.pending.pop(handle, None)

    def release(self, handle):
        self.armed.discard(handle)
        self.roles.pop(handle, None)
        self.input.release(handle)
        future = self.pending.get(handle)
        if future and not future.done():
            future.cancel()


class WinInputSink:
    """SendInput adapter; tests build records but never call SendInput."""
    # HID keyboard usages mapped to IBM set-1 scan codes (0x100 marks E0 prefix).
    SCANS = dict(zip(range(4, 30), [0x1E,0x30,0x2E,0x20,0x12,0x21,0x22,0x23,0x17,0x24,0x25,0x26,0x32,0x31,0x18,0x19,0x10,0x13,0x1F,0x14,0x16,0x2F,0x11,0x2D,0x15,0x2C]))
    SCANS.update(dict(zip(range(30, 40), [2,3,4,5,6,7,8,9,10,11])))
    SCANS.update({40:0x1C,41:1,42:0x0E,43:0x0F,44:0x39,45:0x0C,46:0x0D,
                  47:0x1A,48:0x1B,49:0x2B,51:0x27,52:0x28,53:0x29,54:0x33,55:0x34,56:0x35,
                  57:0x3A,76:0x153,79:0x14D,80:0x14B,81:0x150,82:0x148,
                  0xE0:0x1D,0xE1:0x2A,0xE2:0x38,0xE3:0x15B,
                  0xE4:0x11D,0xE5:0x36,0xE6:0x138,0xE7:0x15C})

    def __init__(self, armed=False):
        from ctypes import wintypes as w
        class Mouse(ctypes.Structure):
            _fields_ = [('dx',w.LONG),('dy',w.LONG),('data',w.DWORD),('flags',w.DWORD),('time',w.DWORD),('extra',ctypes.c_size_t)]
        class Key(ctypes.Structure):
            _fields_ = [('vk',w.WORD),('scan',w.WORD),('flags',w.DWORD),('time',w.DWORD),('extra',ctypes.c_size_t)]
        class Union(ctypes.Union):
            _fields_ = [('mouse',Mouse),('key',Key)]
        class Input(ctypes.Structure):
            _fields_ = [('type',w.DWORD),('data',Union)]
        self.Input, self.armed = Input, armed
        self.send = ctypes.WinDLL('user32', use_last_error=True).SendInput
        self.send.argtypes = [w.UINT,ctypes.POINTER(Input),ctypes.c_int]
        self.send.restype = w.UINT

    def build(self, event):
        record = self.Input()
        if event.kind.startswith('key_'):
            scan = self.SCANS.get(event.code)
            if scan is None:
                return None
            record.type = 1
            record.data.key.scan = scan & 0xFF
            record.data.key.flags = 8 | (1 if scan & 0x100 else 0) | (2 if event.kind == 'key_up' else 0)
        elif event.kind == 'move':
            record.data.mouse.dx, record.data.mouse.dy = event.code, event.value
            record.data.mouse.flags = 1
        else:
            record.data.mouse.flags = ((2,8,32) if event.kind == 'button_down' else (4,16,64))[event.code]
        return record

    def __call__(self, event):
        if not self.armed:
            raise RuntimeError('Windows input injection is not armed')
        record = self.build(event)
        if record is not None and self.send(1, ctypes.byref(record), ctypes.sizeof(record)) != 1:
            raise OSError(ctypes.get_last_error(), 'SendInput failed')
