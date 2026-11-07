import asyncio
import ctypes
import struct
from types import SimpleNamespace

import pytest
from bumble import a2dp, avdtp, l2cap, hci
from bumble.controller import Controller
from bumble.core import PhysicalTransport
from bumble.device import Device
from bumble.link import LocalLink

from audio_pipeline import PcmBuffer, Pipeline, DailyLog, Receiver
from audio_relay import SbcRelay, mix_pcm
from hid_bridge import BootInput, ClassicHidHub, InputEvent, WinInputSink
from listener_smoke import capabilities
from test_audio_pipeline import encoded_frames, packet
from hogp_bridge import HogpHost
from test_hogp import boot_service


def test_boot_reports_release_and_rollover():
    events = []
    inputs = BootInput(events.append)
    inputs.keyboard('one', bytes([2,0,4,0,0,0,0,0]))
    inputs.keyboard('one', bytes([2,0,1,1,1,1,1,1]))
    assert len(events) == 2
    inputs.keyboard('two', bytes([0,0,4,0,0,0,0,0]))
    inputs.release('one')
    assert InputEvent('key_up', 4) not in events
    inputs.release('two')
    assert events[-1] == InputEvent('key_up', 4)
    inputs.mouse('mouse', bytes([1,255,2]))
    assert events[-1] == InputEvent('move', -1, 2)
    inputs.release('mouse')
    assert events[-1] == InputEvent('button_up', 0)
    with pytest.raises(ValueError):
        inputs.keyboard('bad', b'\x00')


def test_wininput_records_without_injection():
    sink = WinInputSink()  # Disarmed by default.
    assert ctypes.sizeof(sink.Input) == (40 if ctypes.sizeof(ctypes.c_void_p) == 8 else 28)
    right = sink.build(InputEvent('key_down', 79))
    assert right.data.key.scan == 0x4D and right.data.key.flags == 9
    up = sink.build(InputEvent('key_up', 4))
    assert up.data.key.flags == 10
    with pytest.raises(RuntimeError):
        sink(InputEvent('key_down', 4))


def test_mixer_saturates():
    assert struct.unpack('<hh', mix_pcm(struct.pack('<hh',30000,-30000), struct.pack('<hh',10000,-10000))) == (32767,-32768)


def test_headphone_capability_gate():
    endpoint = SimpleNamespace(tsep=avdtp.AVDTP_TSEP_SNK, in_use=False, capabilities=[capabilities()])
    assert SbcRelay.compatible(endpoint)
    endpoint.in_use = True
    assert not SbcRelay.compatible(endpoint)
    endpoint.in_use = False
    endpoint.capabilities = [avdtp.MediaCodecCapabilities(media_type=0, media_codec_type=0, media_codec_information=bytes.fromhex('28150235'))]
    assert not SbcRelay.compatible(endpoint)  # Mono-only / no 48 kHz stereo.


class FakeBootPeripheral:
    def __init__(self, device, report_id=1):
        self.report_id = report_id
        self.interrupt = None
        device.create_l2cap_server(l2cap.ClassicChannelSpec(0x11), self.control)
        device.create_l2cap_server(l2cap.ClassicChannelSpec(0x13), self.intr)

    def control(self, channel):
        channel.sink = lambda data: channel.write(b'\x00' if data == b'\x70' else b'\x03')

    def intr(self, channel):
        self.interrupt = channel

    def report(self, data):
        self.interrupt.write(b'\xA1' + bytes([self.report_id]) + data)


@pytest.mark.asyncio
@pytest.mark.parametrize("le_keyboard", [False, True])
async def test_keyboard_mouse_switch_and_headphones_same_host(tmp_path, le_keyboard):
    """Five virtual controllers; central host owns HID host plus A2DP sink/source."""
    link = LocalLink()
    devices = []
    for i, name in enumerate(('host','keyboard','mouse','switch','headphones')):
        c = Controller(name, link=link, public_address=f'F0:F1:F2:F3:F4:{i:02X}')
        d = Device.with_hci(name, hci.Address(bytes(c.public_address), hci.Address.RANDOM_DEVICE_ADDRESS), c, c)
        d.classic_enabled, d.le_enabled = True, False
        devices.append(d)
    host, keyboard, mouse, source, headphones = devices
    if le_keyboard:
        host.le_enabled = keyboard.le_enabled = True
        keyboard.classic_enabled = False
    service, le_report = boot_service()
    keyboard.add_service(service)
    keys, pointer = FakeBootPeripheral(keyboard), FakeBootPeripheral(mouse, report_id=2)
    events = []
    hub = ClassicHidHub(host, events.append)
    le_hub = HogpHost(events.append)
    switch_pcm, headphone_pcm, pc_pcm = PcmBuffer(), PcmBuffer(), PcmBuffer()
    incoming = Pipeline(switch_pcm, DailyLog(tmp_path / 'host'))
    outgoing = Pipeline(headphone_pcm, DailyLog(tmp_path / 'headphones'))
    receiver = Receiver(host, incoming, capabilities())
    Receiver(headphones, outgoing, capabilities())
    host.sdp_service_records = {1: a2dp.make_audio_sink_service_sdp_records(1)}
    headphones.sdp_service_records = {2: a2dp.make_audio_sink_service_sdp_records(2)}
    source.sdp_service_records = {3: [a for a in a2dp.make_audio_source_service_sdp_records(3) if a.id != 0]}
    connections, relay_task = [], None
    try:
        await asyncio.gather(*(d.power_on() for d in devices))
        await asyncio.gather(*(d.set_connectable(True) for d in devices))
        for device, role in ((keyboard,'keyboard'),(mouse,'mouse')):
            if le_keyboard and role == 'keyboard':
                await device.start_advertising(auto_restart=False)
                connection = await host.connect(device.random_address, timeout=5)
                await asyncio.wait_for(le_hub.connect(connection, role), 8)
            else:
                connection = await host.connect(device.public_address, transport=PhysicalTransport.BR_EDR)
                await hub.connect(connection, role)
            connections.append(connection)
        hp_connection = await host.connect(headphones.public_address, transport=PhysicalTransport.BR_EDR)
        connections.append(hp_connection)
        hp_stream = await SbcRelay.connect(hp_connection, receiver.listener)
        relay = SbcRelay(hp_stream, switch_pcm, pc_pcm)
        relay_task = asyncio.create_task(relay.run())
        source_connection = await source.connect(host.public_address, transport=PhysicalTransport.BR_EDR)
        connections.append(source_connection)
        protocol = await avdtp.Protocol.connect(source_connection)
        endpoint = list(await protocol.discover_remote_endpoints())[0]
        selected = avdtp.MediaCodecCapabilities(media_type=0, media_codec_type=0, media_codec_information=bytes.fromhex('12150235'))
        stream = await protocol.create_stream(protocol.add_source(selected, None), endpoint)
        await stream.start()
        for i, frame in enumerate(encoded_frames(40)):
            stream.rtp_channel.write(bytes(packet(i, frame)))
            report = bytes([0,0,4 if i%2 == 0 else 0,0,0,0,0,0])
            if le_keyboard:
                await keyboard.notify_subscribers(le_report, report)
            else:
                keys.report(report)
            pointer.report(bytes([i%2,1,255]))
            pc_pcm.write(struct.pack('<hh',200,200) * 128)
            await asyncio.sleep(.004)
        await asyncio.sleep(.04)
        await asyncio.wait_for(outgoing.queue.join(), 3)
        assert incoming.stats['packets'] == 40 and incoming.stats['pcm_bytes'] > 0
        assert outgoing.stats['packets'] > 10 and outgoing.stats['pcm_bytes'] > 0
        assert not incoming.stats['decode_errors'] and not outgoing.stats['decode_errors']
        assert any(headphone_pcm.data)
        assert any(e.kind == 'move' for e in events) and any(e.kind == 'key_down' for e in events)
        assert len(hub.armed) + len(le_hub.subscriptions) == 2
        await connections[0].disconnect()
        assert len(hub.armed) == 1  # Mouse/audio do not disappear with keyboard.
        await stream.close()
        await hp_stream.close()
        await asyncio.wait_for(asyncio.shield(relay_task), 1)
    finally:
        if relay_task:
            relay_task.cancel()
            try:
                await relay_task
            except asyncio.CancelledError:
                pass
        for connection in connections:
            if connection.handle in connection.device.connections:
                await connection.disconnect()
        await incoming.close()
        await outgoing.close()
