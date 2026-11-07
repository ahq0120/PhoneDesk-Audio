"""Experimental 48 kHz SBC relay. PC audio input is supplied PCM, not loopback capture."""
import asyncio
from array import array
import sys

import av
from bumble import avdtp
from bumble.rtp import MediaPacket


def mix_pcm(left, right):
    if len(left) != len(right) or len(left) % 4:
        raise ValueError('Expected aligned, equally sized stereo PCM')
    a, b = array('h'), array('h')
    a.frombytes(left)
    b.frombytes(right)
    if sys.byteorder != 'little':
        a.byteswap()
        b.byteswap()
    result = array('h', (max(-32768, min(32767, x + y)) for x, y in zip(a, b)))
    if sys.byteorder != 'little':
        result.byteswap()
    return result.tobytes()


class SbcRelay:
    def __init__(self, stream, switch_pcm, pc_pcm=None):
        self.stream, self.switch_pcm, self.pc_pcm = stream, switch_pcm, pc_pcm
        self.encoder = av.CodecContext.create('sbc', 'w')
        self.encoder.sample_rate = 48000
        self.encoder.layout = 'stereo'
        self.encoder.format = 's16'
        self.encoder.bit_rate = 288000
        self.encoder.open()
        self.sequence = self.timestamp = self.sent = 0

    @staticmethod
    def compatible(endpoint):
        if endpoint.tsep != avdtp.AVDTP_TSEP_SNK or endpoint.in_use:
            return False
        for cap in endpoint.capabilities:
            if isinstance(cap, avdtp.MediaCodecCapabilities) and cap.media_codec_type == 0:
                info = bytes(cap.media_codec_information)
                if len(info) == 4 and info[0] & 0x12 == 0x12 and info[1] & 0x15 == 0x15 and info[2] <= 2 and info[3] >= 53:
                    return True
        return False

    @staticmethod
    async def connect(connection, listener):
        protocol = await avdtp.Protocol.connect(connection)
        listener.set_server(connection, protocol)
        try:
            endpoints = list(await protocol.discover_remote_endpoints())
            candidates = [e for e in endpoints if SbcRelay.compatible(e)]
            if not candidates:
                raise ValueError('No compatible 48 kHz stereo SBC sink')
            # Research configuration only: no AAC, HFP/microphone, AVRCP or fallback.
            selected = avdtp.MediaCodecCapabilities(
                media_type=avdtp.AVDTP_AUDIO_MEDIA_TYPE, media_codec_type=0,
                media_codec_information=bytes.fromhex('12150235'))
            stream = await protocol.create_stream(protocol.add_source(selected, None), candidates[0])
            await stream.start()
            return stream
        except BaseException:
            listener.remove_server(connection)
            await protocol.l2cap_channel.disconnect()
            raise

    def send_block(self):
        if self.stream.state != avdtp.State.STREAMING or self.stream.rtp_channel is None:
            raise RuntimeError('Headphone stream is not open')
        samples = self.encoder.frame_size
        pcm = self.switch_pcm.read(samples * 4)
        if self.pc_pcm is not None:
            pcm = mix_pcm(pcm, self.pc_pcm.read(samples * 4))
        frame = av.AudioFrame(format='s16', layout='stereo', samples=samples)
        frame.sample_rate = 48000
        frame.planes[0].update(pcm)
        for encoded in self.encoder.encode(frame):
            packet = MediaPacket(2,0,0,0,self.sequence,self.timestamp,1,[],96,b'\x01' + bytes(encoded))
            self.stream.rtp_channel.write(bytes(packet))
            self.sequence = (self.sequence + 1) & 0xFFFF
            self.timestamp = (self.timestamp + samples) & 0xFFFFFFFF
            self.sent += 1

    async def run(self):
        loop = asyncio.get_running_loop()
        next_send = loop.time()
        interval = self.encoder.frame_size / 48000
        try:
            while self.stream.state == avdtp.State.STREAMING and self.stream.rtp_channel is not None:
                self.send_block()
                next_send += interval
                now = loop.time()
                # Do not send a burst of stale audio after an event-loop stall.
                if now - next_send > .020:
                    self.switch_pcm.clear()
                    if self.pc_pcm is not None:
                        self.pc_pcm.clear()
                    next_send = now
                await asyncio.sleep(max(0, next_send - now))
        finally:
            self.switch_pcm.clear()
            if self.pc_pcm is not None:
                self.pc_pcm.clear()
