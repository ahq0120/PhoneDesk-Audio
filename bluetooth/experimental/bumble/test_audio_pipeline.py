import asyncio
import datetime
import math
import struct
import tempfile
import threading

import av
import pytest
from bumble import avdtp
from bumble.core import PhysicalTransport
from bumble.rtp import MediaPacket

from audio_pipeline import DailyLog, PcmBuffer, Pipeline, Receiver, sbc_frames
from listener_smoke import capabilities


def encoded_frames(count=12, rate=48000, layout="stereo"):
    encoder = av.CodecContext.create('sbc', 'w')
    encoder.sample_rate = rate
    encoder.layout = layout
    encoder.format = 's16'
    encoder.bit_rate = min(328000, rate * (1 if layout == "mono" else 2) * 3)
    encoder.open()
    result = []
    for n in range(count):
        frame = av.AudioFrame(format='s16', layout=layout, samples=encoder.frame_size)
        frame.sample_rate = rate
        values = [int(1000 * math.sin(2 * math.pi * 440 * (n * encoder.frame_size + i) / rate))
                  for i in range(encoder.frame_size) for channel in range(1 if layout == "mono" else 2)]
        frame.planes[0].update(struct.pack('<' + 'h' * len(values), *values))
        result.extend(bytes(p) for p in encoder.encode(frame))
    result.extend(bytes(p) for p in encoder.encode(None))
    assert result
    return result


def packet(seq, frame):
    return MediaPacket(2, 0, 0, 0, seq, seq * 128, 1, [], 96, b'\x01' + frame)


def test_packet_validation_and_pcm_bound():
    frame = encoded_frames(1)[0]
    assert sbc_frames(b'\x01' + frame) == [frame]
    for bad in [b'', b'\x00', b'\x81' + frame, b'\x02' + frame, b'\x01' + frame[:-1], b'\x01' + frame + b'x']:
        with pytest.raises(ValueError):
            sbc_frames(bad)
    pcm = PcmBuffer(max_ms=10)
    pcm.write(bytes([1]) * 4000)
    assert len(pcm.data) == pcm.limit
    assert pcm.dropped_bytes == 4000 - pcm.limit
    pcm.write(bytes([2]) * 400)
    assert pcm.read(pcm.limit)[-400:] == bytes([2]) * 400
    assert pcm.read(16) == bytes(16)
    pcm.clear()
    assert not pcm.data


def test_daily_log():
    with tempfile.TemporaryDirectory() as folder:
        now = [datetime.datetime(2026, 1, 1, 23, 59)]
        log = DailyLog(folder, clock=lambda: now[0], limit=300)
        log.write('old', count=1)
        now[0] += datetime.timedelta(days=1)
        log.write('new', count=2)
        assert 'old' not in log.path.read_text()
        for _ in range(20):
            log.write('bounded', count=3)
        assert log.path.stat().st_size < 500
        with pytest.raises(ValueError):
            log.write('bad', address='private')


def test_log_io_failure_is_nonfatal(tmp_path):
    blocked = tmp_path / 'not-a-directory'
    blocked.write_text('file')
    log = DailyLog(blocked)
    log.write('app_started', count=1)
    assert log.last_error is not None


@pytest.mark.asyncio
async def test_overload_and_reconnect(tmp_path):
    pcm = PcmBuffer()
    pipe = Pipeline(pcm, DailyLog(tmp_path), queue_packets=2)
    frame = encoded_frames(1)[0]
    pipe.begin()
    for i in range(20):
        pipe.submit(packet(i, frame))
    assert pipe.queue.qsize() == 2 and pipe.stats['queue_drops'] == 18
    pipe.end()
    assert pipe.queue.empty() and not pcm.data
    pipe.begin()
    pipe.submit(packet(65535, frame))
    pipe.submit(packet(0, frame))
    pipe.submit(packet(0, frame))
    assert pipe.stats['late_or_duplicate'] == 1 and not pipe.stats['missing_packets']
    pipe.start()
    await asyncio.wait_for(pipe.queue.join(), 5)
    assert pipe.stats['pcm_bytes'] > 0
    await pipe.close()
    assert not pcm.data


@pytest.mark.asyncio
async def test_inflight_old_audio_discarded(tmp_path, monkeypatch):
    import audio_pipeline
    entered, release = threading.Event(), threading.Event()

    class SlowDecoder:
        def decode(self, frames):
            entered.set()
            assert release.wait(3)
            return b'\x01\x00\x01\x00' * 128

    monkeypatch.setattr(audio_pipeline, 'SbcDecoder', SlowDecoder)
    pcm = PcmBuffer()
    pipe = Pipeline(pcm, DailyLog(tmp_path))
    pipe.start()
    pipe.begin()
    pipe.submit(packet(1, encoded_frames(1)[0]))
    try:
        assert await asyncio.to_thread(entered.wait, 2)
        pipe.end()
        pipe.begin()
        release.set()
        await asyncio.wait_for(pipe.queue.join(), 5)
        assert not pcm.data
    finally:
        release.set()
        await pipe.close()


@pytest.mark.asyncio
async def test_virtual_audio_transport(tmp_path):
    from bumble import a2dp
    from bumble.controller import Controller
    from bumble.device import Device
    from bumble.link import LocalLink

    link = LocalLink()
    controllers = [Controller('source', link=link, public_address='F0:F1:F2:F3:F4:F5'),
                   Controller('sink', link=link, public_address='F6:F7:F8:F9:FA:FB')]
    source, sink = [Device.with_hci(c.name, c.public_address, c, c) for c in controllers]
    for device in (source, sink):
        device.classic_enabled = True
        device.le_enabled = False
    source.sdp_service_records = {0x10001: [a for a in a2dp.make_audio_source_service_sdp_records(0x10001) if a.id != 0]}
    sink.sdp_service_records = {0x10002: a2dp.make_audio_sink_service_sdp_records(0x10002)}
    pcm = PcmBuffer()
    pipe = Pipeline(pcm, DailyLog(tmp_path))
    receiver = Receiver(sink, pipe, capabilities())
    try:
        await asyncio.gather(source.power_on(), sink.power_on())
        await sink.set_connectable(True)
        for session in range(2):
            connection = await source.connect(sink.public_address, transport=PhysicalTransport.BR_EDR)
            protocol = await avdtp.Protocol.connect(connection)
            endpoints = list(await protocol.discover_remote_endpoints())
            selected = avdtp.MediaCodecCapabilities(media_type=avdtp.AVDTP_AUDIO_MEDIA_TYPE, media_codec_type=0, media_codec_information=bytes.fromhex("12150235"))
            stream = await protocol.create_stream(protocol.add_source(selected, None), endpoints[0])
            await stream.start()
            for i, frame in enumerate(encoded_frames()):
                stream.rtp_channel.write(bytes(packet(i, frame)))
                await asyncio.sleep(0.003)
            await asyncio.sleep(0.03)
            await asyncio.wait_for(pipe.queue.join(), 5)
            samples = pcm.read(len(pcm.data))
            assert len(samples) > 0 and any(samples)
            assert not pipe.stats['decode_errors']
            await stream.close()
            await connection.disconnect()
            await asyncio.sleep(0)
            assert receiver.handle is None and not pcm.data
        assert pipe.stats['sessions'] == 2
    finally:
        await pipe.close()


@pytest.mark.parametrize("rate", [16000, 32000, 44100, 48000])
@pytest.mark.parametrize("layout", ["mono", "stereo"])
def test_real_decode_and_resample(rate, layout):
    from audio_pipeline import SbcDecoder
    frames = encoded_frames(20, rate=rate, layout=layout)
    for frame in frames:
        assert sbc_frames(b"\x01" + frame) == [frame]
    pcm = SbcDecoder().decode(frames)
    assert len(pcm) % 4 == 0 and any(pcm)
