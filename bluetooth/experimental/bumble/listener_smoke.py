"""Hardware-free AVDTP acceptance test. No USB, drivers, real audio or Windows A2DP API."""
import asyncio
from bumble import a2dp, avdtp
from bumble.controller import Controller
from bumble.core import PhysicalTransport
from bumble.device import Device
from bumble.link import LocalLink


def capabilities():
    return avdtp.MediaCodecCapabilities(
        media_type=avdtp.AVDTP_AUDIO_MEDIA_TYPE,
        media_codec_type=a2dp.A2DP_SBC_CODEC_TYPE,
        media_codec_information=bytes.fromhex("21150235"),
    )


async def run():
    link = LocalLink()
    controllers = [Controller("source", link=link, public_address="F0:F1:F2:F3:F4:F5"),
                   Controller("sink", link=link, public_address="F6:F7:F8:F9:FA:FB")]
    devices = [Device.with_hci(c.name, c.public_address, c, c) for c in controllers]
    source, sink = devices
    for device in devices:
        device.classic_enabled = True
        device.le_enabled = False
    # Deliberately omit the remote source's record-handle attribute.
    record = a2dp.make_audio_source_service_sdp_records(0x10001)
    source.sdp_service_records = {0x10001: [a for a in record if a.id != 0]}
    assert all(a.id != 0 for a in source.sdp_service_records[0x10001])
    sink.sdp_service_records = {0x10002: a2dp.make_audio_sink_service_sdp_records(0x10002)}
    listener = avdtp.Listener.for_device(sink)
    listener.on("connection", lambda protocol: protocol.add_sink(capabilities()))
    await asyncio.gather(*(d.power_on() for d in devices))
    await sink.set_connectable(True)
    connection = await source.connect(sink.public_address, transport=PhysicalTransport.BR_EDR)
    protocol = await avdtp.Protocol.connect(connection)
    endpoints = list(await protocol.discover_remote_endpoints())
    assert len(endpoints) == 1
    local_source = protocol.add_source(capabilities(), None)
    stream = await protocol.create_stream(local_source, endpoints[0])
    await stream.start()
    assert stream.state == avdtp.State.STREAMING
    await stream.close()
    await connection.disconnect()
    print("PASS: virtual BR/EDR -> AVDTP Discover -> Configure -> Open -> Start -> Close")
    print("Source SDP omitted record handle; sink accepted incoming connection without source SDP lookup.")
    print("This does NOT prove Switch 2 hardware, USB support, decoding, playback or latency.")


if __name__ == "__main__":
    asyncio.run(asyncio.wait_for(run(), timeout=15))
