"""Experimental SBC -> bounded PCM -> shared WASAPI. Never stores audio on disk."""
import asyncio
import collections
import datetime
import json
from pathlib import Path
import threading

import av


class DailyLog:
    def __init__(self, folder, clock=datetime.datetime.now, limit=2 * 1024 * 1024):
        self.path = Path(folder) / 'current.log'
        self.clock, self.limit = clock, limit
        self.lock = threading.Lock()
        self.last_error = None

    def write(self, event, **counts):
        # Only caller-defined event names and numeric measurements; no exception text,
        # device IDs, keys, packet payloads or audio samples.
        if not all(isinstance(v, (int, float, bool)) for v in counts.values()):
            raise ValueError('Log fields must be numeric')
        now = self.clock()
        header = 'date=' + now.date().isoformat()
        with self.lock:
            try:
                self.path.parent.mkdir(parents=True, exist_ok=True)
                first = ''
                if self.path.exists():
                    with self.path.open(encoding='utf-8') as f:
                        first = f.readline().strip()
                reset = first != header or self.path.stat().st_size >= self.limit
                with self.path.open('w' if reset else 'a', encoding='utf-8') as f:
                    if reset:
                        f.write(header + '\n')
                    f.write(json.dumps({'time': now.isoformat(), 'event': event, **counts}) + '\n')
                self.last_error = None
            except OSError as error:
                self.last_error = error.errno



def sbc_frames(payload):
    """Validate one unfragmented A2DP SBC payload before passing frames to FFmpeg."""
    if not payload or payload[0] & 0xF0 or not (payload[0] & 15):
        raise ValueError('Invalid or fragmented SBC payload')
    remaining = payload[1:]
    frames = []
    for _ in range(payload[0] & 15):
        if len(remaining) < 4 or remaining[0] != 0x9C:
            raise ValueError('Invalid SBC header')
        h, bitpool = remaining[1:3]
        blocks, subbands = 4 * (((h >> 4) & 3) + 1), 8 if h & 1 else 4
        mode = (h >> 2) & 3
        channels = 1 if mode == 0 else 2
        maximum = (16 if mode in (0, 1) else 32) * subbands
        if not 2 <= bitpool <= min(250, maximum):
            raise ValueError('Invalid bitpool')
        bits = blocks * bitpool * (channels if mode in (0, 1) else 1)
        if mode == 3:
            bits += subbands
        length = 4 + subbands * channels // 2 + (bits + 7) // 8
        if len(remaining) < length:
            raise ValueError('Truncated SBC frame')
        frames.append(remaining[:length])
        remaining = remaining[length:]
    if remaining:
        raise ValueError('Unexpected trailing SBC bytes')
    return frames


class PcmBuffer:
    def __init__(self, max_ms=120):
        self.limit = 48000 * 4 * max_ms // 1000  # 48 kHz, stereo, int16
        self.data = bytearray()
        self.lock = threading.Lock()
        self.dropped_bytes = self.underflows = 0

    def write(self, data):
        if len(data) % 4:
            raise ValueError('Unaligned PCM')
        with self.lock:
            excess = max(0, len(self.data) + len(data) - self.limit)
            if excess:
                self.dropped_bytes += excess
            self.data = (self.data + data)[-self.limit:]

    def read(self, size):
        with self.lock:
            result = bytes(self.data[:size])
            del self.data[:size]
            if len(result) < size:
                self.underflows += 1
            return result + bytes(size - len(result))

    def clear(self):
        with self.lock:
            self.data.clear()


class SbcDecoder:
    def __init__(self):
        self.codec = self.resampler = self.configuration = None

    def decode(self, frames):
        result = bytearray()
        for frame in frames:
            header = frame[1]
            rate = (16000, 32000, 44100, 48000)[header >> 6]
            layout = 'mono' if (header >> 2) & 3 == 0 else 'stereo'
            if self.configuration != (rate, layout):
                # Raw SBC packets need channel/rate context for this FFmpeg build.
                self.codec = av.CodecContext.create('sbc', 'r')
                self.codec.sample_rate, self.codec.layout = rate, layout
                self.resampler = av.AudioResampler(format='s16', layout='stereo', rate=48000)
                self.configuration = (rate, layout)
            for decoded in self.codec.decode(av.Packet(frame)):
                for pcm in self.resampler.resample(decoded):
                    result.extend(bytes(pcm.planes[0])[:pcm.samples * 4])
        return bytes(result)


class Pipeline:
    """Single-session receiver. Decoder runs off the BLE/AVDTP event loop."""
    def __init__(self, pcm, log, queue_packets=6):
        self.pcm, self.log = pcm, log
        self.queue = asyncio.Queue(maxsize=queue_packets)
        self.stats = collections.Counter()
        self.active = False
        self.epoch = 0
        self.sequence = None
        self.worker = None

    def start(self):
        if self.worker is None:
            self.worker = asyncio.create_task(self.run())

    def begin(self):
        self.end()
        self.active = True
        self.stats['sessions'] += 1
        self.log.write('stream_started', sessions=self.stats['sessions'])

    def end(self):
        self.active = False
        self.epoch += 1
        self.sequence = None
        self.pcm.clear()
        while not self.queue.empty():
            self.queue.get_nowait()
            self.queue.task_done()

    def submit(self, packet):
        if not self.active:
            return
        seq = packet.sequence_number
        if self.sequence is not None:
            delta = (seq - self.sequence) & 0xFFFF
            if delta == 0 or delta > 0x8000:
                self.stats['late_or_duplicate'] += 1
                return
            self.stats['missing_packets'] += delta - 1
        self.sequence = seq
        try:
            frames = sbc_frames(packet.payload)
        except ValueError:
            self.stats['invalid_or_fragmented'] += 1
            return
        if self.queue.full():
            self.queue.get_nowait()
            self.queue.task_done()
            self.stats['queue_drops'] += 1
        self.queue.put_nowait((self.epoch, frames))
        self.stats['packets'] += 1

    async def run(self):
        decoder, epoch = None, None
        while True:
            ticket, frames = await self.queue.get()
            try:
                if ticket != epoch:
                    decoder, epoch = SbcDecoder(), ticket
                pcm = await asyncio.to_thread(decoder.decode, frames)
                # A close/reconnect may occur while a background decode completes.
                if self.active and ticket == self.epoch:
                    self.pcm.write(pcm)
                    self.stats['pcm_bytes'] += len(pcm)
            except Exception:
                self.stats['decode_errors'] += 1
                decoder, epoch = None, None
            finally:
                self.queue.task_done()

    async def close(self):
        self.end()
        if self.worker:
            self.worker.cancel()
            try:
                await self.worker
            except asyncio.CancelledError:
                pass
            self.worker = None
        self.log.write('stream_summary', **dict(self.stats),
                       pcm_dropped=self.pcm.dropped_bytes, underflows=self.pcm.underflows)


class WasapiOutput:
    def __init__(self, pcm, device=None):
        import sounddevice as sd
        candidates = [i for i, d in enumerate(sd.query_devices())
                      if d['max_output_channels'] >= 2
                      and sd.query_hostapis(d['hostapi'])['name'] == 'Windows WASAPI']
        if device is None:
            api = next(a for a in sd.query_hostapis() if a['name'] == 'Windows WASAPI')
            device = api['default_output_device']
        if device not in candidates:
            raise ValueError('A stereo WASAPI output is required')
        self.stream = sd.RawOutputStream(
            device=device, samplerate=48000, channels=2, dtype='int16', blocksize=480,
            extra_settings=sd.WasapiSettings(exclusive=False),
            callback=lambda out, frames, timing, status: out.__setitem__(slice(None), pcm.read(frames * 4)))

    def start(self):
        self.stream.start()

    def close(self):
        self.stream.stop()
        self.stream.close()


class Receiver:
    def __init__(self, device, pipeline, capabilities):
        from bumble import avdtp
        self.device, self.pipeline = device, pipeline
        self.handle = None
        self.listener = avdtp.Listener.for_device(device)
        self.listener.on('connection', self.on_connection)
        self.capabilities = capabilities
        self.pipeline.start()

    def on_connection(self, protocol):
        connection = protocol.l2cap_channel.connection
        if self.handle is not None and self.handle != connection.handle:
            self.pipeline.stats['rejected_connections'] += 1
            asyncio.create_task(connection.disconnect())
            return
        self.handle = connection.handle
        endpoint = protocol.add_sink(self.capabilities)
        endpoint.on('start', self.pipeline.begin)
        for event in ('suspend', 'close', 'abort', 'rtp_channel_close'):
            endpoint.on(event, self.pipeline.end)
        endpoint.on('rtp_packet', self.pipeline.submit)

        def disconnected(*args):
            if self.handle == connection.handle:
                self.pipeline.end()
                self.handle = None
        connection.on('disconnection', disconnected)
